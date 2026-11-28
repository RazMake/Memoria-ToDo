# Publish an Android APK

Start **Test then Tag and Release Android** on GitHub's Actions page to publish a signed APK. Android has its own tests, version, notes, and queue; it does not wait for a desktop release.

These releases are **prereleases for early testing**, not a finished app or a Google Play publication. [Android's delivery backlog](android/README.md#delivery-backlog) lists what remains.

**Publication still needs its first real check.** [Pipeline verification](android-releases.md#verification) records the passing tests and signing check. Publishing with your release key has not run yet.

## Set up publishing once

1. Commit and push the pipeline changes to `main`. GitHub cannot show the new manual release workflow until its file is on the default branch.
2. Create or use your own PKCS12 release keystore, following [Android's signing guidance](https://developer.android.com/studio/publish/app-signing). Keep its key and passwords backed up offline. Use the same key for later APKs.
3. Configure the repository's [signing secrets and alias](technology-stack.md#the-apk-signing-key-lives-in-a-github-actions-secret-backed-up-offline), on GitHub under **Settings > Secrets and variables > Actions** or with the commands below. You need write access to the repository ([who can set secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets)). Never put their values in git, logs, or a conversation.

Keep the keystore outside the repository folder, since git doesn't ignore it. Run these from the repository root, after signing in with `gh auth login`, in an interactive terminal:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path '<path-to-your-release.p12>').Path)) |
  gh secret set ANDROID_KEYSTORE_BASE64
gh secret set ANDROID_KEYSTORE_PASSWORD
gh secret set ANDROID_KEY_PASSWORD
gh variable set ANDROID_KEY_ALIAS
```

The first command encodes the keystore and pipes it to GitHub CLI, so nothing prints; GitHub CLI drops the trailing line break. The two password commands each ask "Paste your secret:" and hide what you type. The alias command asks "Paste your variable" and shows it, since the alias isn't secret. When prompts are turned off in GitHub CLI, those three wait for standard input instead; press Ctrl+C, run `gh config set prompt enabled`, and run them again. The linked signing section defines the names.

## Publish each version

1. Write the version's user-facing notes in [CHANGELOG.Android.md](../../CHANGELOG.Android.md), following the [changelog guide](../contributing/changelog.md). Commit and push them with the code.
2. Optionally wait for **Run Android tests** to pass on that commit, which the push to `main` starts, so the release reuses it instead of testing again. Desktop results never count.
3. Open **Actions > Test then Tag and Release Android > Run workflow** and choose `main`. Leave `part` empty for the [default bump](version-tags.md#making-a-tag-raises-one-part-of-the-latest-version), or enter `major`, `minor`, or `patch`. Leave `tag` and `commit` empty unless using a preview.
4. Starting this workflow approves publication. It plans the [Android tag](version-tags.md#each-app-has-its-own-tags-and-notes), reuses that commit's Android pass or runs both Android jobs, creates the tag, then builds and signs the APK.
5. After **Publish** passes, open the repository's **Releases** page and download the APK from that Android prerelease.

For a preview before approval, run `node .agents\version-tag.mjs --platform android [<part>] --dry-run` from the repository root. It changes no tag, and requires your committed branch to be in step with origin. The first Android version is 1.0.0. Put its proposed version's notes in the Android changelog before starting the release.

## Install and check the APK

The APK supports the [Android devices the app design specifies](android/README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material). Transfer it to a supported ARM64 phone, open it, and allow installation from that trusted source when Android asks.

A debug-signed development copy cannot update to the release-signed copy. Back up anything you need before uninstalling it. Later releases signed with your unchanged release key can install over the earlier APK.

Open the installed app and check its common screens. Automated tests do not replace this first manual phone check, and a prerelease does not mean unfinished features work.

## If publishing fails

Use the [release recovery table](releasing.md#a-failed-release-run-leaves-a-tag-only-once-tag-passed), applied only to the Android tag and draft. To clear an Android tag, preview with `node .agents\version-tag.mjs --platform android --clear <tag> --dry-run`, then run the preview's last line. If Publish failed after creating a draft, delete that draft before rerunning the failed Publish job; otherwise asset creation can conflict with the earlier attempt.

Old desktop installers may stop finding updates after unrelated releases fill their first page. Upgrade those copies manually from the newest **desktop** release's Windows installer. Android publication does not wait for that migration.

[Android releases](android-releases.md) owns the pipeline design; [the technology stack](technology-stack.md) owns runner, signature, and version-code checks.
