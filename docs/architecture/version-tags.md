# How a version tag is made and cleared

Status: Decided

A script, `.agents/version-tag.mjs`, previews the next version tag on your PC, and makes and pushes it only in a [release run](releasing.md#a-release-run-plans-tests-tags-then-publishes) on GitHub. It also clears a version tag, and gives the release run a tag's version and its notes from the [changelog](../contributing/changelog.md). The `version-tag` skill clears a tag from Codex, Claude Code, or GitHub Copilot, after you approve its preview. To release a version, use the [release command](releasing.md).

## Behavior

The script checks a tag and its commit before anything reaches GitHub, so a release never tags the wrong commit or a mistyped tag. It's a Node.js script beside [generate.mjs](../../.agents/generate.mjs) in `.agents/`. It belongs to no [module](modules.md), since it changes tags, not the apps.

### The script holds the version tag rule, and the release run asks it

The script is the only code that knows which tags are version tags, such as `v1.2.3`; [the workflows' section](technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) states the rule in words. The script ignores every other tag, such as `v1.2` or `proof-1`. The user decided:

- **`--check <tag>` prints the tag's version, such as `1.2.3`, or fails.** It runs no git, so it needs nothing but the tag.
- **The release run's Publish job checks out the tag first, then runs `node .agents/version-tag.mjs --check <tag>`**, and takes the version from what it prints. The workflow keeps no copy of the rule.
- **The script runs on the Node.js the runner image includes, with no setup step**: Node.js 22 on `windows-2025` today ([Windows Server 2025 image](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)). So it uses only Node.js's own modules, and no feature newer than Node.js 22 or still experimental there. Review checks this, since the tests run on your PC's Node.js.

### Making a tag raises one part of the latest version

You name the part to raise, `major`, `minor`, or `patch`; with none, it's `minor`. The script raises that part of the latest version tag. On your PC it only previews the new tag; a release run [makes and pushes it](#on-github-the-script-tags-the-commit-its-given):

| Latest version tag | `major` | `minor` | `patch` |
| --- | --- | --- | --- |
| None yet | `v1.0.0` | `v1.0.0` | `v1.0.0` |
| `v1.2.3` | `v2.0.0` | `v1.3.0` | `v1.2.4` |

- **The latest is the highest version by number**, so `v0.10.0` comes after `v0.9.0`. Version tags here and on origin both count.
- **Each part is a whole number of any size, kept exact.** The script never rounds one: after `v1.2.9007199254740993`, `patch` makes `v1.2.9007199254740994`, and `--check` prints every digit.
- **The tag goes on a commit of origin's default branch, `main`**: the newest one in a preview on your PC, and the tested one in a release run, even when the branch moved on during the tests. The user decided the tested commit on November 23, 2026.

### Each app has its own tags and notes

The [Android release split](android-releases.md) extends this script; it is not implemented yet. Desktop keeps `v<major>.<minor>.<patch>`. Android uses `android-v<major>.<minor>.<patch>`, with its own highest version and the same bump rules. Neither app's preview, consistency checks, or tag creation depends on the other's tags. Android's numeric parts must also fit the [version-code bounds](technology-stack.md#the-android-tag-determines-the-apks-version-code).

Desktop notes stay in `CHANGELOG.md`; Android notes live in a separate `CHANGELOG.Android.md`, added with Android publication. The script reads only the selected app's notes on the tested commit. Both files follow the [same changelog rules](../contributing/changelog.md), without another parser.

The script gains an explicit platform selector for previews and creation. Existing calls default to desktop. Checking a tag, reading its notes, and clearing it must support both tag families without changing the other platform.

### Checks run before a tag is made, and a failed check changes nothing

The script fetches origin's default branch, then runs these checks. When one fails, it names the problem and makes no tag. The first three are about your clone, so a release run, which names its commit with [`--commit`](#on-github-the-script-tags-the-commit-its-given), checks that commit instead:

| Check | Fails when | With `--commit` |
| --- | --- | --- |
| Branch | You aren't on origin's default branch, or on no branch | Skipped |
| Working tree | It has changes that aren't committed | Skipped |
| In step with origin | Your default branch is behind origin's, ahead of it, or both | Skipped |
| Commit on the default branch | The given commit isn't on origin's default branch, as [`git merge-base --is-ancestor`](https://git-scm.com/docs/git-merge-base#Documentation/git-merge-base.txt---is-ancestor) checks | Only here |

The version tag and [changelog](#a-version-is-tagged-only-when-the-changelog-has-its-notes) checks below run in both cases.

No git setting widens what the script changes. Its fetch names only origin's default branch, with tag following and pruning off, and each push changes only the one tag it names, with tag following off. So settings such as [`fetch.pruneTags`](https://git-scm.com/docs/git-config#Documentation/git-config.txt-fetchpruneTags) or [`push.followTags`](https://git-scm.com/docs/git-config#Documentation/git-config.txt-pushfollowTags) can't make it fetch, delete, or push any other tag or branch.

The user decided that **the push that makes a tag needs no guard**. Git refuses it when origin already has a tag of that name, such as one someone pushed after the check ([push rules](https://git-scm.com/docs/git-push#_push_rules)).

Proposed, each a decision to confirm:

- **A clean working tree is one [`git status`](https://git-scm.com/docs/git-status) calls clean**: no staged, unstaged, or untracked files. Ignored files don't count. As proposed, the script asks for every untracked file and every submodule change, so git settings such as `status.showUntrackedFiles=no` can't hide a change.
- **The script never reads git's wording to decide what happened**, as proposed. After a refused delete, it reads the tags again instead, so it works whatever language git uses.
- **The script asks origin for its default branch** ([`git ls-remote --symref`](https://git-scm.com/docs/git-ls-remote)) instead of naming `main`, so GitHub's setting stays the name's one home.
- **It refuses when a version tag here is missing on origin, or points to another commit there**, and names each one. Such a tag makes "latest" unclear, and a later `git push --tags` would release it.
- **It reads origin's tags without fetching them**, so a check never changes a local tag. A version tag only on origin still counts.
- **The tag is annotated**, as [`git tag`](https://git-scm.com/docs/git-tag) recommends for releases.
- **When the push fails, the script deletes the tag it just made**, so a rerun starts clean.
- **The script pushes and rolls back the exact tag it made**, by the ID git gave the new tag, so a tag moved here in the meantime is never pushed or deleted.

### A version is tagged only when the changelog has its notes

Every release carries notes for the people who use that app, taken from its version's section of the [platform's changelog](#each-app-has-its-own-tags-and-notes), which the [changelog guide](../contributing/changelog.md) shapes. The checks below describe today's desktop command; the Android extension applies them to its own file. The changelog is each app's notes' one home.

Proposed, each a decision to confirm:

- **The script holds the rule for finding a version's notes**, and the release run's Publish job asks it, as it does for the version. So the rule has one home.
- **Making a tag runs one more check, after the ones above**: the changelog. It reads `CHANGELOG.md` as committed on the commit it would tag, not your working tree, since that commit is what the release builds.
- **The check fails when the new version's notes aren't ready**, and names the version and the fix. For a new version `1.3.0`:

  | Fails when, on the commit to tag | The message tells you to |
  | --- | --- |
  | `## Unreleased` has entries, and there's no `## 1.3.0` | Rename `## Unreleased` to `## 1.3.0`, commit, and push |
  | There's no `## 1.3.0` and nothing under `## Unreleased`, or no `CHANGELOG.md` | Add `## 1.3.0` with what changed for users, or one line saying nothing users notice changed, then commit and push |
  | `## 1.3.0` has no text, or appears twice | Write its entries in one `## 1.3.0`, then commit and push |
  | `## 1.3.0` is there, and `## Unreleased` still has entries | Move those entries under `## 1.3.0`, since the tagged commit includes their changes, then commit and push |

- **A preview runs the check too**, so `--dry-run` shows the version to write before you change anything.
- **`--notes <tag>` prints the tag's version's notes**, the text of its section without the heading, for the Publish job. It reads `CHANGELOG.md` in the repository the script sits in, as checked out, and runs no git. It fails as the check above does, except that entries under `## Unreleased` don't fail it, since work after a release mustn't break the notes of a version already tagged.
- **The Publish job runs `--notes` right after `--check`, before building**, writes what it prints to a file, and passes that file to `vpk pack --releaseNotes` ([release notes](https://docs.velopack.io/integrating/release-notes)).
- **Clearing a tag checks nothing in the changelog**, and changes nothing there. A version cleared and made again keeps its section.

### Clearing deletes one version tag, here and on origin

`--clear` deletes the latest version tag, or the one you name, both here and on origin. It doesn't delete the GitHub release made from that tag; delete that on GitHub yourself.

The user decided that **a named tag for `--clear` accepts an optional lowercase `v` and an omitted patch number**.
The [script](../../.agents/version-tag.mjs) fills in the prefix and a missing patch as zero, so `1.0` means `v1.0.0`.
It keeps every digit exact and rejects malformed versions before running git.
Other commands and the rule for recognizing existing version tags stay strict.

The **clear a version tag** task in [VS Code](../../.vscode/tasks.json) asks for the tag and runs the script.
The user chose **immediate deletion, without a preview or confirmation prompt**.
The task runs Node directly, not through a shell, and uses the script's normalization rather than copying it.
An empty answer fails; it never falls back to clearing the latest tag.

The user decided: **the delete on origin is guarded, so it never deletes a tag that changed after the check.** The script runs `git push --force-with-lease=refs/tags/<tag>:<ID> origin :refs/tags/<tag>`, where `<ID>` is the tag's own ID as origin listed it for the run's check. Git deletes the tag only if origin's tag still has that ID ([`--force-with-lease`](https://git-scm.com/docs/git-push#Documentation/git-push.txt---force-with-leaserefnameexpect)). When the tag changed or is gone, git refuses, and the script fails without deleting the tag here. To tell this refusal from others whatever git's language, the script lists origin's tags again after any refused delete: if the tag changed or is gone, it says the tag changed on origin; otherwise it shows git's message.

For an annotated tag, origin lists two IDs: the tag's own, and its commit's. The checks, the preview, and `--origin` compare commits. The guard must use the tag's own ID, since git compares the guard with that, and refuses every annotated tag given its commit's ID. So the script keeps both IDs from the one listing of origin's tags its check reads. For a lightweight tag, the two are the same.

Proposed, each a decision to confirm:

- **It deletes the tag wherever it is**, including one that's only here or only on origin, such as one a failed push left.
- **It deletes the tag on origin first, then here**, so when origin refuses, whether the guard failed or for another reason, no tag has changed.
- **The delete here is guarded too**, as proposed: the script deletes the tag here only if it still has the ID the check read ([`git update-ref -d`](https://git-scm.com/docs/git-update-ref)). If it changed, the script says so and keeps it; clearing again deletes it. If it's already gone, such as when a git setting made the push to origin delete it too, the clear still succeeds.
- **A failed guard gets its own message**: it names the tag, says the tag changed on origin since the check and no tag was deleted, and says to preview again.
- **It refuses a name that isn't a version tag, or that's in neither place**, and `--clear` alone when there's no version tag.
- **It checks nothing about your branch or working tree**, since deleting a tag doesn't depend on them.

### The approved run does only what the preview showed

`--dry-run` runs every check, prints the plan, and ends with the command that does exactly that plan. That command names the tag and its commit, and refuses, changing no tag, when they no longer match.

The user decided: **the approved run must match the preview.** It names the exact tag and commit, and the script refuses if anything changed. So when a `--clear` preview shows `v1.0.0` and someone then pushes `v2.0.0`, the approved run deletes `v1.0.0` or refuses; it never deletes `v2.0.0`.

Proposed, each a decision to confirm:

- **A preview fetches but changes no tag.** For a new tag, it shows the tag and its commit's short ID and subject; for clearing, the tag, its commit, and where it is.
- **Its last line is the command, alone, with every value written out**: the part, the tag, and full commit IDs, as git prints them, so the comparison is exact. For example:

  ```text
  A release run would make v1.3.0 on 3f9c2d4 Fix the build and push it to origin.
  node .agents/version-tag.mjs minor --expect v1.3.0 --commit 3f9c2d41a7be5c0e9d8f6a1b2c3d4e5f60718293
  ```

  For a new tag, that line is the command a release run's Tag job runs, and the release command passes its tag and commit to the run.
- **`--expect <tag> --commit <ID>` makes the tag only if the part still gives that tag, on that commit.** The run works out the next tag as usual, then compares.
- **`--clear <tag>` with `--here <ID>`, `--origin <ID>`, or both deletes the tag only if it's in exactly those places, on those commits.** The preview writes one option for each place it found the tag.
- **"Anything changed" means a change to what the run would do**: the tag, its commit, or the places a cleared tag is in. A run that still does exactly what you approved goes ahead, such as after someone pushes a lower version tag.
- **A clear without these options still works**, for you at a terminal. With no preview, there's no earlier approval for it to drift from. The script can't tell you from an AI tool, so the [skill](#the-skill-clears-a-tag-after-you-approve-its-preview) keeps an AI tool's runs exact.
- **When the commit a tag points to isn't here**, such as one only a tag on origin reaches, a clear preview shows the commit's full ID without its subject, and says it isn't here.

### On GitHub, the script tags the commit it's given

The user decided on November 23, 2026, that only a release run makes a tag, on the commit its tests ran on:

- **`node .agents/version-tag.mjs [<part>] --commit <ID>` makes the next tag of that part on exactly that commit**, and `--expect <tag>` and `--dry-run` are optional. It refuses unless the commit is on origin's default branch, and skips the checks about your clone, as [the checks table](#checks-run-before-a-tag-is-made-and-a-failed-check-changes-nothing) shows. It keeps the rest: version tags in step with origin, the changelog notes at that commit, and the push that rolls back on failure. A release run's Plan job runs it with `--dry-run`, and its Tag job without.
- **Without `--commit`, making a tag only previews.** The preview runs every check on your clone and ends with the command the Tag job runs, [above](#the-approved-run-does-only-what-the-preview-showed). Without `--commit` and without `--dry-run`, the script refuses and names the [release command](releasing.md).
- **A tag pushed any other way publishes nothing**, since no workflow starts on a tag, and the next release counts past it.

### One command previews, makes, clears, or checks a tag

The release run's [decided use](#the-script-holds-the-version-tag-rule-and-the-release-run-asks-it) fixes the script's name and `--check`. Proposed, each a decision to confirm: the other commands the table lists, and the rules below it. Run each command from the repository root. A clear command without the [approval options](#the-approved-run-does-only-what-the-preview-showed) also takes `--dry-run`:

| Command | What it does |
| --- | --- |
| `node .agents/version-tag.mjs --dry-run` | Previews the next minor version on your clone; `major`, `minor`, or `patch` before `--dry-run` names the part |
| `node .agents/version-tag.mjs`, with or without a part | Refuses, and names the release command |
| `node .agents/version-tag.mjs minor --expect v1.3.0 --commit <ID>` | Makes and pushes `v1.3.0` on that commit, or refuses if the next minor version changed or the commit isn't on the default branch. With `--dry-run`, only checks. |
| `node .agents/version-tag.mjs --clear` | Deletes the latest version tag here and on origin |
| `node .agents/version-tag.mjs --clear v1.2.3` | Deletes `v1.2.3` here and on origin |
| `node .agents/version-tag.mjs --clear v1.2.3 --here <ID> --origin <ID>` | Deletes `v1.2.3` here and on origin, or refuses unless it's in exactly those places, on those commits |
| `node .agents/version-tag.mjs --check v1.2.3` | Prints `1.2.3`, and fails for a tag that isn't a version tag |
| `node .agents/version-tag.mjs --notes v1.2.3` | Prints the notes of `1.2.3` from `CHANGELOG.md`, and fails when they [aren't ready](#a-version-is-tagged-only-when-the-changelog-has-its-notes) |

- **The script works on the repository it sits in**, whatever the current folder, so it can't tag another repository by mistake.
- **Exit code 0 means it did the work, or printed a preview.** It prints each thing it did.
- **Exit code 1 means it refused or failed.** It prints one line to the error output, naming the problem and the fix. A refusal changes no tag; [Failure modes](#failure-modes) say what a failed push or deletion leaves.
- **Wrong arguments fail before the script runs git**: an unknown, repeated, or conflicting one, or a missing or malformed value. Examples are two parts, a part with `--clear`, `--check` or `--notes` with anything but one tag, `--expect` without `--commit`, `--here` without a tag to clear, `--dry-run` with `--here` or `--origin`, and a short commit ID.

### The skill clears a tag after you approve its preview

The `version-tag` skill sits in `.agents/skills/version-tag/SKILL.md`, as [AGENTS.md](../../AGENTS.md#ai-instructions-and-agents) places skills, and each AI tool finds it as [How each AI tool finds a skill](agent-skills.md) says. An AI tool uses it when you ask to clear a tag, or to release a version:

1. It picks `--clear` with the tag you name, or the latest when you name none.
2. It runs that command with `--dry-run` and shows you the plan.
3. After you say yes, it runs the command on the preview's last line, exactly as printed, with no argument added, dropped, or changed. Anything but yes stops it.
4. When a check fails, or the approved run refuses because something changed, it shows the script's message and stops. To try again, ask again; it starts with a new preview.
5. Asked to release a version, it makes no tag. It points to [Release a version](releasing.md#start-a-release-or-an-extra-test-run-from-github).

The user decided steps 2 and 3, which [AGENTS.md](../../AGENTS.md#contributions) names as the one exception to "AI never pushes", and on November 23, 2026, that the skill makes no tag. Proposed, each a decision to confirm: the skill's name, steps 1 and 4, and a file with only the two fields the [Agent Skills standard](https://agentskills.io/specification) requires, `name` and `description`, so every AI tool reads it the same way. No settings file approves the script, unlike `repo-map.mjs`, so your AI tool also asks before it runs, unless you've turned its approvals off.

### Unit tests replace git, and integration tests use temporary repositories

Proposed, each a decision to confirm:

- **The script reaches git, and reads `CHANGELOG.md`, only through a few operations of its own**, so tests can replace them. One of them asks whether a commit is on origin's default branch, for `--commit`.
- **Unit tests check every rule, `--check`, `--notes`, the changelog check, and `--commit` included, against an in-memory fake of those operations**, with no real git, files, or network.
- **A few integration tests run the script on real git.** Each uses its own temporary repository, with a local bare repository as origin, so no test touches this repository or GitHub. They check what the fake can't: a tag made and pushed with `--commit`, from a clone that's on another branch with uncommitted changes; a commit that isn't on the default branch, refused; a tag cleared in both places; a fetch that finds a new commit; ignored files in a clean tree; and what `--check` and `--notes` print and their exit codes. They also check that a preview's last line, run unchanged, does what the preview showed; that git settings which fetch, prune, or push other tags change nothing; and that a tag whose commit only origin has can be previewed and cleared. And they check that the guarded delete on origin removes an annotated tag for the ID origin lists for it, and refuses, deleting nothing, for any other ID, such as the tag's commit.
- **The tests sit in `.agents/version-tag.test.mjs` and run as [AGENTS.md](../../AGENTS.md#checking-the-agents-scripts) says.** The workflows don't run them.

## Public interface

None for modules; no module uses the script. The skill, the [README](../../README.md), the [VS Code task](../../.vscode/tasks.json), the [release command](releasing.md), and the release run's Plan, Tag, and Publish jobs use its [command line](#one-command-previews-makes-clears-or-checks-a-tag). The skill and the release command also read a preview's last line, the [command that does the plan](#the-approved-run-does-only-what-the-preview-showed). The Publish job reads what `--check` prints. The user decided that it prints the version or fails; proposed, a decision to confirm, is its exact form:

| Tag | Standard output | Error output | Exit code |
| --- | --- | --- | --- |
| A version tag, such as `v1.2.3` | The version alone on one line: `1.2.3` | Nothing | 0 |
| Any other tag | Nothing | One line naming the tag and the form it needs | 1 |

The Publish job also reads what `--notes` prints. Proposed, a decision to confirm:

| Tag | Standard output | Error output | Exit code |
| --- | --- | --- | --- |
| A version tag whose notes are ready | The text under its `## <version>` heading, without the heading, trimmed of blank lines at its start and end | Nothing | 0 |
| A version tag whose notes [aren't ready](#a-version-is-tagged-only-when-the-changelog-has-its-notes) | Nothing | One line naming the problem and the fix | 1 |
| Any other tag | Nothing | One line naming the tag and the form it needs | 1 |

## Failure modes

- **A check fails**, such as uncommitted changes or a version with no notes in the changelog: the script names the problem and changes no tag. Fix it, then run it again.
- **Something changed between a preview and your yes**, such as a new version tag or a new commit on origin: the approved run names what changed and changes no tag. Preview again.
- **The tag on origin changed between the run's check and its delete**, such as one pushed again or deleted from another clone: git refuses the [guarded delete](#clearing-deletes-one-version-tag-here-and-on-origin), and the script says so and changes no tag. Preview again.
- **Origin refuses the push, or can't be reached**: the script shows git's message and, as proposed, deletes the tag it made here. The release run's Tag job fails, and nothing is published.
- **Origin refuses to delete a tag**: the script shows git's message and keeps the tag here. GitHub refuses when immutable releases are on and the tag's release exists ([managing releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)).
- **The tag here can't be deleted after origin's was**, or it changed after the check: the script shows why. The tag is now only here, and clearing it again deletes it.
- **A tag is cleared while its release run publishes**: not confirmed what the run then publishes. Cancel the run on GitHub first.
- **A cleared version is made again while its GitHub release exists**: not confirmed whether the release run can publish it. Delete the old release first.
- **The script uses a Node.js feature the runner image lacks**: the release run's Plan job fails before any test, and nothing is tagged. Fix the script on the default branch, then start a new release.
- **A version tag is pushed another way, such as with `git tag`**: nothing is published, and the next release counts past it. [Clear](#clearing-deletes-one-version-tag-here-and-on-origin) it.

## Ruled out

### Making and clearing a tag

- **Another language, a tag that isn't pushed, or a clear that also deletes the GitHub release**: you chose otherwise.
- **A first tag that depends on the part, or starts below the first major version**: you chose the same first version for every part, as the table above states.
- **Fetching origin's tags**: a preview would change local tags, and a clashing tag would fail with git's message instead of one that names the fix.
- **Naming `main` in the script**: a second home for GitHub's default-branch setting.
- **Lightweight tags**: git keeps them for private or temporary labels.
- **Refusing a commit that already has a version tag**: it would block giving a tested commit a new version.
- **Using the current folder's repository, as `repo-map.mjs` does**: run from another repository, it would tag that one.
- **An unguarded delete on origin**: a tag someone pushed again after the check would be deleted unseen. You chose the guard.
- **Deleting the tag here first**: when origin then refused, the tag would be left only on origin.
- **A preview-only task or an extra confirmation prompt**: you chose immediate deletion after entering the tag.
- **Normalizing tags only in the task**: the script owns tag handling; terminal commands must accept the same clear input.

### Approving a preview

- **Recomputing the plan at run time**, by running the previewed command again without `--dry-run`: a tag pushed or a commit added between the preview and your yes would change what the run does. You chose a run that names what you approved.
- **One option for the tag and its commit, such as `--expect v1.3.0@<ID>`**: harder to read, and a clear needs one option per place anyway.
- **Requiring the approval options on every clear**: you'd need two commands at a terminal too, and it wouldn't stop an AI tool that ignores the skill, since that tool could run git itself.
- **Refusing when any version tag changed**: it would stop a run that still does exactly what you approved.
- **Fetching a commit only origin has, so a preview can show its subject**: a second download, for a rare case.

### Running and testing the script

- **The skill handing you the command instead of running it**: you chose to let it run the command after your yes.
- **The skill makes tags, or starts the release run**: a tag made by hand publishes nothing, and starting a run that publishes would widen AI's exception to "never push". [Release a version](releasing.md#ruled-out) lists the other options for making a tag on the runner.
- **Making a tag on your PC without `--commit` still pushes it**: the tag would publish nothing, and the next release would count past it.
- **Two copies of the version tag rule, the job's and the script's, or one pattern file both read**: you chose the script as the rule's one home.
- **Installing another Node.js in the release run**: a step every release runs, when the script can use the runner image's own.
- **Only integration tests, as the generator's agent files have**: [AGENTS.md](../../AGENTS.md#test-design) asks unit tests to cover every rule.

### Checking the changelog

- **Only the Publish job checks the notes**: a missing section would show only after the tag is pushed, and you'd clear it and release again.
- **The script renames `## Unreleased` itself when it makes the tag**: the rename would need a commit, and only you commit.
- **Reading the notes from the working tree when making a tag**: notes you haven't committed wouldn't reach the release.
- **Refusing a version with no changes users notice**: a release that only fixes the build couldn't ship. One line saying nothing users notice changed is enough.
- **Allowing an empty section**: What is new would show "No notes for this version.", which tells users nothing.
- **Extending `--check` to print the notes too**: its output is decided as the version alone, and the job needs the two separately.
