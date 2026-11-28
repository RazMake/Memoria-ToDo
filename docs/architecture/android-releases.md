# How the Android app is released apart from the desktop app

Status: Decided

Android has its own test and release pipelines. Neither app's release waits for the other's tests, queue, version, or notes. [Publish an Android APK](publishing-android.md) gives the setup and manual steps.

## Behavior

[Release a version](releasing.md) describes today's desktop release and its shared release rules. This page owns the split and the Android pipeline. The [technology stack](technology-stack.md) owns runners, device commands, signing, and the version code; [Version tags](version-tags.md#each-app-has-its-own-tags-and-notes) owns platform versions and notes.

### Two platform test workflows replace Run All tests

The table lists the target workflows. Existing files keep their names where possible; no aggregate Run All tests entry remains.

| File | Name on Actions | Starts on | Checks or publishes |
| --- | --- | --- | --- |
| `build.yml` | Build and unit tests | Each branch push | Repository-wide build and unit health; not a release gate |
| `all-tests.yml` | Run Desktop tests | Each push to `main`, manual, or desktop release call | Desktop, its shipped shared code, Local API clients, and relevant architecture tests |
| `android-tests.yml` | Run Android tests | Each push to `main`, manual, or Android release call | Android, its shipped shared code, relevant architecture tests, and native emulator tests |
| `release.yml` | Test then Tag and Release | Manual on `main` | Desktop installer, update files, and Local API client packages |
| `android-release.yml` | Test then Tag and Release Android | Manual on `main` | One signed Android package (APK) |

