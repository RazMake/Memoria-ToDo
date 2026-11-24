// The one GitHub component of the release command, the Run All tests command, and the run check, as
// docs/architecture/releasing.md describes. github-runs.test.mjs defines what each export does.
//
// execute(file, args, { live?, input? }) -> Promise<stdout>: runs `git` or `gh` (or any program) bounded and cancellable.
//   Rejects with "<program> <first two args> failed: <stderr or reason>"; rejects when the signal aborts.
//
// terminal output: { ask(question) -> Promise<string>, write(text), waitForGitHub(text) }
//   a question is yellow and a wait message is cyan; both are plain with NO_COLOR set or without color support.
//
// A run is { databaseId, path, event, headSha, status, conclusion, url }, as GitHub's run JSON reads.
//
// createGitHubRuns(execute, { repository?, clock? }) -> github
//   repository: 'owner/name' to skip connect() (the run check knows its repository); clock: { now(), wait(ms) } for tests.
//   github.connect()                                 -> { name: 'owner/name', url: 'https://github.com/owner/name' }
//        reads origin (`git remote get-url origin`, and `--push --all`), rejects a remote outside github.com or an ambiguous one,
//        then checks `gh auth status --hostname github.com`; a failure names `gh auth login`. It binds the component to that repository.
//   github.dispatch(workflowFile, ref, inputs = {})  -> run
//        `gh api -X POST repos/<repo>/actions/workflows/<file>/dispatches -f ref=<ref> -f inputs[<name>]=<value> ... -F return_run_details=true`,
//        then reads the returned run ID's run. Rejects, naming the actions page, when GitHub returns no run ID; rejects with the
//        run's link, saying the run was started, when reading it fails. Every `gh api` call passes `--hostname github.com`.
//   github.follow(run)                               -> the finished run (any conclusion), polling `repos/<repo>/actions/runs/<id>`;
//        every wait is bounded: when it expires, rejects with a message holding run.url.
//   github.failedJobs(run)                           -> [{ name, url }] of the jobs whose conclusion is failure, cancelled, timed_out, or action_required
//   github.runs(workflowFile, commit, event?)        -> run[], unfiltered beyond the query `head_sha=<commit>` (and `event=<event>`)
//   github.jobs(runId)                               -> [{ name, conclusion, url }] of the newest attempt (`filter=latest`)
import { execFile } from 'node:child_process';
import { basename } from 'node:path';
import { setTimeout } from 'node:timers/promises';
import { styleText } from 'node:util';

// The limits of every wait on GitHub.
const commandTimeoutMs = 120_000;
const liveTimeoutMs = 30 * 60_000;
const pollMs = 10_000;
const followLimitMs = 3 * 60 * 60_000;

const failedConclusions = ['failure', 'cancelled', 'timed_out', 'action_required'];

export const messageOf = (error) => String(error.message).trim().replace(/\s*\n\s*/g, ' ');

/** The message of an error that stopped a command that started `run`, ending with the run's link when the message lacks it. */
export const messageWithRun = (error, run) => {
  const message = messageOf(error);
  return message.includes(run.url) ? message : `${message} The run goes on: ${run.url}`;
};

export function createTerminalOutput(terminal, output, signal) {
  return {
    ask: (question) => terminal.question(styleText('yellow', question, { stream: output }), { signal }),
    write: (text) => output.write(text),
    waitForGitHub: (text) => output.write(styleText('cyan', text, { stream: output })),
  };
}

export function createCommands(cwd, signal) {
  return (file, args, { live = false, input = '' } = {}) => new Promise((resolve, reject) => {
    let outcome;
    const child = execFile(file, args, {
      cwd, encoding: 'utf8', signal,
      timeout: live ? liveTimeoutMs : commandTimeoutMs,
      maxBuffer: 8 * 1024 * 1024,
      env: { ...process.env, GH_PROMPT_DISABLED: '1', GH_PAGER: 'cat', GIT_TERMINAL_PROMPT: '0' },
    }, (error, stdout, stderr) => {
      outcome = { error, stdout, stderr };
    });
    child.once('close', () => {
      if (!outcome) reject(new Error(`${basename(file)} closed without reporting its result.`));
      else if (outcome.error) reject(new Error(`${basename(file)} ${args.slice(0, 2).join(' ')} failed: ${outcome.stderr.trim() || outcome.error.message}`));
      else resolve(outcome.stdout);
    });
    child.stdin.end(input);
    if (live) {
      child.stdout.on('data', (text) => process.stdout.write(text));
      child.stderr.on('data', (text) => process.stderr.write(text));
    }
  });
}

