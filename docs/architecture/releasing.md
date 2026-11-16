# How you release a version with one command

Status: Decided

Run `node .agents\release.mjs`, or choose **Tasks: Run Task > release a version** in VS Code. The [release command](../../.agents/release.mjs) checks the code, publishes after a passing branch build, follows GitHub Actions, downloads the installer, and guides your manual checks.

## Behavior

### The command uses the existing version and release rules

The command is repository tooling, not an app module. It needs Node.js, git, and GitHub CLI (`gh`), signed in with access to origin. It accepts a version part or asks for one. The parts and default come from the [version tag script](../../.agents/version-tag.mjs), not a second list.

The [version tag rules](version-tags.md) own choosing the version, checking committed release notes, previewing a tag, and pushing exactly the approved tag and commit. The [workflow](../../.github/workflows/build.yml) owns building, testing, packaging, and publishing the app. This command coordinates them; it copies neither rule.

### Manual preparation stays with you

Before any publishing, the command checks GitHub access and runs the Node tooling tests. A preview then checks the repository and committed changelog.

After a passing preview, the working tree is clean at the commit the tag will name. The command then runs the [full run of every test kind](technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test) on your PC, the same tests the workflow runs before publishing. A failure stops the command before any tag. You decided this on November 16, 2026, after a release failed on UX tests that only branch builds' unit runs had covered. A pass on your PC can't prove the runner passes too, so tests must not depend on the machine, such as where the mouse pointer rests.

When a preview refuses, the command shows its error. You fix the named problem in another terminal, including any changelog edit, commit, or branch push. Enter retries the preview; any other answer cancels. The command never edits files, commits, pulls, switches branches, stashes, discards changes, or pushes a branch.

It works on the repository beside the script, regardless of the current directory. Origin must fetch from and push to one GitHub repository. A different push destination fails before GitHub access or tagging.

The command checks origin again after your publishing confirmation. The tag push uses the validated endpoint explicitly, so a later git configuration change cannot redirect it to another repository.

### Publishing needs a passing branch build, and your yes unless it had already passed

The command follows the workflow for the previewed commit on origin's default branch. It does not accept a passing build of another commit.

What happens next depends on that workflow's state when the command first finds it:

| Branch workflow when first found | What the command does |
| --- | --- |
| Finished and passed | Says the branch checks already passed, then publishes without asking. |
| Queued or running | Waits for it. If it passes, asks you to type exactly `yes`; any other answer cancels without making a tag. |
| Finished and failed, or ended any other way | Stops with an error. Nothing is tagged. |

You decided this: a build that passed before you started is approval enough, but a build you waited for still needs your yes. Either way, the command runs the preview's guarded command arguments unchanged, without a shell. A changed plan refuses and stops; another attempt needs a new preview and, when asked, a new confirmation.

Proposed, a decision to confirm: the command requires an interactive terminal. Redirected input cannot supply release approval. AI tools keep using the [version-tag skill](version-tags.md#the-skill-previews-asks-you-then-runs-the-script).

### The command follows the new tag's workflow, not an older build

The command selects the existing [workflow file](../../.github/workflows/build.yml) explicitly. It matches the tag and exact commit, excluding run IDs that existed before the tag push. It rejects ambiguous matches. Branch checks use the newest matching run.

Proposed, a decision to confirm: waiting for a workflow to appear and waiting for commands to finish are bounded. The limits live in the [command](../../.agents/release.mjs). An expired wait fails with an Actions link; it never silently accepts a missing or unfinished check.

Git operations use the same bounded, cancellable executor as GitHub CLI. If a push is interrupted, its outcome may be uncertain; the command prints recovery links instead of claiming nothing reached GitHub.

After the tag's workflow passes, the command checks that the release is published, is not a prerelease, and contains exactly one nonempty Windows setup executable.

### Questions and GitHub waits stand out

Every question the command asks you appears in yellow. Before each wait on GitHub, such as checking sign-in, finding or watching a workflow, or fetching the release, one cyan message names what it waits for. Proposed, a decision to confirm: these two colors, and plain text with no color codes when the terminal can't show color or `NO_COLOR` is set. The command uses Node.js's own [terminal color support](https://nodejs.org/api/util.html#utilstyletextformat-text-options), so it adds no dependency.

### Installation and the smoke check stay manual

Proposed, a decision to confirm: the command downloads the installer to its own new temporary folder, rather than only linking to the release. It prints the path and never launches the executable.

It warns about the [unsigned installer](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), then guides installation and a short check of the installed app. Success means GitHub published the release and you confirmed the manual checks. It does not mean the script observed the installed app.

### Tests check the guide without publishing anything

The [tests](../../.agents/release.test.mjs) use the version tag script's [shared fake git](../../.agents/version-tag.fake-git.mjs) for unit tests. They check approval, manual preparation, failures, workflow selection, and release validation through the command's interfaces.

Integration tests run local child processes and isolated downloads with a fake GitHub command. They never call real GitHub or launch an installer. Real Actions, VS Code's terminal, and installation still need a release-time check.

## Public interface

No app module uses this command. Developers run `node .agents\release.mjs [<part>]`; `--help` describes it. VS Code's [release a version task](../../.vscode/tasks.json) starts it in a focused terminal.

Exit code zero means either an explicitly reported cancellation before tagging, or a published release whose manual checks you confirmed. Errors, an interrupted command, and unconfirmed manual checks exit nonzero.

## Failure modes

- **Missing tools or GitHub sign-in:** the command names the failed operation and tells you to install GitHub CLI or run `gh auth login`.
- **Repository or changelog not ready:** the preview names the fix; you may fix it and retry or cancel.
- **Checks fail before tagging:** nothing is tagged. Fix the failure, commit and push, then run the command again.
- **The approved plan changes:** the guarded tag command refuses. The guide stops instead of approving another plan for you.
- **A failure after tagging:** the command leaves the tag and any release intact and prints recovery links. Inspect them before retrying. Prefer fixing forward; clearing a tag and deleting a release remain separate, deliberate operations.
- **Manual checks fail or remain unconfirmed:** the release may already be public. The command reports failure rather than claiming the installer works.

## Ruled out

- **Automatic commits, branch changes, or changelog edits:** release preparation belongs to the developer.
- **Automatic installation or deletion of a published release:** these need separate human judgment.
- **Publishing without your yes after waiting for the branch build:** you chose to skip the question only for a build that had already passed.
- **Skipping the manual installer checks:** the command can't see the installed app.
- **Selecting the latest Actions run:** it could belong to another commit, branch, or an earlier use of the tag.
- **Every test kind on each push to the default branch, so the runner checks them before tagging:** you chose the run on your PC, which adds no workflow time to each push.
- **New dependencies or another version parser:** the existing Node.js modules, GitHub CLI, and tag script suffice.

## Open questions

None beyond the proposed decisions to confirm above.
