# Sync through git: how changes travel between devices

Status: Decided

Sync is the shared module that keeps every device's clone of the task repository the same: it commits this device's changes, fetches and merges the others', and pushes, over SSH with the device's own key.

## Behavior

[Tasks](tasks.md) decides what's stored and how two versions of a task merge. This page decides how a device signs in, when changes travel, and what the user sees when sync fails.

### Each device signs in with its own RSA key

Each device makes its own SSH key: RSA, 3072 bits, which every host accepts. Azure DevOps accepts only RSA ([Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/repos/git/use-ssh-keys-to-authenticate)); GitHub, GitLab, and Bitbucket accept Ed25519 and RSA ([GitHub](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent), [GitLab](https://docs.gitlab.com/user/ssh/), [Bitbucket](https://support.atlassian.com/bitbucket-cloud/docs/configure-ssh-and-two-step-verification/)).

On Android, the key can live in the hardware-backed [Android Keystore](https://developer.android.com/privacy-and-security/keystore), which never lets it out. SSH.NET accepts a key through its own interface ([IPrivateKeySource](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/IPrivateKeySource.cs)), so the Keystore could sign for it, but nobody has tried this; the [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) shows it.

Proposed: the public key's comment is the device name, so the host's key list shows each key's device. This is a decision to confirm.

Proposed: the transport offers the key only with SHA-2 signatures, `rsa-sha2-512` or `rsa-sha2-256`, never with SHA-1 as `ssh-rsa`. SSH.NET 2026.0.0 offers `ssh-rsa` first ([PrivateKeyFile](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/PrivateKeyFile.cs)), and the [Windows proof](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport)'s test server dropped the connection on that offer. Each refused offer also costs a round trip. This is a decision to confirm.

### The Windows key file is encrypted, and only the user's account unlocks it

On Windows, the key file is encrypted with a random passphrase. The passphrase is protected for the user's account with the Windows data protection API (DPAPI) ([data protection](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection)). During sync, the app unprotects the passphrase, decrypts the key in memory, and hands it to the [SSH.NET transport](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport); SSH.NET opens encrypted keys from memory ([PrivateKeyFile](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/PrivateKeyFile.cs)). The decrypted key is never written to disk, a copy of the file, such as in a backup, gives no push access, and nothing needs administrator rights.

### The app checks each host key against its own list

The app keeps its own file of trusted host keys, and never reads or changes the user's `~/.ssh` folder. The transport checks each host key in our code, through SSH.NET's host key event ([BaseClient](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/BaseClient.cs)). It ships the host key fingerprints that GitHub, GitLab, Bitbucket, and Azure DevOps publish ([GitHub](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/githubs-ssh-key-fingerprints), [GitLab](https://docs.gitlab.com/user/gitlab_com/), [Bitbucket](https://support.atlassian.com/bitbucket-cloud/docs/configure-ssh-and-two-step-verification/), [Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/repos/git/use-ssh-keys-to-authenticate)). For any other host, Configure shows the host key's fingerprint once, and the user accepts it. A changed host key stops sync with an error.

Proposed: when a built-in host changes its key, an app update brings the new one, as when [GitHub replaced its RSA host key](https://github.blog/news-insights/company-news/we-updated-our-rsa-ssh-host-key/) in March 2023. For other hosts, the user removes the stored key in Configure and accepts the new one. This is a decision to confirm.

### On the desktop, sync runs soon after a change and every 30 minutes

The table lists what starts a sync on the desktop; the Android [Shell](android/shell.md#sync-runs-while-the-app-is-open-and-every-few-hours-in-the-background) decides the phone's timing. Every trigger runs the same [sync](#each-sync-commits-fetches-merges-and-pushes-one-branch).

| When | What happens |
| --- | --- |
| A change is saved | A sync starts about 5 seconds later, so a burst of edits makes one commit |
| The app starts, View Tasks opens, or 30 minutes pass | A sync starts |
| The user presses Sync in View Tasks, or Sync now in Configure | A sync starts at once; the user asked for the button, to sync faster |
| A sync failed | It retries, waiting longer each time, up to 15 minutes |

Proposed, each a decision to confirm:

- The 5-second wait restarts with each change.
- Retries wait 30 seconds, then twice as long each time, up to 15 minutes. A change or a manual sync still starts one at once.
- Saving never waits for the network: Sync locks the clone only while it commits or writes a merge.

### Each sync commits, fetches, merges, and pushes one branch

Proposed: each sync runs these steps. This is a decision to confirm.

1. **Delete** the tasks [due for automatic deletion](tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares).
2. **Commit** this device's changes as one commit. Its message lists them, such as `Add: Buy milk`, and its author is the device name.
3. **Fetch** the remote's default branch, the only branch sync uses.
4. **Merge**, when both sides have new commits. The app computes the merged files with [Tasks' merge rules](tasks.md#two-versions-of-a-task-merge-field-by-field), never git's line-based merge, and commits a merge with two parents.
5. **Push**. When another device pushed first, the push is rejected, and sync fetches and merges again.

Sync ignores files ending in `.tmp`: it never commits, merges, or deletes them. Only a cut-off write leaves one, as the [shared platform](platform.md#files-are-read-and-written-whole-through-one-interface) says, so it stays on that device until the next write of the same file replaces it.

Also proposed, each a decision to confirm:

- Files outside `tasks/`, `images/`, and `settings.json`, such as a README, are left as the remote has them.
- A task file from the remote that can't be read is skipped, never deleted, and named in the sync status.
- After 3 rejected pushes in a row, the sync fails with an error and retries later.

### Configure sets up a device by cloning the repository

Proposed: a device is set up in Configure in these steps. This is a decision to confirm.

1. The user enters the repository's SSH address, such as `git@github.com:<you>/<repository>.git`, and the device name, which starts as the machine's name.
2. Generate makes the device's key. Configure shows the public key with a Copy button, and links to the host's page for adding it. On GitHub, that's a [deploy key](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/managing-deploy-keys), and Configure reminds the user to check "Allow write access".
3. Configure checks the host key: silently for the four built-in hosts, and by showing its fingerprint for any other.
4. Configure clones the repository. On an empty repository, the first device creates `settings.json`.

Afterwards, Configure shows the last sync time, the status, and Sync now.

Also proposed, each a decision to confirm:

- On GitLab, Configure links the repository's [deploy keys](https://docs.gitlab.com/user/project/deploy_keys/), which need write permission. On Azure DevOps and Bitbucket, it links the account's SSH keys: Azure DevOps has no repository keys, and Bitbucket's [can only read](https://support.atlassian.com/bitbucket-cloud/docs/set-up-repository-access-keys-on-linux/). There, a device's key opens every repository of the account; this is a known limit.
- Setup checks that the key can push, so a read-only key fails in Configure, not at the first change.
- A device counts as configured once its clone exists and its first sync succeeded.
- On an empty repository, the first commit goes to a branch named `main`.
- Each app gives Sync a data folder for the clone, the key, and the trusted host keys. On the desktop, it's under `%LocalAppData%`, apart from Velopack's install folder, so updating or uninstalling leaves it.

## Public interface

Each app's Shell, Task list, and Configure use these parts:

- **Sync now**: starts a sync at once, or joins the one running, and reports how it ended.
- **Sync status**: working, offline, or a named kind of error with its details; the last successful sync time; and the files skipped. It sends a notice when it changes.
- **Setup**: the address and device name, the public key and the host's page for it, the host key fingerprint to accept, the clone, and whether the device is configured.

Proposed, each a decision to confirm:

- When it starts, the app passes Sync its periodic interval: 30 minutes on the desktop, and none on Android, where the [Android Shell](android/shell.md#sync-runs-while-the-app-is-open-and-every-few-hours-in-the-background) runs background syncs.
- Sync status also says whether saved changes still wait to be pushed, so the Android Shell can hand them to background work when the user leaves the app.

Sync uses Tasks' change notice, merge rules, and automatic deletion. Each app implements Sync's interface to its secure key store, as [How the apps fit together](overview.md#two-apps-share-one-c-library) says.

## Failure modes

The table lists each failure, whether it shows as offline or as an error, and what the user sees. Proposed: this split, and what the user sees. This is a decision to confirm.

| Failure | Shown as | What the user sees |
| --- | --- | --- |
| The host can't be reached | Offline | The offline badge; sync retries |
| The host rejects the key | Error | Configure says to add the public key on the host, with write access |
| The host key changed | Error | Sync stops until the [host key](#the-app-checks-each-host-key-against-its-own-list) is updated |
| The repository isn't found | Error | Configure shows the address it tried |
| The push is rejected 3 times in a row | Error | Sync retries later |
| A task file can't be read, or has a newer `version` | Error; the rest of the sync completes | The status names the file, as [Tasks](tasks.md#failure-modes) says |
| The clone can't be written, such as on a full disk | Error | The error's details; sync retries |

On the desktop, being offline and errors show as [Shell](desktop/shell.md#sync-stays-quiet-unless-something-is-wrong) decides.

## Ruled out

The options below were considered and rejected, grouped by decision. The git and SSH libraries ruled out are in [How the apps fit together](overview.md#ruled-out).

- Key type:
  - **Ed25519**: Azure DevOps rejects it, and the Android Keystore can't hold it.
- Windows key protection:
  - **An unencrypted key file only the user can read**: any copy, such as a backup, gives push access.
  - **Windows' ssh-agent service**: it needs administrator rights once, and holds all the user's keys together.
  - **OpenSSH asking the app for the passphrase through `SSH_ASKPASS`**: it belongs to the OpenSSH transport that [How the apps fit together](overview.md#ruled-out) rules out.
- Host keys:
  - **Trusting the first key seen, without asking**: an intercepted first connection would trust an impostor.
  - **Always confirming in Configure**: one more step on every device.
- Desktop timing:
  - **Every 5 minutes**: the user chose 30.
  - **Every minute**: about 1,400 contacts with the host a day.
  - **Only at start and when View Tasks opens**: the tag chips and task references in Add would go stale.