function repositoryOf(address) {
  const text = address.trim();
  const normalized = text.startsWith('git@github.com:') ? `ssh://git@github.com/${text.slice('git@github.com:'.length)}` : text;
  let url;
  try {
    url = new URL(normalized);
  } catch {
    throw new Error('origin must use a GitHub HTTPS or SSH URL on github.com.');
  }
  const segments = url.pathname.replace(/^\//, '').replace(/\.git$/, '').split('/');
  if (url.hostname !== 'github.com' || !['https:', 'ssh:'].includes(url.protocol) || url.port || url.search || url.hash
    || segments.length !== 2 || segments.some((segment) => !/^[a-zA-Z0-9_.-]+$/.test(segment) || segment === '.' || segment === '..')) {
    throw new Error('origin must name one repository on github.com using HTTPS or SSH.');
  }
  return segments.join('/');
}

const invalid = (what) => new Error(`GitHub returned an invalid ${what} response.`);
const isText = (value) => typeof value === 'string';
const isTextOrNull = (value) => value === null || isText(value);

function runOf(raw) {
  if (!Number.isSafeInteger(raw?.id) || !isText(raw.path) || !isText(raw.event) || !isText(raw.head_sha)
    || !isText(raw.status) || !isTextOrNull(raw.conclusion) || !isText(raw.html_url)) throw invalid('workflow run');
  return { databaseId: raw.id, path: raw.path, event: raw.event, headSha: raw.head_sha, status: raw.status, conclusion: raw.conclusion, url: raw.html_url };
}

export function createGitHubRuns(execute, { repository, clock = { now: Date.now, wait: setTimeout } } = {}) {
  let name = repository;
  const target = () => {
    if (!name) throw new Error('Connect to GitHub before using its runs.');
    return name;
  };
  // The one place that calls `gh api`, so every call names its host.
  const api = (args) => execute('gh', ['api', '--hostname', 'github.com', ...args]);
  const get = async (path) => JSON.parse(await api([`repos/${target()}/${path}`]));
  const readRun = async (id) => runOf(await get(`actions/runs/${id}`));

  const github = {
    async connect() {
      const origin = repositoryOf(await execute('git', ['remote', 'get-url', 'origin']));
      const destinations = (await execute('git', ['remote', 'get-url', '--push', '--all', 'origin'])).trim().split(/\r?\n/);
      if (destinations.length !== 1 || repositoryOf(destinations[0]).toLowerCase() !== origin.toLowerCase()) {
        throw new Error('origin must push to exactly the same GitHub repository it fetches.');
      }
      try {
        await execute('gh', ['auth', 'status', '--hostname', 'github.com']);
      } catch (error) {
        throw new Error(`${messageOf(error)} Install GitHub CLI if missing, then run gh auth login and retry.`);
      }
      name = origin;
      return { name, url: `https://github.com/${name}` };
    },
    async dispatch(workflowFile, ref, inputs = {}) {
      const fields = Object.entries(inputs).flatMap(([key, value]) => ['-f', `inputs[${key}]=${value}`]);
      const body = await api(['-X', 'POST', `repos/${target()}/actions/workflows/${workflowFile}/dispatches`, '-f', `ref=${ref}`, ...fields, '-F', 'return_run_details=true']);
      const id = body.trim() ? JSON.parse(body)?.workflow_run_id : undefined;
      if (!Number.isSafeInteger(id)) throw new Error(`GitHub did not say which run it started for ${workflowFile}; inspect https://github.com/${target()}/actions and do not start another blindly.`);
      try {
        return await readRun(id);
      } catch (error) {
        throw new Error(`${messageOf(error)} The run was started and goes on: https://github.com/${target()}/actions/runs/${id}`, { cause: error });
      }
    },
    async follow(run) {
      const deadline = clock.now() + followLimitMs;
      for (;;) {
        const current = await readRun(run.databaseId);
        if (current.status === 'completed') return current;
        if (clock.now() >= deadline) throw new Error(`The run did not finish within ${followLimitMs / 60_000} minutes: ${run.url}`);
        await clock.wait(pollMs);
      }
    },
    async jobs(runId) {
      // filter=latest keeps only the newest attempt, so a re-run replaces the attempt it repeats.
      const body = await get(`actions/runs/${runId}/jobs?filter=latest&per_page=100`);
      if (!Array.isArray(body?.jobs) || body.jobs.some((job) => !isText(job?.name) || !isTextOrNull(job.conclusion))) throw invalid('jobs');
      return body.jobs.map((job) => ({ name: job.name, conclusion: job.conclusion, url: job.html_url }));
    },
    async failedJobs(run) {
      return (await github.jobs(run.databaseId)).filter((job) => failedConclusions.includes(job.conclusion)).map(({ name: jobName, url }) => ({ name: jobName, url }));
    },
    async runs(workflowFile, commit, event) {
      const query = `head_sha=${commit}${event ? `&event=${event}` : ''}&per_page=100`;
      const body = await get(`actions/workflows/${workflowFile}/runs?${query}`);
      if (!Array.isArray(body?.workflow_runs)) throw invalid('workflow runs');
      return body.workflow_runs.map(runOf);
    },
  };
  return github;
}
