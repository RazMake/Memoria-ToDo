# Shell: where each way into the Android app leads

Status: Decided

Shell is the Android app's entry point. It decides where the app icon, the app shortcut, and the home-screen widget lead, runs sync while the app is open and in the background, checks for updates, and sets the theme. It opens the [Task editor](task-editor.md), the [Task list](task-list.md), and [Configure](configure.md).

## Behavior

### Four ways lead into the app

The user chose four ways into the app. The table lists each and where it leads:

| Way in | Where it leads |
| --- | --- |
| Opening the app | View Tasks if the phone is configured, otherwise Configure, as [purpose and scope](../purpose.md#both-apps-offer-add-new-task-view-tasks-and-configure) decides |
| The floating Add button in View Tasks | Add. The [Task list](task-list.md) owns the button. |
| The app shortcut "Add task", from a long press on the app icon | Proposed: Add, directly |
| The home-screen widget | Its Add button opens Add. Proposed: a task in it opens View Tasks with that task expanded. |

The shortcut is a static shortcut, declared with the app, so it works from the first install ([app shortcuts](https://developer.android.com/develop/ui/views/launch/shortcuts/creating-shortcuts)).

Proposed, each a decision to confirm:

- The app shortcut opens Add directly.
- Until the phone is configured, every way in opens Configure, as on the [desktop](../desktop/shell.md#a-global-shortcut-and-the-tray-menu-open-the-apps-three-functions).

### The widget shows the top tasks and an Add button

The home-screen widget has an Add button and shows the top tasks. Proposed, each a decision to confirm:

- It shows the top 5 open tasks in rank order, from the [Task list](task-list.md#public-interface), with titles in plain text.
- Tapping a task opens View Tasks with that task expanded.
- It updates after each sync and each change: Shell redraws it whenever the top tasks change ([widget updates](https://developer.android.com/develop/ui/views/appwidgets/advanced)).
- Until the phone is configured, or when the tasks can't be read, it shows only the Add button.

### Sync runs while the app is open, and every few hours in the background

The Android app isn't meant to always run, so it syncs when it opens, soon after a change, and in the background. The table lists what starts a sync on the phone; each runs the same [sync as on the desktop](../sync.md#each-sync-commits-fetches-merges-and-pushes-one-branch):

| When | What happens |
| --- | --- |
| The app opens | A sync starts, to pull other devices' changes |
| The user pulls View Tasks down | A sync starts at once, as the [Task list](task-list.md#pulling-the-list-down-starts-a-sync) decides |
| A change is saved | A sync starts about 5 seconds later |
| The user leaves the app with changes not yet pushed | Background work pushes them when the network returns |
| Every few hours | A background sync, only on a network and not in battery saver |

Background work runs through WorkManager, Android's library for work that must finish after the app closes ([WorkManager](https://developer.android.com/develop/background-work/background-tasks/persistent/getting-started/define-work)). WorkManager has no battery-saver condition, so the background sync checks battery saver itself and skips the run ([PowerManager](https://developer.android.com/reference/android/os/PowerManager#isPowerSaveMode())).

Proposed, each a decision to confirm:

- The background sync runs every 4 hours, and also needs the battery not to be low.
- "The app opens" includes coming back to it after leaving it.
- While the app is open, a failed sync retries as on the desktop. In the background, a failed sync waits for the next run, and a waiting push retries with WorkManager's growing waits.
- Sync's own 30-minute timer doesn't run on the phone: Shell passes Sync no periodic interval, as [Sync's public interface](../sync.md#public-interface) proposes.
- The app shows no notifications, so it never asks for that permission. Sync status shows in [View Tasks](task-list.md#view-tasks-shows-sync-status-and-banners) and [Configure](configure.md#configure-shows-how-sync-is-doing).

### Updates come from this repository's GitHub Releases

At most once a day, the app checks this repository's GitHub Releases. The repository is public, so the check needs no token; GitHub allows 60 such requests an hour ([rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)). A newer APK, the Android package file, shows a banner in View Tasks. Tapping it downloads the APK and opens Android's installer. The user allows the app to install apps once, the first time ([install permission](https://developer.android.com/reference/android/Manifest.permission#REQUEST_INSTALL_PACKAGES)).

Releases reuse the desktop's release step, as the [Android app page](README.md#later-designs-answer-what-these-files-leave-open) lists. Android installs an update only when it's signed with the same key as the installed app ([app signing](https://developer.android.com/studio/publish/app-signing)).

Proposed, each a decision to confirm:

- Shell shows the update banner through the [Task list's banners](task-list.md#public-interface), so the Task list holds no update code.
- A failed check or download waits for the next day's check, with no banner, as on the desktop.

### The app follows the system theme

Proposed: the app follows the phone's light or dark setting. On Android 12 and later, it uses dynamic color, the colors Android takes from the wallpaper ([dynamic color](https://developer.android.com/develop/ui/views/theming/dynamic-colors)). Colored text keeps [Rendering's palette](../rendering.md#six-colors-read-well-in-light-and-dark-themes). This is a decision to confirm.

## Public interface

None. Shell is the Android app's entry point, and no module uses it.

## Failure modes

- **A background sync fails**: it tries again at the next run, and View Tasks and Configure show the error when the app opens.
- **An update can't be checked or downloaded**: the app keeps its current version and checks again the next day.
- **An update won't install**, such as an APK signed with another key: Android's installer says so, and the current version keeps running.
- **The widget can't read the tasks**: it shows only the Add button.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Ways into the app:
  - **A share target**, which adds a task from text another app shares, and **a Quick Settings tile**: offered, and not chosen.
- Background sync:
  - **No background sync**, and **every 30 minutes**: offered, and not chosen.
- Updates:
  - **Downloading each update by hand**, and **[Obtainium](https://github.com/ImranR98/Obtainium)**, an app that installs updates from GitHub Releases: offered, and not chosen.
