# Purpose and scope: what Memoria-ToDo does, and for whom

Status: Decided

Memoria-ToDo is a Windows desktop app and an Android phone app that let one person add a task almost instantly and see the same tasks on every device they own. This page says what the apps do and for whom; later rows of the [design backlog](README.md#design-backlog) decide how they're built.

## Behavior

Two principles shape everything below: adding a task is as fast as it can be, and every device shows the same tasks.

### Adding a task takes almost no time

The main goal is that the user gets from deciding to add a task to typing in the editor as close to instantly as possible.

- **Title and description are markdown.** The user formats text and inserts common snippets with shortcuts.
- **Tags can be added while adding the task**, or later in View Tasks.
- **Anything else that makes adding a task faster is in scope.**

### Every device shows the same task list

One person uses Memoria-ToDo, and only they write to it, from their own Windows PCs and Android phones. There's one task list. Every change made on one device reaches all the others: adding, updating, tagging, ranking, resolving, and deleting a task.

- **No server.** The apps sync through a git repository the user owns, on any git host that offers git over SSH: GitHub, Azure DevOps, GitLab, or a self-hosted server.
- **Each device is configured once.** The user sets up each device by hand in Configure; after that, it stays in sync.
- **Each device has its own SSH key.** The device creates its own key pair, and the user adds the public key on the git host, as [Sync through git](sync.md#configure-sets-up-a-device-by-cloning-the-repository) describes. A key never expires, and removing one device's key revokes only that device.
- **The operating system protects the private key.** On Windows, only the user's account can unlock it; on Android, the [Android Keystore](https://developer.android.com/privacy-and-security/keystore) protects it. [Sync through git](sync.md#the-windows-key-file-is-encrypted-and-only-the-users-account-unlocks-it) decides the Windows mechanism, and the Android [Configure](android/configure.md#the-key-is-made-inside-the-android-keystore-and-never-leaves-it) the Android one.
- **Only a private repository protects the tasks.** Files aren't encrypted, so anyone who can read the repository, including the git host, can read every task. The apps don't remind the user to keep it private.

### Both apps offer Add new Task, View Tasks, and Configure

The two apps differ in how they run and where they open:

| App | How it runs | Where it opens |
| --- | --- | --- |
| Windows desktop app | Starts with Windows and lives in the notification area (the tray). | Clicking the tray icon offers the three functions. |
| Android app | Isn't meant to always run: it runs while the user has it open, and [syncs in the background](android/shell.md#sync-runs-while-the-app-is-open-and-every-few-hours-in-the-background) every few hours. | View Tasks if the app is configured, otherwise Configure. A floating button in View Tasks adds a task. |

The desktop app must be built with Windows Presentation Foundation (WPF). Both apps are for personal use: the desktop app installs with a simple installer, and the Android app is sideloaded from an Android package (APK) file.

### Each task is a JSON file with a few properties

Each task is saved in the repository as its own JSON file, named after the task's ID and holding the task's properties. The user sets four properties, and the apps set the rest:

| Property | What it holds | Set by |
| --- | --- | --- |
| Title | A short title, in markdown | The user |
| Description | Longer text in markdown, which can hold images, links, info blocks, references, and time stamps | The user |
| Rank | A sort key that sets the order of the tasks, as [Tasks](tasks.md#rank-is-a-text-key-that-sorts-between-its-neighbors) decides | The user |
| Tags | Labels for the task | The user |
| ID | The task's identity, also its file name | The apps |
| Created time | When the task was added | The apps |
| Last-updated time | When the task last changed | The apps |
| Resolved time | When the task was resolved, if it was | The apps |

The first version has no due dates or reminders.

Images in a description are saved as files in the same repository and sync like tasks. The apps shrink any image over 1 MB before saving it, so no saved image is larger. An image is deleted with its task. [Tasks](tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) decides how images are stored and shrunk. Git history keeps every image ever added, so the repository only grows, an [accepted risk](overview.md#risks-the-later-rows-must-resolve).

### Resolved tasks stay for a week, then are deleted

Resolving a task marks it resolved and records when. The task stays in the repository and is deleted automatically one week after it was resolved. Until then, the user can reopen it, which clears its resolved time so it's no longer due for deletion.

The user can change the one-week delay. It's one setting, stored in the repository and shared by every device: changing it on one device changes it on all.

The apps hide resolved tasks by default. They show how many there are, and list them when the user asks.

### Later designs decide how the apps are built

Apart from WPF and SSH keys, this page sets no technology, architecture, or modules. Each is a row in the [design backlog](README.md#design-backlog):

- **High-level architecture**: [how the apps fit together](overview.md).
- **Technology stack**: test tools and the build command.
- **Modules**: how each marks its public interface, and the module map.
- **[Desktop app](desktop/README.md)**, with **[Editing](editing.md)** and **[Rendering](rendering.md)**: the desktop app's screens, shortcuts, and settings, the formatting and snippet commands, and how Markdown shows.
- **[Tasks](tasks.md)** and **[Sync through git](sync.md)**, designed together: the task files, how and when changes travel, and what happens when two devices change the same task.
- **[Android app](android/README.md)**: the Android app's screens and settings.

## Public interface

None. This page defines no module; every other design file works within its scope.

## Failure modes

None. Each app's design and [Sync through git](sync.md#failure-modes) own their failures, such as an unreachable git host or a rejected key.

## Ruled out

The options below were considered and rejected, grouped by the decision they belong to.

- Who writes, and how many lists:
  - **Several people sharing one repository**: only the user writes, from their own devices.
  - **Several repositories per device**: one task list is shared by every device instead.
- How devices reach the repository:
  - **GitHub only**: it would let the apps use the GitHub web API instead of git, but would lock them to GitHub.
  - **HTTPS with a personal access token**: the user doesn't want the apps to use personal access tokens.
  - **Browser sign-in through a GitHub App**, with device flow and tokens limited to one repository, lasting 8 hours and refreshed for 6 months ([GitHub](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/refreshing-user-access-tokens)): it would limit hosts to those with a sign-in provider.
  - **Browser sign-in for GitHub and Azure DevOps through Microsoft Entra ID**: more work, and work tenants often block device-code sign-in ([Microsoft Entra](https://learn.microsoft.com/en-us/entra/identity/conditional-access/policy-block-authentication-flows)).
- How tasks are stored and protected:
  - **Markdown files with front matter**: each task is a JSON file of its properties.
  - **Encrypted files**, such as a passphrase typed on every device: a lost passphrase would lose every task, and git would store a whole copy of a file on each change ([git-crypt limitations](https://github.com/AGWA/git-crypt#limitations)).
  - **A reminder in Configure to keep the repository private**: keeping it private is left to the user.
- Images:
  - **Links to external images only**: images live in the repository, so they sync with their task.
  - **Images up to 5 MB, saved as is**, or **no size limit of the apps' own**: git history keeps every image, so smaller images keep the repository smaller.
- Resolving and deleting:
  - **Deleting a task when it's resolved**, or **moving it to an archive folder**: a resolved task stays in place, marked resolved, until its automatic deletion.
  - **Resolving as final**: reopening undoes a mistaken resolve, and the task is kept anyway.
  - **A deletion delay set on each device**: every deletion reaches every device, so the shortest delay would win everywhere.
- First version and distribution:
  - **Due dates**, with or without **reminders**: not in the first version.
  - **Publishing to app stores**: the apps are for personal use and installed by hand.
