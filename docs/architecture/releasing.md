# How you release a version with one command

Status: Decided

Run `node .agents\release.mjs`, or choose **Tasks: Run Task > release a version** in VS Code. The [release command](../../.agents/release.mjs) checks the code, asks before publishing, follows GitHub Actions, downloads the installer, and guides your manual checks.

## Behavior

### The command uses the existing version and release rules

The command is repository tooling, not an app module. It needs Node.js, git, and GitHub CLI (`gh`), signed in with access to origin. It accepts a version part or asks for one. The parts and default come from the [version tag script](../../.agents/version-tag.mjs), not a second list.

The [version tag rules](version-tags.md) own choosing the version, checking committed release notes, previewing a tag, and pushing exactly the approved tag and commit. The [workflow](../../.github/workflows/build.yml) owns building, testing, packaging, and publishing the app. This command coordinates them; it copies neither rule.

### Manual preparation stays with you

Before any publishing, the command checks GitHub access and runs the Node tooling tests. A preview then checks the repository and committed changelog.

When a preview refuses, the command shows its error. You fix the named problem in another terminal, including any changelog edit, commit, or branch push. Enter retries the preview; any other answer cancels. The command never edits files, commits, pulls, switches branches, stashes, discards changes, or pushes a branch.

It works on the repository beside the script, regardless of the current directory. Origin must fetch from and push to one GitHub repository. A different push destination fails before GitHub access or tagging.

The command checks origin again after your publishing confirmation. The tag push uses the validated endpoint explicitly, so a later git configuration change cannot redirect it to another repository.

### Publishing needs a passing branch build and your confirmation

The command follows the workflow for the previewed commit on origin's default branch. It does not accept a passing build of another commit.

After that build passes, the command asks you to type exactly `yes`. Any other answer cancels without making a tag. It runs the preview's guarded command arguments unchanged, without a shell. A changed plan refuses and stops; another attempt needs a new preview and confirmation.

Proposed, a decision to confirm: the command requires an interactive terminal. Redirected input cannot supply release approval. AI tools keep using the [version-tag skill](version-tags.md#the-skill-previews-asks-you-then-runs-the-script).

### The command follows the new tag's workflow, not an older build

The command selects the existing [workflow file](../../.github/workflows/build.yml) explicitly. It matches the tag and exact commit, excluding run IDs that existed before the tag push. It rejects ambiguous matches. Branch checks use the newest matching run.

Proposed, a decision to confirm: waiting for a workflow to appear and waiting for commands to finish are bounded. The limits live in the [command](../../.agents/release.mjs). An expired wait fails with an Actions link; it never silently accepts a missing or unfinished check.

Git operations use the same bounded, cancellable executor as GitHub CLI. If a push is interrupted, its outcome may be uncertain; the command prints recovery links instead of claiming nothing reached GitHub.

After the tag's workflow passes, the command checks that the release is published, is not a prerelease, and contains exactly one nonempty Windows setup executable.

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
- **Selecting the latest Actions run:** it could belong to another commit, branch, or an earlier use of the tag.
- **New dependencies or another version parser:** the existing Node.js modules, GitHub CLI, and tag script suffice.

## Open questions

None beyond the proposed decisions to confirm above.
