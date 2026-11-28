# How you release a version and run platform tests on GitHub

Status: Decided

To release desktop, open **Actions** and start **Test then Tag and Release** on `main`. It tests the commit or reuses its passed desktop run, then tags and publishes. [Publish an Android APK](publishing-android.md) starts Android's separate pipeline. In VS Code, **Tasks: Run Task > open GitHub Actions** opens that page.

## Behavior

This page owns the shared release rules, VS Code shortcut, and optional desktop commands. [Android releases](android-releases.md) owns the platform split and workflow table. The [technology stack](technology-stack.md) owns tools and signing; [version tags](version-tags.md) owns platform versions and notes. The commands are repository tooling, not an app module.

### Five workflows: unit tests on each push, platform tests on main, releases by hand

The [platform workflow table](android-releases.md#two-platform-test-workflows-replace-run-all-tests) lists the entries. The former Run All tests file now runs desktop tests only. Android has its own test and release entries; there is no aggregate release gate.

No workflow starts on a tag, so a version tag pushed any other way, such as with `git tag`, publishes nothing.

### Every push to main runs every test

A push to `main`, including a merged pull request, starts **Run Desktop tests** and **Run Android tests** independently on the pushed commit. Other branches' pushes still start only Build and unit tests. No pull-request trigger is added.

- **The trigger filters on `main`**, so other branches and tag pushes create no full-test run. If the default branch is renamed, update that filter too.
- **A newer push does not cancel an earlier run.** Each push keeps its own result.
- **Build and unit tests keeps its [timing check](technology-stack.md#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue).** Each platform workflow runs every test kind of what it ships.

### Start a release or an extra test run from GitHub

Open the repository's **Actions** page. Starting a workflow needs repository write access, not admin access ([GitHub's instructions](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)).

| To | Do this |
| --- | --- |
| Release a version | Select **Test then Tag and Release**, choose **Run workflow** on `main`, enter a `part` or leave it empty, and leave `tag` and `commit` empty. Starting the run approves publishing: Plan shows the tag in its log, and the run does not pause to ask again. |
| Run platform tests without releasing | A push to `main` starts both selections. For an extra run, choose **Run Desktop tests** or **Run Android tests**, then **Run workflow** on the pushed branch or tag. |
| Publish an Android APK | Follow [Publish an Android APK](publishing-android.md); desktop release inputs never start Android publication. |
| Investigate a failed release | Open the failed job's log, then use the [failure table](#a-failed-release-run-leaves-a-tag-only-once-tag-passed). |

### A release run plans, tests, tags, then publishes

A Test then Tag and Release run takes three text inputs, each empty by default:

