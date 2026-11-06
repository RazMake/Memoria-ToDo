# Configure: where the Android app's settings live

Status: Decided

Configure is the Android app's settings screen. It sets up sync with the phone's own key, changes the deletion delay every device shares, holds the swipe direction, and shows how sync is doing.

## Behavior

### Each setting has one owner

Proposed: Configure holds the settings in the table below, plus sync status and Sync now. This is a decision to confirm. The table lists each setting, the module that owns it, where it's kept, and its default:

| Setting | Owner | Kept | Default |
| --- | --- | --- | --- |
| The repository's SSH address, the device name, the phone's SSH key, and the accepted host keys | Sync | On this phone | Proposed: the device name starts as the phone's name. The rest is set up as [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) says. |
| Days before a resolved task is deleted | Tasks | In the repository, shared by every device | One week, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides |
| Which swipe resolves a task | Configure | On this phone | Swipe right, as the [Task list](task-list.md#swipes-resolve-and-delete-and-a-long-press-moves-a-task) decides |

Configure changes the deletion delay and sets up sync as on the [desktop](../desktop/configure.md#each-setting-has-one-owner).

Proposed, each a decision to confirm:

- The swipe direction is this phone's setting and never syncs.
- This phone's settings, the clone, and the trusted host keys live in the app's private storage, which Android removes when the app is uninstalled.
- Android's Auto Backup is off for the app. Otherwise Android copies app data to the user's cloud backup by default ([Auto Backup](https://developer.android.com/identity/data/autobackup)), which would put every task there, and restore a clone without its key.

### The key is made inside the Android Keystore and never leaves it

Proposed: Generate makes the phone's RSA 3072-bit key, the type [Sync through git](../sync.md#each-device-signs-in-with-its-own-rsa-key) decides, inside the Android Keystore, which never lets it out. This is a decision to confirm.

The key lives in the phone's trusted execution environment (TEE), the protected part of its main processor. StrongBox, the separate security chip some phones have, holds RSA keys of 2048 bits only ([Android Keystore](https://developer.android.com/privacy-and-security/keystore)).

Proposed: Configure shows the public key with Share and Copy buttons, so the user can send it to the computer where they add it on the git host. This is a decision to confirm.

The rest of the setup follows Sync's steps: the host key check, the clone, and the first sync.

### Configure shows how sync is doing

As on the [desktop](../desktop/configure.md#configure-shows-how-sync-is-doing), Configure shows the last successful sync time, the files sync skipped, and the error's details while sync fails. Its Sync now button starts a sync at once.

## Public interface

Shell uses these parts:

- **Show Configure**: opens the settings screen.
- **Whether the phone is configured**, from Sync's setup.
- **This phone's settings**: the swipe direction, with a notice when it changes. Shell hands it to the Task list.

## Failure modes

- **The Keystore can't make the key**: Configure shows the error, and the phone stays unconfigured.
- **Setting up sync fails**, such as when the host rejects the key: Configure shows the error, as [Sync through git](../sync.md#failure-modes) lists, and the phone stays unconfigured until setup succeeds.
- **The settings can't be read**: Configure says so. The app runs with the default settings, and leaves the stored settings as they are until the user saves.

## Ruled out

None of its own. The settings Configure shows were chosen elsewhere, with their rejected options: the sync settings in [Sync through git](../sync.md#ruled-out), the deletion delay in [purpose and scope](../purpose.md#ruled-out), and the swipes in the [Task list](task-list.md#ruled-out).
