# How you release a version with one command

Status: Decided

Run `node .agents\release.mjs`, or choose **Tasks: Run Task > release a version** in VS Code. The [release command](../../.agents/release.mjs) previews the next version tag, asks you once, then starts **Test then Tag and Release** on GitHub, which runs every test, tags the tested commit, and publishes. The command follows that run, downloads the installer, and guides your manual checks. To run every test without releasing, start **Run All tests** with `node .agents\run-all-tests.mjs` or the **run all tests** task.

## Behavior

You decided this design on November 23, 2026. This page owns the two workflows you start by hand and the commands that start them. The [technology stack](technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) owns what all workflows share and the release steps, and [version tags](version-tags.md) own the tag. The commands are repository tooling, not an app module.

### Three workflows: one for pushes, two you start by hand

The table lists them:

| File | Name on the Actions page | Starts on | What it does |
| --- | --- | --- | --- |
| `build.yml` | Build and unit tests | Each branch push | Builds and runs the unit tests, and [times](technology-stack.md#the-workflow-times-the-unit-run-and-a-slow-run-opens-one-issue) the default branch's run |
| `all-tests.yml` | Run All tests | **Run workflow** on any pushed branch or tag, or a call from `release.yml` | Its `full-tests` job, the one home of the full-test steps: the [desktop check](technology-stack.md#agents-check-the-desktop-before-they-start-screen-tests), then the [full-suite command](technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-kind-of-test). It publishes nothing. |
| `release.yml` | Test then Tag and Release | **Run workflow** on the default branch | Plans, tests, tags, and publishes, [below](#a-release-run-plans-tests-tags-then-publishes). The only way a version is published. |

No workflow starts on a tag, so a version tag pushed any other way, such as with `git tag`, publishes nothing.

### A release run plans, tests, tags, then publishes

A Test then Tag and Release run takes three text inputs, each empty by default:

| Input | What it gives |
| --- | --- |
| `part` | `major`, `minor`, or `patch`, as the [version tag script](version-tags.md#making-a-tag-raises-one-part-of-the-latest-version) takes it. Empty means the script's default, so the parts and the default keep one home. |
| `tag` | The tag the release command's preview showed. Plan and Tag refuse any other. |
| `commit` | The commit the preview showed. Plan and Tag refuse to run on any other. |

You leave `tag` and `commit` empty when you start a run on GitHub's page. Each run's name shows the part you typed, or "default part" when it's empty, so the workflow names no default; a decision to confirm.

Its four jobs run in order:

1. **Plan** checks the request before any test runs. It asks the version tag script for a [preview on the run's commit](version-tags.md#on-github-the-script-tags-the-commit-its-given), so it fails at once on an unknown part, a commit that isn't on the default branch, or a changelog with no notes for the version. Then it asks the [run check](#a-release-reuses-a-passed-run-all-tests-run-of-its-commit) whether Run All tests already passed on that commit.
2. **Tests** calls `all-tests.yml` ([reusable workflows](https://docs.github.com/actions/how-tos/reuse-automations/reuse-workflows)), so a release runs exactly what Run All tests runs, on the same commit. It's skipped when Plan found a pass.
3. **Tag** runs only when Plan passed and Tests passed or was skipped, never after a failure or a cancel. Through the version tag script, it makes Plan's tag on the tested commit and pushes it, or refuses when the next tag changed since Plan. A push to the default branch during the tests changes nothing: Tag still tags the tested commit. The tagger is `github-actions[bot]`; a decision to confirm.
4. **Publish** checks out the new tag and runs the [release steps](technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases).

- **Only Tag and Publish write the repository's contents.** Plan also reads Actions results; the other jobs only read.
- **Release runs go one at a time, in order.** The whole workflow shares one concurrency group with `queue: max`, so up to 100 runs wait and none is canceled ([concurrency](https://docs.github.com/actions/how-tos/writing-workflows/choosing-when-your-workflow-runs/control-the-concurrency-of-workflows-and-jobs)). Run All tests runs and pushes run alongside them.
- **Inputs reach a step only through environment variables**, never written into its command, so typed text can't run as code ([script injections](https://docs.github.com/actions/concepts/security/script-injections)). A decision to confirm.

### A release reuses a passed Run All tests run of its commit

You decided on November 22, 2026, that a release never runs the same tests twice on one commit, and kept it on November 23. Plan runs the [run check](../../.agents/full-run-check.mjs), the rule's one home. Each point below is a decision to confirm:

- **A pass is the newest Run All tests run of that exact commit, started with Run workflow, whose newest attempt passed, `full-tests` job included.** It may have run on any branch or tag, since the commit fixes both the code and the workflow files.
- **An unfinished or failed newest run isn't a pass**, so Tests runs every test itself. Plan never waits for another run.
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

`node .agents\release.mjs [<part>]` needs Node.js, git, and GitHub CLI (`gh`), signed in with access to origin. It works on the repository beside the script, whatever the current folder, and origin must name one repository on github.com.

1. **It prepares.** It takes the part or asks for one, checks your sign-in and origin, and runs the Node tooling tests. It runs no .NET test: every test runs [only in the cloud](test-selection.md#the-full-suite-runs-only-in-the-cloud).
2. **It previews the tag**, which checks your clone and the committed changelog. When the preview refuses, you fix the named problem in another terminal; Enter retries, anything else cancels. The command never edits files, commits, pulls, switches branches, stashes, discards changes, or pushes.
3. **It asks you once**, before it starts anything, to type exactly `yes` to test, tag, and publish the previewed tag from the previewed commit. Any other answer cancels, and nothing starts. This replaces your earlier choice that a check you waited for still needs a yes after it passes, since the run tags and publishes right after its tests.
4. **It starts Test then Tag and Release on the default branch** with the part, the previewed tag, and the previewed commit, and asks GitHub for the new run's details ([run IDs on dispatch](https://github.blog/changelog/2026-02-19-workflow-dispatch-api-now-returns-run-ids/)). When the run's commit isn't the previewed one, it says the default branch moved, links the run, and stops; that run's Plan refuses by itself.
5. **It follows that run until it ends.** When the run fails, it lists each failed job with its link, says whether the version tag now exists on origin, and gives the matching row of the [failure table](#a-failed-release-run-leaves-a-tag-only-once-tag-passed). It never re-runs a job.
6. **When the run passes**, it checks that the release is published, isn't a prerelease, and holds exactly one nonempty Windows setup executable, then guides [your checks](#installation-and-the-smoke-check-stay-manual).

It never looks for an earlier Run All tests run, since Plan does that, and never pushes a tag; a decision to confirm. It requires an interactive terminal, so redirected input can't approve a release; also a decision to confirm.

### The Run All tests command tests the commit you pushed

`node .agents\run-all-tests.mjs`, or the **run all tests** task, starts Run All tests on your current branch and follows it. It takes no arguments and asks nothing.

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

The release command downloads the installer to its own new temporary folder, prints the path, and never launches it; a decision to confirm. It warns about the [unsigned installer](desktop/shell.md#velopack-installs-the-app-and-keeps-it-up-to-date), then guides installation and a short check of the installed app. Success means GitHub published the release and you confirmed the checks, not that the script saw the app work.

### Tests check the guide without publishing anything

- **One fake GitHub, `github-runs.fake-github.mjs`, serves every test** of both commands, the shared component, and the run check. The version tag script's [fake git](../../.agents/version-tag.fake-git.mjs) serves the release command's tests.
- **Unit tests** cover approval, preparation, a run started on another commit, following, failed jobs, release validation, and the run check against made-up runs: a pass, another commit, workflow, or event, a failed or unfinished attempt, a missing `full-tests` job, and none.
- **Integration tests** run the commands as child processes against the fake, with isolated downloads; none calls real GitHub or launches an installer.
- **Only hosted runs check the workflow files**: Plan, the skipped Tests, Tag's condition, and the queue, along with VS Code's terminal and installation.

## Public interface

No app module uses this tooling. You use:

- **`node .agents\release.mjs [<part>]`**, and the **release a version** task in [tasks.json](../../.vscode/tasks.json). Exit code 0 means a cancel before any run started, or a published release whose checks you confirmed. Anything else exits nonzero.
- **`node .agents\run-all-tests.mjs`**, and the **run all tests** task. Exit code 0 means the run passed.
- **Test then Tag and Release's inputs**, [above](#a-release-run-plans-tests-tags-then-publishes).

Both commands describe themselves with `--help`. Plan reads the run check's command line, `node .agents/full-run-check.mjs --repo <owner/name> --commit <ID>`. A decision to confirm:

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
- **Run All tests is started on a branch without `all-tests.yml`:** GitHub refuses, and the command shows its message. Merge the default branch, push, and try again.
- **The command is interrupted, or a wait expires, after it started a run:** the run goes on, and the command prints its link.
- **Manual checks fail or stay unconfirmed:** the release may already be public. The command reports failure.

## Ruled out

- How the workflows split:
  - **Two files, with `build.yml` as Run All tests when started by hand**: a called workflow sees its caller's event, so every job's condition must tell three cases apart, and push runs show as Run All tests.
  - **One workflow with a mode input**: one **Run workflow** entry with a list, not two named entries, and every condition grows.
  - **File names that spell the display names**, such as `test-tag-release.yml`: more links change, and GitHub lists a renamed file's past runs apart.
- How a release is tested:
  - **Each release run always runs every test itself**: a release right after a passing Run All tests would run the suite twice, which reverses your November 22 choice.
  - **A release run's passed Tests counts as a pass**: a second kind of run for the rule to read.
  - **The release command also looks for an earlier run**: Plan already does, and the rule would have a second caller.
  - **Every test on each push, or on your PC before tagging**: each push would wait for the full suite; on your PC, releases depended on its desktop, as from November 16 to 22, 2026.
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
  - **One concurrency group without `queue: max`**, or one on Tag and Publish only: a third run cancels the waiting second, or a failed publish re-runs a tagging that refuses.
- What the command does for you:
  - **Automatic commits, branch changes, changelog edits, installation, or deletion of a release**: these need your judgment.
  - **Skipping the manual installer checks**: the command can't see the installed app.
  - **Selecting the latest Actions run**: it could belong to another commit or branch.
  - **New dependencies or another version parser**: Node.js's own modules, GitHub CLI, and the tag script suffice.

## Open questions

None.
