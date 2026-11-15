// In-memory stand-in for the git port that version-tag.mjs's run(args, git) uses, shared by its unit tests.
// It models one repository and its origin. Tests read the resulting tags with snapshot() instead of watching calls.
//
// The port (every method is async; a failed git operation rejects with an Error holding git's message):
//   defaultBranch()               -> origin's default branch name, such as "main"
//   fetch(branch)                 -> brings only that branch up to date (the real port also turns tags and pruning off)
//   currentBranch()               -> the checked-out branch name, or undefined when HEAD is detached
//   isClean()                     -> true when git status shows no staged, unstaged, or untracked files
//   aheadBehind(branch)           -> { ahead, behind }: commits the local branch has that origin/<branch> lacks, and the reverse
//   originCommit(branch)          -> full id of the newest commit of origin/<branch>
//   localTags()                   -> Map of tag name -> { id, commit }, as remoteTags() gives for origin: the tag's own object id and
//                                    the commit it points to (an annotated tag is peeled)
//   remoteTags()                  -> Map of tag name -> { id, commit }: the tag's own object id and the commit it points to,
//                                    from one listing of origin. For a lightweight tag the two are equal.
//   commitInfo(id)                -> { shortId, subject }, or undefined when that commit isn't here
//   createTag(name, commit, msg)  -> makes an annotated tag here and returns the new tag object's id
//   pushTag(name, id)             -> pushes the tag object with that id to origin as `name`, whatever the tag here is now;
//                                    rejects when origin already has a tag of that name
//   deleteLocalTag(name, id)      -> deletes the tag here only if its own id is still `id`; otherwise rejects with git's
//                                    "is at <id> but expected <id>" refusal and deletes nothing
//   deleteRemoteTag(name, id)     -> deletes the tag on origin only if its own id is still `id`; otherwise rejects with
//                                    git's "(stale info)" refusal and deletes nothing
//
// Tag options (`local`, `remote`) map a tag name to a commit id, which makes an annotated tag whose own id is derived
// from it, or to { commit, id } (id: commit makes a lightweight tag). Tests change origin behind the script's back with
// setRemoteTag(), removeRemoteTag() and setLocalTag(), or with the afterRemoteTagsRead / afterLocalTagsRead options, functions called with the
// fake right after the first remoteTags() / localTags() read, or the afterCreateTag option, called right after createTag().
import { createHash } from 'node:crypto';

/** Full lowercase hex commit ids, 40 and 64 characters, as git prints them. */
export const commitA = 'a1'.repeat(20);
export const commitB = 'b2'.repeat(20);
export const commitC = 'c3'.repeat(32);

const tagObjectId = (name, commit) => createHash('sha1').update(`${name}:${commit}`).digest('hex');
const remoteEntry = (name, value) => (typeof value === 'string' ? { commit: value, id: tagObjectId(name, value) } : { id: tagObjectId(name, value.commit), ...value });

