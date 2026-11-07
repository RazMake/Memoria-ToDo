# How the Android app is built

The Android app is a .NET for Android app for adding a task in seconds on the phone and seeing the same task list as on the desktop. This page lists its four modules, the proof on a phone that comes before any other Android work, the Android versions and libraries it uses, and what it needs from later designs.

## Four modules make up the Android app

Each module is a project of its own, separate from the desktop module of the same name, and the [module map](../modules.md#module-map) says which modules each may use. The table lists them in the order a user meets them:

| Module | What its design file decides |
| --- | --- |
| [Shell](shell.md) | Where each way into the app leads: the app icon, the app shortcut, and the home-screen widget. Also background sync, updates, and the theme. |
| [Task editor](task-editor.md) | The full-screen Add screen: its fields, toolbar, tag chips, and draft. View Tasks edits a task with the same screen. |
| [Task list](task-list.md) | View Tasks: the list in rank order, expanding in place, swipes, reordering, the tag filter, search, resolved tasks, and the floating Add button |
| [Configure](configure.md) | The settings screen: the phone's key, the shared deletion delay, the swipe direction, and sync setup and status |

The shared modules serve the phone as they serve the desktop: [Editing](../editing.md) holds the formatting and snippet commands, [Rendering](../rendering.md) turns Markdown into HTML and styled text, [Tasks](../tasks.md) holds the task files, and [Sync through git](../sync.md) keeps devices the same. Where an Android screen works like its desktop counterpart, its design file links the [desktop design](../desktop/README.md) instead of repeating it.

## A proof on a phone comes before any other Android work

The first Android build step is a throwaway proof on a phone, and it blocks all other Android work. It runs the design that [How the apps fit together](../overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport) decides, on .NET for Android: LibGit2Sharp, with libgit2 built with the Android Native Development Kit (NDK), and our SSH transport on SSH.NET. The proof:

1. Clones a repository, commits, and pushes over SSH.
2. Signs in with an RSA 3072-bit key held in the [Android Keystore](https://developer.android.com/privacy-and-security/keystore), which never lets the key out.
3. Measures how soon the Add screen accepts typing, against the [Task editor's targets](task-editor.md#add-opens-full-screen-with-the-keyboard-up).

The proof also checks what the [risks](../overview.md#risks-the-later-rows-must-resolve) ask of it, such as both forms of SSH address and the size of the Android package (APK).

The proof passed on November 6, 2026, on a Google Pixel 10 Pro with Android 17 (API level 37), and before that on an emulator with Android 16 (API level 36) on x86_64. Both runs used .NET 11 RC 3 and LibGit2Sharp 0.32.0. They showed:

- **LibGit2Sharp loads our libgit2 by name.** The app never sets `GlobalSettings.NativeLibraryPath`, and .NET found `libgit2-5853918.so` in 9 ms on the phone.
- **The app runs on CoreCLR.** The APK holds `libcoreclr.so` and no Mono runtime, and the running app mapped `libcoreclr.so`.
- **SSH.NET signs with a Keystore key the app can't read.** On the phone, the key lives in its trusted execution environment (TEE), as [Configure](configure.md#the-key-is-made-inside-the-android-keystore-and-never-leaves-it) decides. Creating it took 280 ms, and each signature 10 to 66 ms. The key offers only `rsa-sha2-512` and `rsa-sha2-256`, and each sign-in signed once.
- **Both address forms work against GitHub**, as on Windows. Two clones pushed and fetched in both directions, and the transport refused a host key that isn't on its list. On the phone, each step took 1.3 to 2.1 seconds, 0.7 to 1.2 seconds of it to connect.
- **The Add screen meets its cold-start target, and its warm-start target depends on what counts as accepting typing.** The [Task editor](task-editor.md#add-opens-full-screen-with-the-keyboard-up) records the timings. Only the phone's count, since the emulator draws its screens in software.
- **The release APK is 22.1 MB with arm64-v8a only.** It's 23.3 MB with x86_64 only, and 39.1 MB with both. libgit2 takes 0.7 MB of each ABI's share. The largest parts are the .NET assemblies (10.8 MB), the AndroidX and Material code (4.4 MB), and the .NET runtime (4.3 MB).
- **Android copies the native libraries out of the APK when it installs the app**, because the build marks the APK that way. On the phone they take another 21.9 MB, so the installed app takes about 44 MB.

If the proof fails, these fallbacks are tried in order:

1. Current JGit with Apache MINA SSHD.
2. JGit 5.13 with MINA SSHD, which the [WebIDE](https://github.com/h465855hgg/WebIDE/blob/main/gradle/libs.versions.toml) Android app uses.
3. [go-git](https://github.com/go-git/go-git) through [gomobile](https://pkg.go.dev/golang.org/x/mobile/cmd/gomobile), which adds the Go toolchain to the build.

Each fallback changes how the Android app reaches git, so [How the apps fit together](../overview.md) records it when it's taken. The [technology stack](../technology-stack.md#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided) says when the proof runs, where its code lives, and where its results are recorded.

## The app runs on Android 8.0 and later, with AndroidX and Material

Proposed, each a decision to confirm:

- **The app needs Android 8.0 (API level 26) or later.** .NET 11 needs Android 7.0 (API level 24) ([minimum Android API level](https://learn.microsoft.com/en-us/dotnet/core/compatibility/maui/11/android-minimum-api-level)). Android 8.0 made installing apps a permission of each app ([Android 8.0 changes](https://developer.android.com/about/versions/oreo/android-8.0-changes)), so the user allows only this app to install its [updates](shell.md#updates-come-from-this-repositorys-github-releases).
- **The released APK holds only 64-bit ARM code (arm64-v8a).** Debug builds add x86_64 for the emulator. Each application binary interface (ABI) needs its own libgit2 build and makes the APK bigger, as the [risks](../overview.md#risks-the-later-rows-must-resolve) say. Phones with only 32-bit ARM can't install the app.

The screens use AndroidX and Material Components through Microsoft's .NET bindings, all new dependencies. The table lists each package, its latest stable version, and what uses it:

| Package | Version | Used for |
| --- | --- | --- |
| [Xamarin.Google.Android.Material](https://www.nuget.org/packages/Xamarin.Google.Android.Material/1.14.0.6) | 1.14.0.6 | The floating Add button, chips, the Undo bar, the top app bar, and the theme |
| [Xamarin.AndroidX.RecyclerView](https://www.nuget.org/packages/Xamarin.AndroidX.RecyclerView/1.4.0.6) | 1.4.0.6 | The task list, its swipes, and drag to reorder |
| [Xamarin.AndroidX.Work.Runtime](https://www.nuget.org/packages/Xamarin.AndroidX.Work.Runtime/2.12.0) | 2.12.0 | Background sync |
| [Xamarin.AndroidX.SwipeRefreshLayout](https://www.nuget.org/packages/Xamarin.AndroidX.SwipeRefreshLayout/1.2.0.3) | 1.2.0.3 | [Pulling the task list down](task-list.md#pulling-the-list-down-starts-a-sync) to sync |

## Later designs answer what these files leave open

The Android design relies on the technology stack, a later row of the [design backlog](../README.md#design-backlog). The table lists each need, the modules that need it, and the design that answers it:

| Needed | Used by | Answered by |
| --- | --- | --- |
| The release step that publishes the APK to GitHub Releases, reusing the desktop's | Shell | Technology stack |
| The build of libgit2 with the NDK for each ABI, and where it lives | Sync, on Android | Technology stack, as the [risks](../overview.md#risks-the-later-rows-must-resolve) say |
| Where the key that signs each APK is kept. Android's update model needs every update signed with the same key ([app signing](https://developer.android.com/studio/publish/app-signing)). | Shell | Technology stack |

Everything else the Android screens need from Tasks and Sync is in the [desktop app's table](../desktop/README.md#later-designs-answer-what-these-files-leave-open), already answered.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Fallbacks if the proof fails:
  - **libgit2 with libssh2**: a second C SSH stack we would maintain.
- Screens:
  - **Android's built-in widgets only**: offered, and not chosen. Android has no chips, floating button, or Undo bar of its own, so the app would draw them itself.
