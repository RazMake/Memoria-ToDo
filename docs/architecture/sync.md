# Sync through git: how changes travel between devices

Status: Decided

Sync is the shared module that keeps every device's clone of the task repository the same: it commits this device's changes, fetches and merges the others', and pushes, over SSH with the device's own key.

## Behavior

[Tasks](tasks.md) decides what's stored and how two versions of a task merge. This page decides how a device signs in, when changes travel, and what the user sees when sync fails.

### Each device signs in with its own RSA key

Each device makes its own SSH key: RSA, 3072 bits, which every host accepts. Azure DevOps accepts only RSA ([Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/repos/git/use-ssh-keys-to-authenticate)); GitHub, GitLab, and Bitbucket accept Ed25519 and RSA ([GitHub](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent), [GitLab](https://docs.gitlab.com/user/ssh/), [Bitbucket](https://support.atlassian.com/bitbucket-cloud/docs/configure-ssh-and-two-step-verification/)).

The private key stays on this device: it is never shared, committed, or written into task files or shared settings.
Only the public key is registered with the Git host, through that host's account or repository-access settings;
registering a repository-specific deploy key is not adding a key file to the repository.

On Android, the key can live in the hardware-backed [Android Keystore](https://developer.android.com/privacy-and-security/keystore), which never lets it out. SSH.NET accepts a key through its own interface ([IPrivateKeySource](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/IPrivateKeySource.cs)), so the Keystore could sign for it, but nobody has tried this; the [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) shows it.

Proposed: the public key's comment is the device name, so the host's key list shows each key's device. This is a decision to confirm.

Proposed: the transport offers the key only with SHA-2 signatures, `rsa-sha2-512` or `rsa-sha2-256`, never with SHA-1 as `ssh-rsa`. SSH.NET 2026.0.0 offers `ssh-rsa` first ([PrivateKeyFile](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/PrivateKeyFile.cs)), and the [Windows proof](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport)'s test server dropped the connection on that offer. Each refused offer also costs a round trip. This is a decision to confirm.

### The Windows key file is encrypted, and only the user's account unlocks it

On Windows, the key file is encrypted with a random passphrase. The passphrase is protected for the user's account
with the Windows data protection API (DPAPI) ([data protection](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection)).
During sync, App unprotects the passphrase and opens an owned [signing handle](../../src/Shared/Sync/DeviceKeys/IDeviceSigningKey.cs);
Sync adapts that handle to SSH.NET without requiring private-key export. The decrypted key is never written to disk,
a copy of the file, such as in a backup, gives no push access, and nothing needs administrator rights.

**Approved:** App stores one `device-key.json` in the supplied device-only folder under Windows `%LocalAppData%`,
outside the task clone. Neither key is committed or shared between devices; only the public key is copied for
registration with the Git host. The local envelope has this format:

- The envelope has `version: 1`, `encryptedPrivateKey` (base64 encrypted PKCS#8 DER), and `protectedPassphrase`
  (base64 CurrentUser-DPAPI ciphertext). One atomic filesystem write prevents mismatched key/passphrase files.
- Generation uses a cryptographically random 32-byte passphrase and standard PKCS#8 encryption with AES-256-CBC,
  PBKDF2-HMAC-SHA256, and 100,000 iterations. The encrypted DER records its encryption parameters, so later generation
  defaults can change without rewriting old keys.
- Missing, unsupported, corrupt, or undecryptable envelopes fail opening without replacement. Generate refuses an
  existing envelope. Temporary passphrase buffers are cleared; opened RSA objects and signing handles are disposed.

This is not the proof's DPAPI-wrapped plaintext PEM. App separates the envelope/handle logic from its internal
CurrentUser-protection adapter, so unit tests can use fake files and fake protection without invoking Windows;
real DPAPI persistence and signing are final-validation integration checks. The public port remains
[`IDeviceKeyStore`](../../src/Shared/Sync/DeviceKeys/IDeviceKeyStore.cs), not a Windows or private-key API.

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

Also proposed, each a decision to confirm:

- Commit messages name the device and list each changed managed path with its action: Add, Update, or Delete.
  The task title above is an example, not a requirement to look titles up. Paths also describe settings and image
  changes, and use Git's existing change information without parsing Tasks' files or adding a public interface.
- Files outside `tasks/`, `images/`, and `settings.json`, such as a README, are left as the remote has them.
- A task file from the remote that can't be read is skipped, never deleted, and named in the sync status.
- After 3 rejected pushes in a row, the sync fails with an error and retries later. Only pushes within one sync
  count, whatever reason the remote gives for rejecting them. The write-access check below isn't a push.

**Decided by the user:** allow the remote version or deletion to replace local work in these two cases:

- Content exists only in Git's staging area for an unmanaged file. Sync may unstage it and apply the remote
  result without preserving that staged-only version.
- A local managed task file is malformed and cannot be merged, and the remote deleted that task. Sync may apply
  the deletion without preserving or quarantining the malformed local bytes.

These choices can lose the only copy of that content. Sync does not add a preservation/recovery mechanism for
these cases. This permission does not cover arbitrary failures, private keys or other device-only data.
Unreadable remote task files still follow the skip-and-report rule above; Tasks' input validation is unchanged.

<a id="configure-sets-up-a-device-by-cloning-the-repository"></a>

### Configure keeps the selected clone or makes a new one

Proposed: a device is set up in Configure in these steps. This is a decision to confirm.

1. The user enters the repository's SSH address, such as `git@github.com:<you>/<repository>.git`, and the device name, which starts as the machine's name.
2. Generate makes the device's key. Configure shows the public key with a Copy button, and links to the host's page for adding it. On GitHub, that's a [deploy key](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/managing-deploy-keys), and Configure reminds the user to check "Allow write access".
3. Configure checks the host key: silently for the four built-in hosts, and by showing its fingerprint for any other.
4. Configure keeps the selected clone, lets the user select another existing clone, or clones into a new or empty folder. On an empty repository, the first device creates `settings.json`.

Afterwards, Configure shows the last sync time, the status, and Sync now.

Also proposed, each a decision to confirm:

- On GitLab, Configure links the repository's [deploy keys](https://docs.gitlab.com/user/project/deploy_keys/), which need write permission. On Azure DevOps and Bitbucket, it links the account's SSH keys: Azure DevOps has no repository keys, and Bitbucket's [can only read](https://support.atlassian.com/bitbucket-cloud/docs/set-up-repository-access-keys-on-linux/). There, a device's key opens every repository of the account; this is a known limit.
- Setup checks that the key can push, so a read-only key fails in Configure, not at the first change.
- A device counts as configured once its clone exists and its first sync succeeded.
- Every pass that has nothing to push still checks writable access through `git-receive-pack` before reporting
  success. A previous successful receipt does not prove that the host still grants write access. This adds an
  SSH command/connection to otherwise idle passes; that cost is a low-risk decision to confirm.
- On an empty repository, the first commit goes to a branch named `main`.
- Each app gives Sync a device-only data folder for its private key and trusted host keys, separate from the task clone. On the desktop, it's under `%LocalAppData%`, apart from Velopack's install folder, so updating or uninstalling leaves it.

The supplied device folder must be outside the selected task clone. Sync rejects an already selected clone that
contains that folder before starting work, and rejects clone/select requests for such a folder before creating
directories, generating keys, changing Git configuration, selecting Tasks, or committing anything. Compare
normalized path segments, including equality, not textual prefixes (`clone` is not an ancestor of `clone-other`);
filesystem aliases must not bypass that boundary. Existing clones remain unchanged on rejection. No exclusion
pattern or `.gitignore` is a substitute for keeping private material outside the worktree.

Approved: Sync delegates physical containment to the existing platform
[`IFileSystem`](../../src/Shared/Platform/IFileSystem.cs) contract. App implements OS-specific alias resolution;
Sync remains platform-independent. Resolution runs on the owned worker before effects, and a resolution failure
propagates rather than falling back to lexical paths or allowing the operation. Service creation performs no I/O.

### One owned worker keeps synchronous Git and SSH off the UI thread

LibGit2Sharp's native calls and smart-transport callbacks are synchronous. Sync explicitly owns a dispatcher
for those calls and its synchronous-only local adapters. This is not a claim that wrapping I/O in `Task.Run`
makes it native async. No public call performs Git, key-store, or file I/O on the calling thread.

The worker starts lazily, once per service, and exits during shutdown. One dedicated background thread consumes
a bounded, single-slot synchronous work queue. One asynchronous coordinator serializes setup and sync operations
before dispatch, awaits each work item's completion, and observes its failure. The worker waits on a synchronous
queue primitive, not on a Task. It never uses `.Wait()`, `.Result`, or `GetAwaiter().GetResult()` to bridge async work.

At most one sync pass runs, with one coalesced follow-up requested by changes that arrive during it. Manual callers
join the active pass. Timers only request work; they never run Git or start overlapping passes. Setup uses the same
coordinator. If a dependency returns a Task, the coordinator awaits it and all transport cleanup before dispatching
the next operation. Native callbacks remain synchronous: command completion and cleanup must be retained for that
outer await, not blocked on inside a callback. The proof's blocking command waits are not production code to copy.

Command output EOF is not proof of successful command completion. The coordinator awaits the owned transport
completion and cleanup before reading the final exit result or publishing a successful pass. Late execution,
timeout, and cleanup failures propagate rather than being dropped because a reader already returned.
The existing internal cleanup capability carries that outcome; no public contract addition is needed.
Transport deadlines and timers use the injected TimeProvider, including through the internal operation scope.

Decided: explicit key or write-permission denials are Authentication failures with write-access guidance, using
these case-insensitive rejection phrases: `permission denied`, `access denied`, `marked as read only`,
`marked as read-only`, or `does not have write access`. Bare `read-only` or a mention of write access is not a
rejection match: `read-only file system` remains a Repository failure. Other nonzero remote command exits are
Repository failures, retaining stderr; they are not guessed to be connectivity failures.
SSH authentication, host-key refusal, and actual connection failures keep their existing distinct classifications.

Decision to confirm: successful command completion requires an explicit zero exit status. Recommended: classify a
completed command without that status, including a remote exit signal, as Repository and retain available signal
and error details. This reports the incomplete command outcome without guessing why the channel closed.
Alternative: classify an unexplained channel close as Connection, inviting a connectivity retry but inferring a
transport cause without evidence. Neither option allows success without explicit exit zero.

Decision to confirm: how Sync judges a command when sending EOF fails, and which failure an operation reports when
several arrive. Recommended:

- When sending EOF fails with a plain fault, such as a channel the host closed after writing its refusal, Sync still
  waits for the exit report within the graceful-completion wait below. It skips that wait when the caller cancelled
  or the stalled-network timeout already expired. It then judges the command as the two paragraphs above say,
  including a finished command without an exit status. Only a command that doesn't report within the wait is a
  Connection failure. So the same refusal gets the same failure, whatever the timing.
- An operation reports the first failure it records, with one exception: a Connection failure guessed from a plain
  I/O fault, which the host didn't explain, gives way to a later Repository or Authentication failure that Sync read
  from the host's own output or exit status. Nothing else replaces a recorded failure, not even another guess.

Alternative: always keep the first failure, and end the command at the EOF fault. The same host refusal then shows
as Repository or as Offline, depending on timing.

Decision to confirm: what Sync reports when the host refuses to start a command at all. The pinned SSH.NET
2026.0.0 ignores that refusal and leaves the channel open, so Sync can't tell it from a stalled network.
Recommended: treat it as a stall, a Connection failure at the 30-second stalled-network timeout below. Alternatives:
read SSH.NET's private channel by reflection to see the refusal at once, which can break silently on an SSH.NET
update; or replace SSH.NET, which is a new dependency.

Creation validates arguments only; it performs no file, Git, or key-store I/O. Its initial setup snapshot can name
the already selected folder and default device name from memory, but it does not yet know the saved origin,
device name, key, or configured receipt. Its initial false `IsConfigured` is not a completed setup check.

On the first `Start`, the service retains one asynchronous initialization operation. The owned worker loads those
local values and publishes the first setup snapshot before the service requests its initial network sync.
Publication happens even with no selected clone or key, and even when the visible values equal the initial ones.
An initialization failure publishes the existing Error status, so a consumer waiting for initialization can show
Configure and the error instead of waiting forever. No new readiness API or synchronous wait is needed.
Repeated starts do not initialize again. This startup order is decided, not permission to move I/O into creation.

`Start` returns without waiting for initialization or network work. A manual pass or setup operation can still run
before `Start`, as Android background work needs; its I/O also belongs to the owned worker. Status and setup getters
read immutable cached snapshots. Notices may arrive on any thread; UI consumers dispatch them. No notice runs under
Tasks' lock.

Cancelling a manual sync caller stops only its wait. The service still owns and observes the shared pass.
Cancelling a setup call requests cancellation of that operation, whose completion the service also retains.
`StopAsync` permanently stops timers, rejects new work, cancels owned operations and waiters, and joins initialization, the worker,
signing handles, and transport cleanup. Cancelling its caller stops only the join, not shutdown.
`DisposeAsync` joins the same shutdown without caller cancellation. App awaits shutdown through its async exit
path; it must not add a blocking bridge to Shell's synchronous lifecycle.

Decisions to confirm: this one-worker lifetime and terminal stop; a 30-second connection/stalled-network timeout;
and up to 5 seconds for graceful command completion after sending EOF, before transport interruption. Cancellation
signals interruption without doing blocking cleanup on the UI thread. Shutdown is complete only after its work
has actually ended, not merely after a cancellation request or timeout was issued.

The pinned SSH.NET connection setup cannot be interrupted by disconnecting a session before `Connect` publishes
it. Cancellation is checked before and after that synchronous call on the worker; a native connection stage
already in progress may run until its configured connection timeout. That is not an end-to-end cancellation
deadline or a promise of immediate interruption. Do not bridge `ConnectAsync` back to a synchronous callback or
abandon the connection attempt. Once connected, owned cancellation is registered before command startup, whose
synchronous channel-opening work must also be interruptible. The outer cleanup joins every started command and
interruption task, including those from a startup or constructor that fails.

### Sync reads only its clone's own git settings

**Decided by the user:** Sync ignores the git configuration levels outside its clone: the system, global, and XDG
(`~/.config/git`) levels, and on Windows the ProgramData level. What Sync does then depends only on the clone itself:
its own `.git/config`, with any files that config includes or names, and the repository's files.

libgit2 reads those outer files as installed git does. On one PC, the system file set `core.autocrlf=true` with
`core.safecrlf=true`. libgit2 then refused to stage files with the `\n` line breaks
[Tasks writes](tasks.md#each-task-file-holds-nine-fields), with "LF would be replaced by CRLF", so every sync failed.
Other outer settings can change Sync too: `url.<base>.insteadOf` can send the origin's address to a transport Sync
doesn't own, and a global ignore file can hide `tasks/`.

The table lists what Sync could take from the outer files, and where it comes from now.

| Setting | Where Sync gets it now |
| --- | --- |
| Commit author: `user.name` and `user.email` | Sync names the device on each commit it makes ([ManagedCommit](../../src/Shared/Sync/Git/ManagedCommit.cs), [DomainMerge](../../src/Shared/Sync/Git/DomainMerge.cs)). The log lines libgit2 adds on its own under `.git/logs` name `unknown`; nothing reads them. |
| First branch: `init.defaultBranch` | Sync takes the remote's default branch, or `main` on an empty remote ([GitPass](../../src/Shared/Sync/Git/GitPass.cs)). |
| Line endings, attributes, and ignore rules | Only the clone's own `.git/config` and any attribute or ignore file it names (`core.attributesfile`, `core.excludesfile`), the `.gitattributes` and `.gitignore` files in the clone, committed or not, and the clone's own `.git/info` files. Sync writes none of these settings, so without them files travel byte for byte. |
| Trusted folders: `safe.directory` | Nowhere. libgit2's [own ownership rules](https://github.com/libgit2/libgit2/blob/5853918c4c6a7b12f8becf4bd11ff4362ebb9020/src/libgit2/repository.c#L645-L650) then decide which clone owners it accepts: mainly the current user; see [Failure modes](#failure-modes). |
| The receipt and the origin | Unchanged: they already live in the clone's own `.git/config`. |

Settings libgit2 never uses, such as proxies, `core.sshCommand`, hooks, and commit signing, change nothing either
way: Sync uses [its own SSH transport](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport),
and libgit2 runs no hooks and signs nothing.

Proposed, each a decision to confirm:

- **Sync empties the four levels' search paths right before each git operation**, on the owned worker: reading or
  writing the receipt, reading the origin, using an existing clone, cloning, and each sync pass. The setting is
  shared by the whole process ([GlobalSettings](https://github.com/libgit2/libgit2sharp/blob/0.32.0/LibGit2Sharp/GlobalSettings.cs#L334-L338)),
  so Sync sets it every time rather than trusting that nothing changed it since. That costs four native calls.
  Creation still runs no git code.
- **An empty search path means "no file at this level"**, not a path to an empty folder. libgit2 then finds no file
  there ([sysdir.c](https://github.com/libgit2/libgit2/blob/5853918c4c6a7b12f8becf4bd11ff4362ebb9020/src/libgit2/sysdir.c#L548-L560)),
  including the attribute and ignore files it looks for in the same places
  ([common.h](https://github.com/libgit2/libgit2/blob/5853918c4c6a7b12f8becf4bd11ff4362ebb9020/include/git2/common.h#L277-L280)).
- **Sync keeps libgit2's ownership check on**, with no list of trusted folders of its own.
- **Sync still follows the files the clone's own `.git/config` includes or names**, such as `include.path`,
  `core.excludesfile`, and `core.attributesfile`, even outside the clone. Sync never writes these settings; only
  someone editing the clone by hand adds them, and that is their choice.

If emptying a level fails, the operation fails with that error; Sync never runs git with only some levels ignored.
The Android app runs the same code with the same libgit2, so it needs nothing more.

### Device metadata has one owner and never travels in task commits

The app remains the only owner of the selected path. Sync receives a small
[selection port](../../src/Shared/Sync/IRepositorySelection.cs) implemented beside App's composition root.
It adapts the existing selection operation: persist `repository-folder.txt` once and load that same folder into
Tasks. Sync does not write another selected-path setting. Initial loading follows the
[approved initialization boundary](#public-interface); subsequent setup selections go through Sync's serialized
operation. Git and SSH stay inside Sync, not in this adapter.

**Decided by the user:** the three device-local storage contracts and configured-receipt binding below.
Other implementation choices remain subject to confirmation.

- `device-name.txt` in the supplied device folder holds the chosen single-line name. An absent file uses the
  app-supplied machine name without writing; an unreadable file fails rather than reverting to that default.
  Decision to confirm: a present saved name that fails the setter's existing single-line/nonblank validation
  raises InvalidDataException and preserves the file. Only an absent file uses the default; corrupt content is
  never silently replaced. Loading reuses that validation rather than inventing another name policy.
  Decision to confirm: a failed saved-name read or validation publishes an empty `DeviceName` to show that the
  name is unknown, not the machine-name default. A successfully loaded name remains visible when an unrelated
  setup field fails to load.
  Commits use that name and `device@memoria.invalid` as their author identity. The public-key comment uses the name
  too, without regenerating the key when the name changes.
- The selected clone's local Git configuration holds `memoria.lastSuccessfulSync` as an ISO-8601 UTC time and
  `memoria.deviceKeyFingerprint` as the successful device key's SHA-256 fingerprint. Together these are the
  configured receipt; a different key or clone needs a first successful writable sync. The origin remains the
  only stored repository address. Changing it through setup clears the receipt. These entries are local Git
  metadata, not files that Sync commits.
- `known-hosts.json` in the device folder is an object with `version: 1` and `hosts`, an array of objects with
  `host`, `port`, and `fingerprint`. Custom trust is scoped to the normalized host and port. Unknown versions,
  malformed entries, or read failures stop trust loading rather than silently trusting or replacing anything.
  A failure to load custom-host trust keeps its cause, and the [rule for naming failures](#failure-modes) gives
  its kind. This adds no recovery UI or replacement of the trust file.
  **Decided by the user:** while that load fails, setup still shows the key the host offered, with its host, port,
  algorithm, and fingerprint, and never allows accepting it. It looks like a changed key, and the user can compare
  the fingerprint with the host's.
  Acceptance writes only the exact pending unknown custom-host key. A changed trusted key must first be forgotten;
  built-in pins can neither be forgotten nor accepted through setup.
  Decision to confirm: using Forget for a built-in host refuses the request with an explanation through the
  existing setup action, rather than silently succeeding. No new eligibility API is needed.
  **Decided by the user:** Forget for a custom host and port with no saved trust succeeds and changes nothing,
  so pressing it twice, or before Accept, is harmless. Afterwards nothing is trusted for that host and port,
  which is what Forget promises.
  Decision to confirm: Forget with a missing or blank host, or a port outside 1 to 65535, is an invalid argument.
  It fails before any work, like the other setup actions' invalid arguments, and shows no error status.

The reviewed [built-in pin data](sync-host-keys.json) is embedded directly by Sync's project, not copied into
another source file. Each host's official source and verification date live beside its fingerprints. DNS names
compare without case or a trailing dot; a built-in name still requires a published pin when an explicit port is used.
The [Windows encryption envelope](#the-windows-key-file-is-encrypted-and-only-the-users-account-unlocks-it) belongs
to App's secure-store adapter, not to this module's device metadata.

## Public interface

The declarations are in `src/Shared/Sync/`. Git repositories, transport clients, worker internals, and native
library types are not public. The [module map](modules.md#module-map) already permits Sync to use Tasks; no map
or platform change is needed.

| Contract | Caller and purpose |
| --- | --- |
| [`ISyncService`](../../src/Shared/Sync/ISyncService.cs) | Shell, Task list, and Configure: lifecycle, manual sync, status, and setup |
| [`ISyncStatus`](../../src/Shared/Sync/Status/ISyncStatus.cs) | Immutable status and failure kind, successful-sync time, pending changes, and Tasks' skipped-file reports |
| [`ISyncSetup`](../../src/Shared/Sync/Setup/ISyncSetup.cs) | Configure: device name, key generation, clone or existing-clone selection, and explicit custom-host trust |
| [`ISyncSetupState`](../../src/Shared/Sync/Setup/ISyncSetupState.cs) | Immutable setup state, including the selected clone, public key, and host's add-key page |
| [`ISshHostKey`](../../src/Shared/Sync/Setup/ISshHostKey.cs) | The endpoint and fingerprint actually observed, including whether acceptance is allowed |
| [`IDeviceKeyStore`](../../src/Shared/Sync/DeviceKeys/IDeviceKeyStore.cs) | App's synchronous secure-store adapter, called only on the owned worker |
| [`IDeviceSigningKey`](../../src/Shared/Sync/DeviceKeys/IDeviceSigningKey.cs) | An owned signing handle: public blob and RSA SHA-2 signing, never private-key export |
| [`IRepositorySelection`](../../src/Shared/Sync/IRepositorySelection.cs) | App's adapter over the single existing selected-path owner |

**Decided by the user:** the selected-folder adapter's constructor only validates and stores dependencies.
Sync calls its synchronous `Load()` on the owned worker during initialization, before refreshing or publishing
setup. App reads its saved selection directly, treating only FileNotFoundException as an absent selection;
other read or adoption failures reach Sync's existing initialization failure boundary. That boundary publishes
Error and the failed setup snapshot so Shell can open Configure, rather than terminating before Shell exists.
App retains ownership of reading and adopting the selection; no second selected-path store is added.

The signer accepts the original SSH data, hashes and signs it once with RSA PKCS#1 v1.5, and returns a raw signature.
Only Sync adds the SSH signature wrapper and public-key comment. This works with a non-exportable Android key.
Each pass disposes the signer it opens; generation disposes its returned handle after reading the public blob.
No decrypted signing handle stays open while the service is idle.
Transport registration is process-wide, but each callback must resolve its own operation's immutable credentials
and trust context; never reuse the proof's mutable global "current key" or host-pin dictionary.

The implementation adds this one concrete construction entry point; the contracts checkpoint adds no throwing
factory stub or unusable partial method:

```csharp
ISyncService SyncService.Create(
    ITaskSynchronization tasks,
    IFileSystem files,
    IDeviceKeyStore keys,
    IRepositorySelection repositories,
    TimeProvider time,
    string deviceFolder,
    string defaultDeviceName,
    TimeSpan? periodicInterval)
```

App supplies and wires all arguments. Creation validates arguments and creates the service, with no I/O or worker
started. The interval is positive or null: App supplies 30 minutes on desktop and null on Android. There is no public
Git adapter, host-pin override, worker setting, or test-only construction switch. Setup rejects unsupported SSH
addresses and invalid names or paths before side effects. It never runs installed Git to perform client operations.
Windows absolute drive paths are local paths, not SSH addresses; rejecting them must not exclude valid
single-character SCP hostnames.

Every accepted address uses Sync's SSH transport for clone, remote discovery, fetch, and push, even when the
address also names an existing local directory on Windows. Local transport is never a fallback.

**Decided by the user:** before any Git operation, make the already-resolved SSH username explicit in every
bare single-letter SCP host address. Preserve the host spelling and remote path exactly. Persist this equivalent
origin spelling, including for existing origins; ordinary addresses stay unchanged. This is the sole exception
to retaining the user's origin spelling, and does not depend on whether a matching local directory exists.
The normalized origin is permanent, not a temporary URL toggle. No second stored address, public interface, or
dependency is added. Existing unsafe-state checks still run before changing an existing repository's origin.

`UseExistingAsync` can reuse the currently selected clone; it validates before changing its origin or selection.
It contacts no host, so it doesn't check push access. `CloneAsync` checks push access right after cloning, removes
what it cloned when that check fails, and selects only after both succeed. Neither operation counts as the first
successful writable sync: Configure then awaits `SyncNowAsync`, which verifies push access even when there are no
new local commits.
A failed first sync leaves the selected clone available for offline work and setup retry. A detached HEAD,
non-default branch, or unfinished Git operation is reported before changing task files or making commits, rather
than silently switching branches or discarding work. This safety refusal is a decision to confirm.

The approved supporting [Tasks contracts](tasks.md#step-5-publishes-small-capabilities-without-growing-task-operations)
give Sync its exclusive local updates, canonical managed paths, pure merge rules, skipped-file reports, and a
local-save notice distinct from imported changes. Only local commit and merge application hold Tasks' lock.
Fetch, push, setup connections, and every asynchronous network wait run outside it.

Setup keeps the clone the user already configured; selecting a folder is not an alternative to Git synchronization.
The existing device-local `repository-folder.txt` remains the only persisted selected-path setting, as
[Configure](desktop/configure.md#the-sync-settings-section-sets-the-devices-clone) records. Sync's implementation
must commit, fetch, merge, and push through the configured repository rather than require a replacement clone.

### Implementation and focused checks

The never-merged proof is `proofs` at `773bda795e4334dd2d64ad70bd1673f9b521a44b`:
`proofs/Shared/SshTransport.cs`, `SshAddress.cs`, `OpenSshRsa.cs`, and `GitScenario.cs`; and
`proofs/Windows/TestSshServer.cs`. Read it with `git show`, not a merge or checkout over active work.
Keep its two address forms, RSA SHA-2 behavior, and EOF-before-close finding. Do not copy its blocking async bridges,
global credential state, incomplete host pins, or proof-only key envelope.

[`Directory.Packages.props`](../../Directory.Packages.props) is the sole version home. Sync references LibGit2Sharp
and SSH.NET; only the [shared test project](technology-stack.md#each-part-keeps-its-tests-in-one-test-project)
references the in-process SSH server package. The project inherits the existing target framework, analyzers, and
test-kind policy. Its internal Git port stays internal; Sync's tests can exercise internal pure policies and the
worker, as [Modules](modules.md#a-module-is-one-capability-behind-a-small-interface) allows, without exposing those
internals to App or another module.
The shared fake assembly can implement that port and the public device adapters, so a fake still has only one home.

The Sync tester starts with unit scenarios for address rejection, immutable status, scheduling/coalescing,
caller cancellation, stop/dispose ownership, trust changes, and key-handle lifetime. Use completion signals,
not sleeps. Real Git/SSH, writable-key checks, branch safety, complete merge/push round trips, and interruption
of stalled transport are integration scenarios, written upfront but run only at session-end validation.
Keep shared port fakes in the [existing fake project](technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test).

Integration tests check that Sync [ignores the git configuration levels outside its clone](#sync-reads-only-its-clones-own-git-settings),
since only real libgit2 shows it. Each test brings its own bad outer settings, so it checks the same thing on a
machine with clean settings as on a PC whose own settings break Sync:

1. Connect a device first. Sync has then already run git, so isolation applied only once would miss what follows.
2. Write the test's settings into a temporary folder, as `gitconfig`, `.gitconfig`, and `config`, the file names
   libgit2 looks for at the four outer levels. Point all four levels at that folder.
3. Run the step under test. Then, even when the test fails, reset the four levels to libgit2's defaults and delete the folder.

The table lists each test.

| Step under test | Outer settings | Passes when | Without the isolation |
| --- | --- | --- | --- |
| A sync pass | `core.autocrlf=true` and `core.safecrlf=true` | It pushes a new task, and `settings.json` on an empty origin, with their `\n` line breaks unchanged | The pass fails with "LF would be replaced by CRLF" |
| A second device's clone | The same pair | Its files match the origin's byte for byte | Its files have `\r\n` line breaks |
| A setup refresh, then a pass | `url.<base>.insteadOf`, rewriting the origin's address to one Sync rejects | Setup shows the saved address, and the pass succeeds | Setup shows the rewritten address, and the pass fails |

These tests change libgit2's process-wide settings, so they share one xUnit collection with parallelization turned
off. xUnit runs such collections after the others, one at a time, with no other test running
([xUnit maintainer](https://github.com/xunit/xunit/discussions/3206)). Sync's other tests only ever set empty paths,
so they still run side by side. To prove these tests can fail, at final validation remove the call that empties the
levels, and see the last column's failures.

Desktop tests reuse one fake `ISyncService`/setup implementation in `tests/Shared/Fakes`, rather than one per window.
It supplies immutable controllable snapshots, any-thread notices, and completion signals for sync/setup/stop,
with the same caller-cancellation and shared-pass ownership semantics as the public contract. It performs no real
I/O and requires no production-only fake hooks. Desktop's existing fake project supplies Configure's setup actions,
extends its tray fake for badges/notifications, and adapts lifetime fakes to async stop. Tests update their factories
to the new required arguments; no old constructor or sync-less overload remains just to keep a fixture compiling.

Focused build: `dotnet build src\Shared\Sync\Memoria.ToDo.Shared.Sync.csproj`.
After the tester adds unit cases, run Sync's unit tests as a
[focused batch](technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test):
`dotnet test --project tests\Shared\Tests\Memoria.ToDo.Shared.Tests.csproj --filter-namespace "Memoria.ToDo.Shared.Sync.Tests*"`.
At main-session final validation only, run the full commands from the
[test workflow](technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-test),
including the architecture integration checks and the agent-file generator checks required by `AGENTS.md`.

**Validation backlog**

| ID | Deferred check | Boundary |
| --- | --- | --- |
| SYNC-SSH-LETTER-COLLISION | An actual SSH clone, remote discovery, fetch, and push for a normalized single-letter SCP host while the original bare address also names an existing Windows directory | Needs an isolated Windows fixture with a reachable single-letter DNS host and controlled SSH server, without changing this machine's hosts file, DNS, or process/drive current directory. Address-policy unit checks, native origin-persistence checks, and pinned native routing analysis do not prove this network round trip. |
| SYNC-INITIALIZATION-PUBLICATION | Deterministically prove that failed initialization publishes Error and its setup snapshot before a queued pass proceeds, and that Stop joins the failed initialization through publication | The current public ports lack the ordering signal needed to force these interleavings. Source review covers the owned operation and initialization-task join; ordinary initialization-failure and shutdown tests do not prove these specific races. No production test-only API is added for this deferral. |

## Failure modes

The table lists each failure, whether it shows as offline or as an error, and what the user sees. Only some rows
are decided; the list after the table says which.

| Failure | Shown as | What the user sees |
| --- | --- | --- |
| The host can't be reached | Offline | The offline badge; sync retries |
| The host rejects the key | Error | Configure says to add the public key on the host, with write access |
| The host key changed | Error | No sync can succeed until the [host key](#the-app-checks-each-host-key-against-its-own-list) is updated; retries follow the [existing backoff](#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) |
| The repository isn't found | Error | Configure shows the address it tried |
| The push is rejected 3 times in a row | Error | Sync retries later |
| A task file can't be read, or has a newer `version` | Error; the rest of the sync completes | The status names the file, as [Tasks](tasks.md#failure-modes) says |
| The clone can't be written, such as on a full disk | Error | The error's details; sync retries |
| The clone's folder belongs to an account libgit2's ownership check doesn't accept | Error | The details say the current user doesn't own the folder. Clone again in Configure, or make the current user the folder's owner |
| A failure Sync doesn't recognize, such as a dependency disposed while the service isn't stopping | Error | The error's details; sync retries with the [existing backoff](#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) |

On the desktop, being offline and errors show as [Shell](desktop/shell.md#sync-stays-quiet-unless-something-is-wrong) decides.

Which rows are decided:

- **Decided by the user:** the last row, with the rule below for naming failures.
- **Decided in the designs the rows link to:** a changed host key stops sync with an error, as
  [host keys](#the-app-checks-each-host-key-against-its-own-list) say. A task file that can't be read, or has a
  newer `version`, is skipped and named in the status, as [Tasks](tasks.md#failure-modes) says.
- **Proposed, each a decision to confirm:** only an unreachable host shows as offline, and every other failure as
  an error. Also what the user sees when the host rejects the key, the repository isn't found, the push is rejected
  3 times in a row, the clone can't be written, or libgit2's ownership check doesn't accept the clone's owner.

**Decided by the user:** Sync names each failure by its cause, the same way in a sync pass, at startup, in a setup
action, and in the setup refresh that follows any of them. So one broken file shows one kind wherever it's found,
and the tray notifies once for it. The table's last row shows how an `Unexpected` failure appears.

- A Git or host failure keeps the kind Sync's git code gave it, as
  [the owned worker's section](#one-owned-worker-keeps-synchronous-git-and-ssh-off-the-ui-thread) describes.
- A file, access, or encryption failure on this device, the clone included, is `LocalStorage`. So is saved data on
  this device that can't be used, such as a corrupt device name, key, or trust file, or a malformed saved selection.
- Anything else is `Unexpected`, including a dependency disposed while the service isn't stopping. It's a fault in
  the app, and the app keeps running, as the [Task list](desktop/task-list.md#ruled-out) decided.

Decision to confirm: once the service has begun stopping, through `StopAsync` or disposal, a pass failure of any
kind is part of the stop. The service publishes no failure status for it and schedules no retry, so the
[naming rule](#failure-modes) above doesn't apply. The caller waiting on that pass sees the same end as a call made
after the service stopped. During shutdown, a dependency can be disposed under a running pass, and the host-key
check or the signing step wraps that in a failure of another kind. Recommended: this, so the user sees no false
error as the app closes. Alternative: count only a cancellation or a disposed dependency as part of the stop; a
wrapped failure hides that cause, so the false error would still show.

Decision to confirm: if stopping begins in the instant between a pass's stop check and its failure being published,
that failure can show briefly. The pass still ends as a stop and the status from before it comes back. Recommended:
accept this, because closing the gap needs one lock shared by stopping and every status change. Alternative: add
that lock, so no failure can show once stopping begins.

**Decided by the user:** a failed sync still shows the local changes it found that haven't reached the remote.
When a sync fails at or after its first contact with the host, for example offline, at the stalled-network timeout,
or when the host refuses the push, it shows pending changes if the clone holds uncommitted changes to tasks, images,
or settings, or commits the remote doesn't have as last fetched or pushed. A sync whose Git work finished but that
then failed, such as when its receipt can't be written, shows what that work found.

Decision to confirm: when the Git work fails and closing its session then fails too, such as when the key handle
can't be closed, the sync reports the closing failure and shows the pending value the Git work found. Recommended:
this, which keeps which failure is reported unchanged. Alternative: report the Git work's failure and append the
closing failure's details, as a failed setup refresh does below; that carries both failures out of the session.

Decision to confirm: a sync that fails earlier, in its local checks, keeps the pending value shown before. Those
checks cover an unusable or rejected folder, a missing or unreadable key, a folder that isn't a Git repository, a
missing or unusable origin, and unsafe repository state. Recommended: keep that last known value, with no extra
scan of the clone. Alternative: scan the clone after every failure; it reads a clone that may be the problem, and
the scan can fail too.

A setup-snapshot refresh failure never replaces a pass's primary failure kind or details: append the refresh
details while retaining that outcome. If the pass otherwise succeeded, report the refresh failure, with its cause's
kind, instead of a success-shaped fallback. Unsafe repository state is a Repository failure detected before setup
changes origin or selection and before a pass changes managed files or commits.

Decision to confirm: when a Git merge and the subsequent Tasks reload report the same managed path, show the
path once and retain the merge-specific reason rather than replace it with the reload reason. Reports for other
paths remain; reason priority does not suppress Tasks' newer-version indication.

A malformed saved repository selection is a LocalStorage failure, not invalid user input to a setup action.
Keep the saved selection unchanged and report the failure through status; automatic requests must observe it
rather than leave a background task fault unobserved. User-supplied invalid setup arguments keep their existing
argument-error behavior.

Decision to confirm: when a cancelled clone also fails to remove its partial files, setup reports an IOException
classified as LocalStorage, retaining the cancellation and cleanup failure together in an AggregateException.
Its details warn that some cloned files may remain in the folder. Cancellation without a cleanup failure still
reports cancellation; a cleanup failure must not look like a successfully cancelled, fully cleaned-up operation.

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
  - **Silently succeeding when Forget targets a built-in host**: it looks as though trust was removed when it was not.
  - **Adding an eligibility capability only to hide that action**: it grows the public interface for a request the
    existing action can refuse with an explanation.
  - **Hiding the offered key when the trust file can't be loaded**: the user would lose the fingerprint the host
    offered.
  - **Refusing Forget for a host and port with nothing saved**: pressing it twice, or before Accept, would show an
    error for a harmless request.
- Desktop timing:
  - **Every 5 minutes**: the user chose 30.
  - **Every minute**: about 1,400 contacts with the host a day.
  - **Only at start and when View Tasks opens**: the tag chips and task references in Add would go stale.
- Local work in the two discard cases above:
  - **Stop sync and preserve the local bytes and index**: the user chose to allow the remote result instead,
    accepting possible loss rather than blocking the pass for manual repair.
  - **Implement index preservation or quarantine/recovery now**: the user chose not to add that scope.
- Single-letter SCP host addresses:
  - **Reject the bare form and require an explicit username**: the user chose to keep accepting it and save the
    equivalent explicit-username spelling instead.
  - **Add native transport machinery to preserve exact origin spelling**: it adds a native maintenance boundary
    that the approved normalization avoids.
- A cancelled clone whose cleanup also fails:
  - **Report only cancellation**: the caller can overlook partial files that could not be removed.
  - **Report only the cleanup failure**: it loses the cancellation that caused cleanup to run.
- Pending changes after a failed sync:
  - **Keep the previous value on every failure**: a sync that found new local work and then failed would say
    nothing is waiting to be sent.
- Git settings outside the clone:
  - **Setting `core.autocrlf=false` in each clone Sync manages**: the user rejected it, since it fixes only that
    setting. Others, such as `core.safecrlf`, `core.eol`, `url.<base>.insteadOf`, or a global ignore file, could
    still change what Sync does.
  - **Ignoring them once, when Sync's code loads**: that runs on the UI thread when the app creates Sync, and a
    failure there breaks the whole app, not one sync. .NET also warns against module initializers in libraries
    ([CA2255](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2255)).
  - **Ignoring them once per process, at the first git operation**: a later change to the shared setting, by the app
    or a test, would go unnoticed.
  - **Pointing each level at an empty folder**: it costs disk access, and someone could put a file there.
  - **Turning off libgit2's ownership check, so `safe.directory` isn't missed**: it reopens the attack the check
    blocks ([CVE-2022-24765](https://nvd.nist.gov/vuln/detail/CVE-2022-24765)).
- Failure kinds:
  - **A sync pass naming some local failures `Unexpected`, while startup and setup call them `LocalStorage`**: one
    broken file would show two kinds, and the tray would notify twice.
  - **Naming only a failed setup refresh after a successful pass by its cause**: a corrupt device name or key file
    would still show two kinds.
