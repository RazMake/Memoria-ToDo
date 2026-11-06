# Configure: where the desktop app's settings live

Status: Decided

Configure is the desktop app's settings window. It holds this device's own settings, changes the deletion delay that every device shares, and sets up sync and shows its status, which Sync owns.

## Behavior

### Each setting has one owner

The table lists every setting in Configure, the module that owns it, where it's kept, and its default:

| Setting | Owner | Kept | Default |
| --- | --- | --- | --- |
| Global shortcut for Add | Configure | On this device | Win+Alt+T, as [Shell](shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions) decides |
| Start with Windows | Configure | On this device | Proposed: on |
| Days before a resolved task is deleted | Tasks | In the repository, shared by every device | One week, as [purpose and scope](../purpose.md#resolved-tasks-stay-for-a-week-then-are-deleted) decides |
| The repository's SSH address, the device name, the device's SSH key, and the accepted host keys | Sync | On this device | The device name starts as the machine's name; the rest is set up as [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) says |

Configure reads and changes the deletion delay through Tasks, so a change on one device reaches every device; [Tasks](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares) sets its allowed range. Configure sets up sync through Sync, in the steps that [Sync through git](../sync.md#configure-sets-up-a-device-by-cloning-the-repository) lists.

Proposed, each a decision to confirm:

- This device's settings live in a file of their own under `%LocalAppData%`, outside the clone and the install folder, and never sync.
- The deletion delay shows in days.
- Configure checks each value as it's entered, and saves nothing invalid.

### A new shortcut is checked at once

When the user picks a new shortcut, Configure saves it and Shell registers it with Windows. If another app holds it, Configure says so and lets the user pick another. Proposed: the previous shortcut stays active until a new one registers. This is a decision to confirm.

### Configure shows how sync is doing

Configure shows the last successful sync time, the files sync skipped, and, while sync fails, the error's details; the tray shows the same in short, as [Shell](shell.md#sync-stays-quiet-unless-something-is-wrong) says. Configure reads them from Sync. Its Sync now button starts a sync at once, as [Sync through git](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes) decides.

## Public interface

Shell uses these parts:

- **Show Configure**: opens the window, or brings it forward.
- **This device's settings**: the shortcut and Start with Windows, with a notice when either changes. Shell applies them.
- **Report a setting's result**: Shell reports whether the shortcut registered and whether the `Run` key was written, and Configure shows any failure.
- **Whether the device is configured**, from Sync's settings.

## Failure modes

- **The shortcut is taken**: Configure says so, and the previous shortcut stays active.
- **The settings file can't be read**: Configure says so. The app runs with the default device settings, and leaves the file as it is until the user saves.
- **A setting can't be applied or saved**, such as a failed write to the `Run` key: Configure shows the error and keeps the previous value.
- **Setting up sync fails**, such as when the host rejects the key or the repository isn't found: Configure shows the error, as [Sync through git](../sync.md#failure-modes) lists, and the device stays unconfigured until setup succeeds.

## Ruled out

None of its own. The settings Configure shows were chosen elsewhere, with their rejected options: the shortcut in [Shell](shell.md#ruled-out), the deletion delay in [purpose and scope](../purpose.md#ruled-out), and the sync settings in [Sync through git](../sync.md#ruled-out).