| Input | What it gives |
| --- | --- |
| `part` | `major`, `minor`, or `patch`, as the [version tag script](version-tags.md#making-a-tag-raises-one-part-of-the-latest-version) takes it. Empty means the script's default, so the parts and the default keep one home. |
| `tag` | The tag the release command's preview showed. Plan and Tag refuse any other. |
| `commit` | The commit the preview showed. Plan and Tag refuse to run on any other. |

You leave `tag` and `commit` empty when you start a run on GitHub's page. Each run's name shows the part you typed, or "default part" when it's empty, so the workflow names no default; a decision to confirm.

Its four jobs run in order:

1. **Plan** checks the request before any test runs. It asks the version tag script for a [preview on the run's commit](version-tags.md#on-github-the-script-tags-the-commit-its-given), so it fails at once on an unknown part, a commit that isn't on the default branch, or a changelog with no notes for the version. Then it asks the [run check](#a-release-reuses-a-passed-platform-test-run-of-its-commit) whether that platform already passed on that commit.
2. **Tests** calls the platform's [reusable test workflow](android-releases.md#two-platform-test-workflows-replace-run-all-tests), on the same commit. It's skipped when Plan found a pass.
3. **Tag** runs only when Plan passed and Tests passed or was skipped, never after a failure or a cancel. Through the version tag script, it makes Plan's tag on the tested commit and pushes it, or refuses when the next tag changed since Plan. A push to the default branch during the tests changes nothing: Tag still tags the tested commit. The tagger is `github-actions[bot]`; a decision to confirm.
4. **Publish** checks out the new tag and runs the [release steps](technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases).

- **Only Tag and Publish write the repository's contents.** Plan also reads Actions results; the other jobs only read.
- **Each platform's release runs go one at a time, in order.** Each release workflow has its own concurrency group with `queue: max`, `release` for desktop and `android-release` for Android, so up to 100 runs of each wait and none is canceled ([concurrency](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)). Platform test runs and pushes run alongside them. Not confirmed by a hosted run; actionlint 1.7.12 doesn't know the `queue` field yet and reports it.
- **Inputs reach a step only through environment variables**, never written into its command, so typed text can't run as code ([script injections](https://docs.github.com/actions/concepts/security/script-injections)). A decision to confirm.

### A release reuses a passed platform test run of its commit

You decided on November 22, 2026, that a release never runs the same tests twice on one commit, and kept it on November 23. Plan runs the [run check](../../.agents/full-run-check.mjs), the rule's one home. Each point below is a decision to confirm:

- **A pass is the newest selected-platform run of that exact commit, started by `push` or `workflow_dispatch`, whose newest attempt and every required job passed.** The [shared metadata](../../.agents/release-platforms.json) names the workflow and jobs. Automatic and manual runs are considered together. Other events never count; adding a trigger later does not silently make its runs eligible.
- **An unfinished or failed newest eligible run isn't a pass**, so Tests runs every test itself. Plan never waits for another run. To reuse the automatic run, start the release after that run passes.
- **A release run's own Tests never counts**, even when Tag or Publish failed after it. GitHub lists a called workflow's jobs under the caller's run, so counting them would give the rule a second kind of run to read. A failed Publish needs no new tests anyway, as [below](#a-failed-release-run-leaves-a-tag-only-once-tag-passed).
- **A run shows what Plan found.** Plan writes a notice, shown on the run's summary page, and a line in its job summary ([workflow commands](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-commands)). It names the commit and links the reused run, or says Tests runs every test.
- **The check needs only the repository and the commit**, and Plan's token reads Actions results (`actions: read`). Its `--ref` option goes: it only named where to start a missing run, and Plan now runs the tests instead.

### A failed release run leaves a tag only once Tag passed

The table lists what a failed run leaves, and what you do:

| Fails in | Left behind | What you do |
| --- | --- | --- |
| Plan or Tests | No tag, nothing published | Fix the problem, commit and push, then start a new release |
| Tag | No new tag on origin; the script deletes the tag it made when its push fails | Start a new release |
| Publish | The tag on origin, no published release, and a [draft](technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) of it if the upload started, which no user or installed app sees | Delete the draft on GitHub, if there is one. Then re-run the failed jobs: GitHub re-runs only Publish and keeps the other jobs' results ([re-runs](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs)). Or [clear](version-tags.md#clearing-deletes-one-version-tag-here-and-on-origin) the tag and start a new release. |

### The release command previews, asks once, then follows the run

This optional terminal command previews the tag and commit before asking you to start the same release workflow. `node .agents\release.mjs [<part>]` needs Node.js, git, and GitHub CLI (`gh`), signed in with access to origin. It works on the repository beside the script, whatever the current folder, and origin must name one repository on github.com.

1. **It prepares.** It takes the part or asks for one, checks your sign-in and origin, and runs the Node tooling tests. It runs no .NET test: every test runs [only in the cloud](test-selection.md#the-full-suite-runs-only-in-the-cloud).
2. **It previews the tag**, which checks your clone and the committed changelog. When the preview refuses, you fix the named problem in another terminal; Enter retries, anything else cancels. The command never edits files, commits, pulls, switches branches, stashes, discards changes, or pushes.
3. **It asks you once**, before it starts anything, to type exactly `yes` to test, tag, and publish the previewed tag from the previewed commit. Any other answer cancels, and nothing starts. This replaces your earlier choice that a check you waited for still needs a yes after it passes, since the run tags and publishes right after its tests.
4. **It starts Test then Tag and Release on the default branch** with the part, the previewed tag, and the previewed commit, and asks GitHub for the new run's details ([run IDs on dispatch](https://github.blog/changelog/2026-02-19-workflow-dispatch-api-now-returns-run-ids/)). When the run's commit isn't the previewed one, it says the default branch moved, links the run, and stops; that run's Plan refuses by itself.
5. **It follows that run until it ends.** When the run fails, it lists each failed job with its link, says whether the version tag now exists on origin, and gives the matching row of the [failure table](#a-failed-release-run-leaves-a-tag-only-once-tag-passed). It never re-runs a job.
6. **When the run passes**, it checks that the release is published, isn't a prerelease, and holds exactly one nonempty Windows setup executable, then guides [your checks](#installation-and-the-smoke-check-stay-manual).

It never looks for an earlier platform test run, since Plan does that, and never pushes a tag; a decision to confirm. It requires an interactive terminal, so redirected input can't approve a release; also a decision to confirm.

### The `run-all-tests` command tests the commit you pushed

The optional `node .agents\run-all-tests.mjs` command retains its filename but now starts **Run Desktop tests** on your current branch and follows it. It takes no arguments and asks nothing. Start Android tests separately from Actions.

- **It refuses before starting** when you aren't on a branch, or your branch's newest commit isn't exactly origin's, so the run tests the commit you have. Uncommitted changes don't stop it; it warns that they aren't tested.
- **It checks the started run's commit**, as the release command does, then follows the run.
- **It exits 0 only when the run passed**, and links the run either way.

### Both commands share how they reach GitHub and the terminal

One GitHub component, `.agents/github-runs.mjs`, reads origin's repository, checks your sign-in, starts a workflow and reads the new run, follows a run, and lists a run's failed jobs. The run check reads runs and jobs through it too, so GitHub's run format has one reader. Each command keeps only its own steps.

- **Every wait is bounded**, and the limits live in the component; a decision to confirm. An expired wait fails with the run's link; it never accepts an unfinished run. Proposed, each a decision to confirm: a command ends after 2 minutes, or 30 minutes for one that shows its output live; a run is read every 10 seconds; and following a run ends after 3 hours.
- **The failure report names the row of the failure table for the latest failed stage**, Plan, Tests, Tag, or Publish; proposed, a decision to confirm.
- **Every question appears in yellow, and one cyan message names each wait on GitHub** before it starts. With no color support, or `NO_COLOR` set, text is plain. A decision to confirm. Node.js's own [terminal colors](https://nodejs.org/api/util.html#utilstyletextformat-text-options) do this, so there's no new dependency.
- **Git and GitHub CLI run through one bounded, cancellable executor.** Ctrl+C stops the command, not a run it started; the command prints that run's link.

### Installation and the smoke check stay manual

After releasing through the browser, download the Windows setup executable from GitHub's **Releases** page. The optional release command downloads it to its own new temporary folder, prints the path, and never launches it; a decision to confirm. It warns about the [unsigned installer](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), then guides installation and a short check of the installed app. Success means GitHub published the release and you confirmed the checks, not that the script saw the app work.

### Tests check the guide without publishing anything

- **One fake GitHub, `github-runs.fake-github.mjs`, serves every test** of both commands, the shared component, and the run check. The version tag script's [fake git](../../.agents/version-tag.fake-git.mjs) serves the release command's tests.
- **Unit tests** cover approval, preparation, a run started on another commit, following, failed jobs, release validation, and the run check against made-up runs: push and manual passes, their newest run deciding, another commit or workflow, an ineligible event, a failed or unfinished attempt, a missing `full-tests` job, and none.
- **Integration tests** run the commands as child processes against the fake, with isolated downloads; none calls real GitHub or launches an installer.
- **Only hosted runs check the workflow files**: the automatic trigger, Plan, the skipped Tests, Tag's condition, and the queue. Opening the VS Code task and installing the app remain manual checks.

## Public interface

No app module uses this tooling. You use:

- **The open GitHub Actions task** in [tasks.json](../../.vscode/tasks.json). It runs `gh browse --actions` in the workspace, opening origin's Actions page without starting a run. One browser shortcut replaces the two run-command tasks; a decision to confirm.
- **`node .agents\release.mjs [<part>]`**, optional. Exit code 0 means a cancel before any run started, or a published release whose checks you confirmed. Anything else exits nonzero.
- **`node .agents\run-all-tests.mjs`**, optional. Exit code 0 means the run passed.
- **Test then Tag and Release's inputs**, [above](#a-release-run-plans-tests-tags-then-publishes).

Both commands describe themselves with `--help`. Plan reads the run check's command line, `node .agents/full-run-check.mjs --repo <owner/name> --commit <ID> [--platform desktop|android]`. Desktop remains the default. A decision to confirm:

| Outcome | Standard output | Error output | Exit code |
| --- | --- | --- | --- |
| A pass exists | The passed run's link | Nothing | 0 |
| No pass | Nothing | Nothing | 0 |
| Wrong arguments, or GitHub can't be read | Nothing | One line naming the problem | 1, and Plan fails |

## Failure modes

- **Missing tools or GitHub sign-in:** the command names the failed operation and tells you to install GitHub CLI or run `gh auth login`.
- **Repository or changelog not ready:** the preview names the fix; you may fix it and retry, or cancel.
- **The default branch moves between the preview and the run's start:** the command stops, Plan refuses, and nothing is tagged. Run the command again.
- **A release run fails:** the [failure table](#a-failed-release-run-leaves-a-tag-only-once-tag-passed) says what's left and what you do.
- **Run Desktop tests is started on a branch without `all-tests.yml`:** GitHub refuses, and the command shows its message. Merge the default branch, push, and try again.
- **The command is interrupted, or a wait expires, after it started a run:** the run goes on, and the command prints its link.
- **Manual checks fail or stay unconfirmed:** the release may already be public. The command reports failure.

## Ruled out

- How the workflows split:
  - **Two files, with `build.yml` as Run All tests when started by hand**: a called workflow sees its caller's event, so every job's condition must tell three cases apart, and push runs show as Run All tests.
- How automatic full tests start:
  - **Trigger on every branch, then skip non-default branches in the job**: each branch would leave a skipped run. A newer skipped run for the same commit could hide its earlier pass from the release check.
  - **Cancel an older run on a newer push**: it saves runner time but leaves the older commit without a completed result.
  - **One workflow with a mode input**: one **Run workflow** entry with a list, not two named entries, and every condition grows.
  - **File names that spell the display names**, such as `test-tag-release.yml`: more links change, and GitHub lists a renamed file's past runs apart.
- How a release is tested:
  - **Each release run always runs every test itself**: a release right after a passing platform test run would run its tests twice, which reverses your November 22 choice.
  - **A release run's passed Tests counts as a pass**: a second kind of run for the rule to read.
  - **The release command also looks for an earlier run**: Plan already does, and the rule would have a second caller.
  - **Every test on each branch push, or on your PC before tagging**: each branch push would wait for the full suite; on your PC, releases depended on its desktop, as from November 16 to 22, 2026.
  - **A release trusts another run's uploaded files or a signed attestation**, or a pass of another commit with the same files: more to get right than asking GitHub for one commit's result.
- How a tag is made and published:
  - **A pushed version tag still publishes**: two publishing paths to keep correct.
  - **The skill starts the release run**: AI's exception to "never push" would widen to a run that publishes.
  - **The run refuses when the default branch moved during its tests**: any push would waste the whole run. Tagging the newest commit instead would release untested code.
  - **The runner checks out the default branch at the tested commit** so today's checks pass: it fakes a clone's state, and fails whenever the branch moved.
  - **The part alone, with no `tag` and `commit` inputs**: the run couldn't refuse a tag you didn't approve.
  - **The part as a list in the workflow**: the parts and default would have two homes, or a generator would rewrite a workflow file.
- How you approve and start runs:
  - **Your yes after the tests, through a GitHub environment you review**: a repository setting outside the review surface, and runs started on GitHub's page wait for approval.
  - **A task that runs `gh workflow run` itself, or no task**: nothing would check which commit the run tests.
  - **Keep the release a version and run all tests tasks**: routine use would still lead through local commands instead of the Actions page.
  - **One concurrency group without `queue: max`**, or one on Tag and Publish only: a third run cancels the waiting second, or a failed publish re-runs a tagging that refuses.
- What the command does for you:
  - **Automatic commits, branch changes, changelog edits, installation, or deletion of a release**: these need your judgment.
  - **Skipping the manual installer checks**: the command can't see the installed app.
  - **Selecting the latest Actions run**: it could belong to another commit or branch.
  - **New dependencies or another version parser**: Node.js's own modules, GitHub CLI, and the tag script suffice.

## Open questions

None.