export function createFakeGit(options = {}) {
  const state = {
    defaultBranch: 'main',
    branch: 'main',
    clean: true,
    ahead: 0,
    behind: 0,
    originHead: 'c1',
    local: {},
    remote: {},
    afterFetch: {},
    afterRemoteTagsRead: undefined,
    afterLocalTagsRead: undefined,
    afterCreateTag: undefined,
    failFetch: undefined,
    refusePush: undefined,
    refuseRemoteDelete: undefined,
    refuseLocalDelete: undefined,
    ...options,
    commits: {
      c1: { shortId: 'abc1234', subject: 'Add release notes' },
      [commitA]: { shortId: 'a1a1a1a', subject: 'Add release notes' },
      [commitB]: { shortId: 'b2b2b2b', subject: 'Fix the build' },
      ...options.commits,
    },
  };
  const local = new Map(Object.entries(state.local).map(([name, value]) => [name, remoteEntry(name, value)]));
  const remote = new Map(Object.entries(state.remote).map(([name, value]) => [name, remoteEntry(name, value)]));
  // Every tag object that ever existed here, by id, so a push can send one that the tag here no longer is.
  const objects = new Map([...local.values(), ...remote.values()].map((entry) => [entry.id, entry]));
  const annotated = new Set();
  const calls = [];
  const fetchedBranches = [];
  let fetched = false;
  let remoteRead = false;
  let localRead = false;

  const track = (name, work) => async (...args) => {
    calls.push(name);
    return work(...args);
  };

  const git = {
    fetch: track('fetch', async (branch) => {
      if (state.failFetch) throw new Error(state.failFetch);
      fetched = true;
      fetchedBranches.push(branch);
      Object.assign(state, state.afterFetch);
    }),
    defaultBranch: track('defaultBranch', async () => state.defaultBranch),
    currentBranch: track('currentBranch', async () => state.branch),
    isClean: track('isClean', async () => state.clean),
    aheadBehind: track('aheadBehind', async () => ({ ahead: state.ahead, behind: state.behind })),
    originCommit: track('originCommit', async () => state.originHead),
    localTags: track('localTags', async () => {
      const listing = new Map([...local].map(([name, entry]) => [name, { ...entry }]));
      if (!localRead) {
        localRead = true;
        state.afterLocalTagsRead?.(git);
      }
      return listing;
    }),
    remoteTags: track('remoteTags', async () => {
      const listing = new Map([...remote].map(([name, entry]) => [name, { ...entry }]));
      if (!remoteRead) {
        remoteRead = true;
        state.afterRemoteTagsRead?.(git);
      }
      return listing;
    }),
    commitInfo: track('commitInfo', async (id) => {
      const info = state.commits[id];
      return info ? { ...info } : undefined;
    }),
    createTag: track('createTag', async (name, commit, message) => {
      if (local.has(name)) throw new Error(`fatal: tag '${name}' already exists`);
      if (!message) throw new Error('an annotated tag needs a message');
      const entry = remoteEntry(name, commit);
      local.set(name, entry);
      objects.set(entry.id, entry);
      annotated.add(name);
      state.afterCreateTag?.(git);
      return entry.id;
    }),
    pushTag: track('pushTag', async (name, id) => {
      if (!objects.has(id)) throw new Error(`error: src refspec ${id} does not match any`);
      if (state.refusePush) throw new Error(state.refusePush);
      if (remote.has(name)) throw new Error(`! [rejected] ${name} -> ${name} (already exists)`);
      remote.set(name, { ...objects.get(id) });
    }),
    deleteLocalTag: track('deleteLocalTag', async (name, id) => {
      if (state.refuseLocalDelete) throw new Error(state.refuseLocalDelete);
      if (!local.has(name)) throw new Error(`error: tag '${name}' not found.`);
      if (local.get(name).id !== id) throw new Error(`error: cannot lock ref 'refs/tags/${name}': is at ${local.get(name).id} but expected ${id}`);
      local.delete(name);
      annotated.delete(name);
    }),
    deleteRemoteTag: track('deleteRemoteTag', async (name, id) => {
      if (state.refuseRemoteDelete) throw new Error(state.refuseRemoteDelete);
      if (remote.get(name)?.id !== id) throw new Error(`! [rejected] ${name} (stale info)\nerror: failed to push some refs`);
      remote.delete(name);
    }),
  };

  // Object.defineProperties keeps the getters live; Object.assign would read them once.
  return Object.defineProperties(git, {
    /** Tags now here and on origin, as plain objects of name -> commit id. */
    snapshot: { value: () => ({ local: Object.fromEntries([...local].map(([name, entry]) => [name, entry.commit])), remote: Object.fromEntries([...remote].map(([name, entry]) => [name, entry.commit])) }) },
    isAnnotated: { value: (name) => annotated.has(name) },
    /** Origin's own object id of a tag, as the guarded delete must name it. */
    remoteTagId: { value: (name) => remote.get(name)?.id },
    /** The same for the tag here. */
    localTagId: { value: (name) => local.get(name)?.id },
    /** Somebody else's change on origin, or here, after the script's earlier reads. */
    setRemoteTag: { value: (name, value) => remote.set(name, remoteEntry(name, value)) },
    removeRemoteTag: { value: (name) => remote.delete(name) },
    setLocalTag: { value: (name, value) => { const entry = remoteEntry(name, value); objects.set(entry.id, entry); local.set(name, entry); } },
    /** Changes a fake setting, such as refuseLocalDelete, between two runs. */
    configure: { value: (changes) => Object.assign(state, changes) },
    fetched: { get: () => fetched },
    /** The branch names each fetch was given. */
    fetchedBranches: { get: () => [...fetchedBranches] },
    /** Names of the port methods called so far; only for promises such as "runs no git". */
    calls: { get: () => [...calls] },
  });
}