The two platform selections together cover every test kind in the repository. Each selection includes all tests of the modules that platform ships, not just tests of changed files. Shared tests can run in both. [Test selection](test-selection.md#the-full-suite-runs-only-in-the-cloud) owns how the project graph determines those selections.

Each platform workflow owns its test steps once. Its release calls that workflow or reuses its successful exact-commit run. A desktop-only failure cannot fail Android's gate, including through a solution-wide build or unrelated architecture assertion. The reverse holds too.

### The pipelines share tools, never release prerequisites

- **No prerequisite crosses apps.** Jobs, workflow results, queues, versions, notes, and update feeds remain platform-specific. Shared code that both apps ship must still pass both gates.
- **One run check serves both platforms.** It applies the [existing reuse rule](releasing.md#a-release-reuses-a-passed-platform-test-run-of-its-commit) to the selected platform's workflow and required jobs only. It never waits for or accepts the other platform's run.
- **One version tag script serves both platforms.** Workflows get versions and notes from it, rather than adding parsers.
- **Each release uses its own queue.** Desktop keeps `release`; Android uses `android-release`, with the same no-cancellation queue policy. Ordinary GitHub runner capacity is still shared.
- **Desktop keeps its release inputs and publishing behavior.** Its test gate narrows, and its updater and delta lookup must [filter by platform](#each-updater-finds-only-its-own-releases).

### The Android pipeline plans, tests, tags, then publishes

You start Android releases by hand on `main`. The inputs and approval follow the [desktop release rules](releasing.md#a-release-run-plans-tests-tags-then-publishes). The table lists Android's four jobs.

Initially every Android publication is a GitHub prerelease. There is no stable-publication input until the app is ready in delivery step 7. This permits signed APK downloads for early testing without notifying normal installed copies.

| Job | Runner | Does |
| --- | --- | --- |
| Plan | `windows-2025` | Previews the next Android tag on the run's commit; validates Android notes, inputs, and version-code bounds; checks for a passed Android test run of that commit |
| Tests | Windows and Linux | Skipped only when Plan found that pass. Otherwise calls `android-tests.yml`: PC tests on Windows and native device tests on the [Linux emulator](technology-stack.md#a-linux-job-runs-the-android-tests-on-an-emulator-before-each-release) |
| Tag | `windows-2025` | Pushes Plan's tag on the tested commit, only after Plan and the Android gate succeed |
| Publish | `windows-2025` | Checks out the tag, builds and signs the APK, validates it, uploads to an Android draft, then publishes it |

Publish checks that the APK is nonempty, carries the intended application ID, display version and version code, and has the release signature. Its ABI follows the [Android app design](android/README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material). It uploads no desktop or Local API assets.

Before making the draft public, Publish reads it back and requires one completed APK asset with the inspected file's name, size, and SHA-256. Its tag must still match and it must still be a prerelease draft. An interrupted or changed upload leaves the draft unpublished.

Only Tag and Publish write repository contents. Plan reads Actions results; test jobs only read. Only Android Publish receives the [signing secrets](technology-stack.md#the-apk-signing-key-lives-in-a-github-actions-secret-backed-up-offline). A missing secret or invalid APK fails before upload. Temporary key files are removed even after failure, and passwords never enter logs.

### Each updater finds only its own releases

Both apps publish in this repository. Android initially publishes prereleases, and release names include Android and its version; they are never marked GitHub's latest release. Separate names also avoid Velopack's [duplicate-name refusal](https://github.com/velopack/velopack/blob/1.2.0/src/vpk/Velopack.Deployment/GitHubRepository.cs#L72-L81).

Each updater filters by its platform's [tag rule](version-tags.md#each-app-has-its-own-tags-and-notes), ignores drafts and prereleases, and chooses the highest eligible version with the required assets. Neither asks GitHub for the repository's single latest release.

Velopack 1.2.0 reads only the [newest 10 releases](https://github.com/velopack/velopack/blob/1.2.0/src/lib-csharp/Sources/GithubSource.cs#L98). Both the desktop adapter and Publish's delta lookup must read past unrelated Android releases. If a request fails, they report the failure, not "no update." They keep the [Updates port](desktop/updates.md#public-interface) unchanged.

After paging past unrelated releases, the desktop adapter keeps the ten highest eligible desktop versions, not the ten most recently published. This bounds release-feed downloads without hiding the newest update. You approved this on November 28, 2026; the limit lives in [shared release metadata](../../.agents/release-platforms.json).

Already-installed desktop versions keep their old lookup. Document a manual installer upgrade for affected versions. Android never waits for a desktop release or completion of that migration.

### Delivery follows the Android backlog

The Android [delivery backlog](android/README.md#delivery-backlog) owns when the app's features are built. You brought the pipeline infrastructure and prerelease APK publication forward: workflows, shared tools, test host, version stamping, signing, and draft-then-publish upload. Stable publication and in-app updates remain in step 7. Building the infrastructure releases nothing; a manual workflow run approves only an early prerelease. Not confirmed: no native screen test, published APK, or hosted run of these workflows has passed yet, so this page stays Decided until they do.

Implementation covers these areas:

- Release and test workflows, the shared run check, and platform test selection.
- The existing version tag script and the separate Android changelog.
- Desktop updater and delta lookup. Android in-app update discovery remains in step 7.
- DeviceRunners production integration, pinned dependencies, and portable Android project selection for Linux. The proof branch is never merged.
- APK version stamping, signing, asset validation, and migration guidance.

### Tests prove independence without publishing real releases

Implementation tests cover backlog row 26 and the infrastructure brought forward from Android steps 2 and 7:

- **Unit:** independent tag sequences and notes; exact-commit, platform-only run reuse; version-code boundaries; platform test selection; asset validation; update discovery past more than 10 unrelated releases.
- **Integration:** scripts against isolated repositories and fake GitHub; malformed APKs, signing failures, and interrupted upload leave no public release.
- **UX:** native screens in the DeviceRunners host with fake services, under the existing screen-test policy.
- **Hosted:** simultaneous releases use different queues; a red desktop gate or failed desktop build does not block Android, and a failed Android gate or emulator does not block desktop; a passed platform run skips Tests; signature and version stamping match the tag.

## Public interface

Android's manual workflow takes `part`, `tag`, and `commit`, under the [existing input rules](releasing.md#a-release-run-plans-tests-tags-then-publishes). The shared run check and version script gain a platform selector; existing desktop calls retain their defaults. No app module's interface changes.

The optional Android release command remains deferred to step 7. It must dispatch Android's workflow and verify the APK rather than a Windows installer.

## Failure modes

- **Plan or Android Tests fails:** no Android tag or release. Desktop continues.
- **Tag fails or its push is refused:** use the existing script's rollback; no published release.
- **Publish fails:** the Android tag remains, and a draft may remain. Use the [release recovery table](releasing.md#a-failed-release-run-leaves-a-tag-only-once-tag-passed), applied only to that Android release.
- **A version exceeds the version-code limits:** Plan refuses before tests.
- **GitHub is unavailable or has no runner capacity:** both workflows may be delayed by the provider. Separate pipelines remove repository prerequisites, not this shared infrastructure.

## Ruled out

- **One release pipeline, queue, or version sequence:** the platforms block each other.
- **A separate Android release repository:** adds a cross-repository credential and changes the decided update source.
- **One aggregate test workflow, even with independent jobs:** you chose separate platform entries and no aggregate.
- **Shared release notes:** one platform consumes or waits for the other's notes.
- **Linux APK publishing:** Windows already builds the app; Linux is needed only for the emulator.
- **An independent version-code counter:** adds another version source.
- **Waiting for a desktop migration before Android publication:** old desktop copies get manual upgrade guidance instead.
- **Stable early APKs:** prereleases allow testing without offering the unfinished app through normal updates.
- **A second Android test project:** the existing project remains dual-targeted, preserving its module boundary and friend-assembly name.

## Open questions

None. Hosted validation and publication use the [publishing guide](publishing-android.md).

### Decisions to confirm

- **Platform test tools are selected from the shared release metadata.** This keeps workflow files free of module and script lists. The alternative is separate hand-kept workflow selections.
- **The shared .NET setup action disables the workload resolver for desktop-only jobs.** Those jobs consume no workload; the SDK pin remains unchanged. Android jobs install the pinned Android or MAUI Android workload. The Android PC job also needs MAUI Android to check the native host's build properties, but runs no native screen tests.
- **`MemoriaAndroidTestTarget=pc|android` selects referenced Android projects before source exclusions.** With no selector, production modules keep their dual target and the test project defaults to PC. The alternative is restoring native frameworks during every PC test build.
- **APK inspection uses the newest numeric build-tools directory in the private SDK installed by the pinned workload.** The alternative is another independently maintained tool version.
