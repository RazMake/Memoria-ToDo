# How you release a version with one command

Status: Decided

Run `node .agents\release.mjs`, or choose **Tasks: Run Task > release a version** in VS Code. The [release command](../../.agents/release.mjs) checks the code, publishes after the workflow's manual run passes every test on the commit it tags, follows GitHub Actions, downloads the installer, and guides your manual checks.

## Behavior

### The command uses the existing version and release rules

The command is repository tooling, not an app module. It needs Node.js, git, and GitHub CLI (`gh`), signed in with access to origin. It accepts a version part or asks for one. The parts and default come from the [version tag script](../../.agents/version-tag.mjs), not a second list.

The [version tag rules](version-tags.md) own choosing the version, checking committed release notes, previewing a tag, and pushing exactly the approved tag and commit. The [workflow](../../.github/workflows/build.yml) owns building, testing, packaging, and publishing the app. This command coordinates them; it copies neither rule.

### Manual preparation stays with you

Before any publishing, the command checks GitHub access and runs the Node tooling tests. A preview then checks the repository and committed changelog.

After a passing preview, the working tree is clean at the commit the tag will name. The command runs no .NET tests on your PC: the full suite runs [only in the cloud](test-selection.md#the-full-suite-runs-only-in-the-cloud), in the workflow's manual run, [below](#publishing-needs-a-manual-run-that-passed-every-test-on-the-tagged-commit). You decided this on November 22, 2026. The manual run covers every test kind on the same runner image the release uses.

When a preview refuses, the command shows its error. You fix the named problem in another terminal, including any changelog edit, commit, or branch push. Enter retries the preview; any other answer cancels. The command never edits files, commits, pulls, switches branches, stashes, discards changes, or pushes a branch.

It works on the repository beside the script, regardless of the current directory. Origin must fetch from and push to one GitHub repository. A different push destination fails before GitHub access or tagging.

The command checks origin again after your publishing confirmation. The tag push uses the validated endpoint explicitly, so a later git configuration change cannot redirect it to another repository.

### Publishing needs a manual run that passed every test on the tagged commit

You decided that the command starts the [workflow's](../../.github/workflows/build.yml) manual run, `workflow_dispatch`, on the commit it will tag, and tags only after that run passes. The version tag's own run then [reuses that pass](technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) and runs no test again. A manual run counts only when it ran on the previewed commit itself; a run of another commit never counts.

What happens next depends on the newest manual run of that commit when the command first looks:

| Newest manual run of the previewed commit | What the command does |
| --- | --- |
| Finished and passed | Says every test already passed on this commit, links the run, then publishes without asking. |
| Queued or running | Waits for it. If it passes, asks you to type exactly `yes`; any other answer cancels without making a tag. |
| None | Starts one on the default branch, then checks that it runs the previewed commit. If it does, waits for it, and on a pass asks for `yes` as above. |
| Finished and failed, or ended any other way | Stops with an error and the run's link. Nothing is tagged, and no new run starts, since running a failure again until it passes is [never allowed](../../AGENTS.md#run-timing). |

You decided earlier that a check which passed before you started is approval enough, but a check you waited for still needs your yes. Proposed, a decision to confirm: that rule now applies to the manual run, which replaces the branch build as the command's gate, since it runs the branch build's unit tests too. Either way, the command runs the preview's guarded command arguments unchanged, without a shell. A changed plan refuses and stops; another attempt needs a new preview and, when asked, a new confirmation.

Proposed, each a decision to confirm:

- **The command starts the run through GitHub's API and asks for the new run's details** ([run IDs on dispatch](https://github.blog/changelog/2026-02-19-workflow-dispatch-api-now-returns-run-ids/)), so it follows the run it started, never a guess among recent runs.
- **A run that started on another commit stops the command**, since someone pushed to the default branch after the preview. It says so, links the run, and tags nothing; preview again. It leaves that run alone, since a test run publishes nothing.
- **One shared check decides which run proves a commit passed every test**: a run of the workflow file, started by `workflow_dispatch`, on that exact commit, whose newest attempt passed, including its `full-tests` job. The command and the release job both use it, so the rule has one home. It lives in a Node.js script in `.agents/`, which the release job runs as it runs the [version tag script](version-tags.md#the-script-holds-the-version-tag-rule-and-the-release-job-asks-it), with the same Node.js limits.

Proposed, a decision to confirm: the command requires an interactive terminal. Redirected input cannot supply release approval. AI tools keep using the [version-tag skill](version-tags.md#the-skill-previews-asks-you-then-runs-the-script).

### The command follows the new tag's workflow, not an older build

The command selects the existing [workflow file](../../.github/workflows/build.yml) explicitly. It matches the tag and exact commit, excluding run IDs that existed before the tag push. It rejects ambiguous matches. Manual runs of a commit use the newest matching run. The tag's run checks for the manual run's pass, then builds and publishes, without running tests.

Proposed, a decision to confirm: waiting for a workflow to appear and waiting for commands to finish are bounded. The limits live in the [command](../../.agents/release.mjs). An expired wait fails with an Actions link; it never silently accepts a missing or unfinished check.

Git operations use the same bounded, cancellable executor as GitHub CLI. If a push is interrupted, its outcome may be uncertain; the command prints recovery links instead of claiming nothing reached GitHub.

After the tag's workflow passes, the command checks that the release is published, is not a prerelease, and contains exactly one nonempty Windows setup executable.

### Questions and GitHub waits stand out

Every question the command asks you appears in yellow. Before each wait on GitHub, such as checking sign-in, finding or watching a workflow, or fetching the release, one cyan message names what it waits for. Proposed, a decision to confirm: these two colors, and plain text with no color codes when the terminal can't show color or `NO_COLOR` is set. The command uses Node.js's own [terminal color support](https://nodejs.org/api/util.html#utilstyletextformat-text-options), so it adds no dependency.

### Installation and the smoke check stay manual

Proposed, a decision to confirm: the command downloads the installer to its own new temporary folder, rather than only linking to the release. It prints the path and never launches the executable.

It warns about the [unsigned installer](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), then guides installation and a short check of the installed app. Success means GitHub published the release and you confirmed the manual checks. It does not mean the script observed the installed app.

### Tests check the guide without publishing anything

The [tests](../../.agents/release.test.mjs) use the version tag script's [shared fake git](../../.agents/version-tag.fake-git.mjs) for unit tests. They check approval, manual preparation, each row of the manual run table, a started run on another commit, failures, workflow selection, and release validation through the command's interfaces. The shared run check's own unit tests give it made-up run and job lists: a pass, another commit, another event, a failed or unfinished attempt, a missing `full-tests` job, and no run at all.

Integration tests run local child processes and isolated downloads with a fake GitHub command. They never call real GitHub or launch an installer. Real Actions, VS Code's terminal, and installation still need a release-time check.

## Public interface

No app module uses this command. Developers run `node .agents\release.mjs [<part>]`; `--help` describes it. VS Code's [release a version task](../../.vscode/tasks.json) starts it in a focused terminal.

Exit code zero means either an explicitly reported cancellation before tagging, or a published release whose manual checks you confirmed. Errors, an interrupted command, and unconfirmed manual checks exit nonzero.

## Failure modes

- **Missing tools or GitHub sign-in:** the command names the failed operation and tells you to install GitHub CLI or run `gh auth login`.
- **Repository or changelog not ready:** the preview names the fix; you may fix it and retry or cancel.
- **Checks fail before tagging:** nothing is tagged. Fix the failure, commit and push, then run the command again.
- **The default branch moves between the preview and the manual run's start:** the started run tests another commit, so the command stops and tags nothing. Preview again.
- **A version tag pushed without the command, such as with the [version-tag skill](version-tags.md#the-skill-previews-asks-you-then-runs-the-script) or `git tag`, has no passing manual run:** the release job fails before building and publishes nothing, as [the workflow's section](technology-stack.md#a-github-actions-workflow-checks-each-push-and-publishes-each-version-tag) says. Start the manual run on the tag, with `gh workflow run build.yml --ref <tag>`, wait for it to pass, then re-run the failed release job; the tag stays.
- **The approved plan changes:** the guarded tag command refuses. The guide stops instead of approving another plan for you.
- **A failure after tagging:** the command leaves the tag and any release intact and prints recovery links. Inspect them before retrying. Prefer fixing forward; clearing a tag and deleting a release remain separate, deliberate operations.
- **Manual checks fail or remain unconfirmed:** the release may already be public. The command reports failure rather than claiming the installer works.

## Ruled out

- **Automatic commits, branch changes, or changelog edits:** release preparation belongs to the developer.
- **Automatic installation or deletion of a published release:** these need separate human judgment.
- **Publishing without your yes after waiting for the tests:** you chose to skip the question only for a check that had already passed.
- **Skipping the manual installer checks:** the command can't see the installed app.
- **Selecting the latest Actions run:** it could belong to another commit, branch, or an earlier use of the tag.
- **Every test kind on each push to the default branch, so the runner checks them before tagging:** each push would wait for the full suite; you chose one manual run per release.
- **The full suite on your PC before tagging:** you ran it there from November 16 to 22, 2026; the release depended on your PC's desktop.
- **No tests before tagging, with the tag's run testing:** a failure would leave a tag to clear.
- **The tag's run tests again:** you asked that a release never run the same tests twice on one commit.
- **The tag's run trusts the manual run's uploaded files or a signed attestation of them:** more moving parts than asking GitHub for the run's result, and the release builds from source anyway.
- **The tag's run builds and publishes with no check:** a tag pushed without the command would publish untested code.
- **A manual run of another commit with the same files:** proving two commits hold the same files is a second check to get right; the commit is the simple proof.
- **New dependencies or another version parser:** the existing Node.js modules, GitHub CLI, and tag script suffice.

## Open questions

None beyond the proposed decisions to confirm above.
