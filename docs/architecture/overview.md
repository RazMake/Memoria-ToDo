# How the apps fit together

Status: Decided

Memoria-ToDo is two C# apps, a Windows Presentation Foundation (WPF) desktop app and a .NET for Android app, built on one shared C# library that holds the task format, the merge rules, and sync. Each device keeps a full git clone of the task repository as its store and syncs it in the background.

## Behavior

This page decides the parts, their main technology, and how they connect. [Purpose and scope](purpose.md) says what the apps do, and [Modules](modules.md) splits each part into modules. Each app, Tasks, and Sync have design files of their own, listed in the [design backlog](README.md#design-backlog).

### Two apps share one C# library

All our code is C#; the Android app also ships libgit2, git's C library, which we build for Android ourselves. The table lists the three parts and how each reaches git:

| Part | What it holds | How it reaches git |
| --- | --- | --- |
| Shared library | The task format, merge rules, sync, editing commands, and turning Markdown into HTML. It has no screens. | Through [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/blob/master/CHANGES.md) 0.31 or later and one SSH transport of its own, as [below](#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport) says |
| Desktop app | WPF screens, the tray icon, and the Windows implementations of the library's interfaces | Through the shared library, with the Windows libgit2 that LibGit2Sharp ships ([native binaries](https://github.com/libgit2/libgit2sharp.nativebinaries)) |
| Android app | .NET for Android screens, and the Android implementations of the library's interfaces | Through the shared library, with libgit2 built from source with the Android Native Development Kit (NDK) |

The shared library is a set of C# projects, one per module plus the shared platform, as [Modules](modules.md#a-module-is-one-capability-behind-a-small-interface) decides; both apps reference them.

Each app also implements the library's interfaces for the secure key store, the file system, the clock, and image shrinking. Images are shrunk with each platform's own image tools, with no extra image library, as [Tasks](tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) decides.

### Each app edits in its own text box, with shared commands

The editor is each platform's own text box: [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) on Windows, and the standard Android text field. Formatting, snippet, and list commands are shared C# text operations, so both text boxes run the same commands. Native text boxes open fastest and use the least memory, and on a phone, keyboard autocorrect and voice typing work fully.

Rendered descriptions, with their images and info blocks, show as HTML in a web view in both apps: WebView2 on Windows, Android WebView on the phone. A web view is used because maintained native WPF Markdown renderers are archived, such as [Markdig.Wpf](https://github.com/Kryptos-FR/markdig-wpf). The shared library turns Markdown into HTML with [Markdig](https://github.com/xoofx/markdig), a fast CommonMark parser for .NET. Titles show as styled native text, as [Rendering](rendering.md) decides.

A web view costs memory and start-up time ([WebView2 memory](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1705)), so it must never delay the editor. On the desktop, the [Task editor](desktop/task-editor.md) uses no web view, and the [Task list](desktop/task-list.md) decides when its one web view starts; on the phone, the Android [Task list](android/task-list.md) decides.

### Each app keeps thin view models; rules live in shared modules

A view model holds a screen's state and commands, so the screen itself only displays them. Each app keeps thin view models of its own. Every rule a user notices, such as which tasks show and in what order, lives in the shared modules; a view model only holds screen state and calls them.

### The local git clone is the store

Each device keeps a full clone of the task repository. The apps read and write task files in the clone and keep the task list in memory, so every operation is instant and works offline.

Sync runs in the background: commit, fetch, merge, and push. Merging goes task by task with the app's own rules, never with git's line-based text merge. Sync signs in with the device's own SSH key, as [purpose and scope](purpose.md#every-device-shows-the-same-task-list) decides. [Tasks](tasks.md#two-versions-of-a-task-merge-field-by-field) decides the merge rules, and [Sync through git](sync.md) decides when sync runs and how it retries.

### Both apps reach git through LibGit2Sharp and one SSH transport

Both apps run git through [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/blob/master/CHANGES.md) 0.31 or later, a C# library over libgit2. They connect over SSH through one transport of our own, not through LibGit2Sharp's built-in SSH, which is [ruled out](#ruled-out).

- **One SSH transport serves both apps.** The shared library writes it once, as a LibGit2Sharp [smart subtransport](https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/SmartSubtransport.cs), the base for custom single-connection transports, registered for the `ssh` scheme ([GlobalSettings](https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/GlobalSettings.cs)). It's built on [SSH.NET](https://github.com/sshnet/SSH.NET), a C# SSH client. It runs `git-upload-pack` or `git-receive-pack` on the server and streams their input and output ([SshCommand](https://sshnet.github.io/SSH.NET/api/Renci.SshNet.SshCommand.html)). libgit2 tries custom transports before its own, and also sends scp-style addresses, such as `git@host:owner/repo.git`, to the transport registered for `ssh` ([transport.c](https://github.com/libgit2/libgit2/blob/v1.8.4/src/libgit2/transport.c)). It passes the address unchanged ([smart.c](https://github.com/libgit2/libgit2/blob/v1.8.4/src/libgit2/transports/smart.c)), so our transport reads both forms, `ssh://[user@]host[:port]/path` and `[user@]host:path`, and needs no rewrite to `ssh://`.
- **The private key reaches the transport only in memory.** The transport takes it from the platform's secure key store, and checks the server's host key in our own code.
- **The Android app ships a libgit2 we build.** LibGit2Sharp's native libraries cover only Windows, macOS, and Linux ([native binaries](https://github.com/libgit2/libgit2sharp.nativebinaries)), so the Android app uses libgit2 built from source with the Android NDK. PuppyGit, a shipping Android app, builds libgit2 this way ([PuppyGit build script](https://github.com/catpuppyapp/PuppyGit/blob/main/lib_build_scripts/3_buildlibs.sh)).

Two throwaway proofs test this before Sync is built on it. The [risks](#risks-the-later-rows-must-resolve) list what they must show, and the [technology stack](technology-stack.md#the-throwaway-proofs-run-on-a-never-merged-branch-before-this-page-is-decided) says where their code lives. On Android, a [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) is the first build step and blocks all other Android work; if it fails, that page lists the fallbacks. The git code both apps share, the transport included, lives inside the Sync module, as [Modules](modules.md#the-platform-serves-every-module-and-knows-none) decides.

The Windows proof passed on November 6, 2026, on .NET 11 RC 3 with LibGit2Sharp 0.32.0. It cloned once with each address form, then committed, pushed, and fetched in both directions. It ran first against [Sync's test server](technology-stack.md#syncs-integration-tests-run-an-ssh-server-inside-the-test-process), then against GitHub. It showed:

- **libgit2 sends both address forms to our transport.** LibGit2Sharp's Windows libgit2 has SSH built in, but our transport ran every command.
- **The transport refuses a host key that isn't on its list.** GitHub's host key matched the fingerprint GitHub publishes.
- **Each clone, push, or fetch took 0.8 to 1.5 seconds against GitHub**, from a cloud dev box. Each makes its own connection, which took 0.4 to 0.8 seconds.
- **The transport must let the server end each command before it closes the channel**, as git does with OpenSSH. It sends end-of-file, then waits for the command to end. Closing first made SSH.NET wait 1 second per command against the test server, which doesn't answer a close.
- **SSH.NET offers an RSA key with a SHA-1 signature first**, which [Sync](sync.md#each-device-signs-in-with-its-own-rsa-key) rules out.

Not confirmed:

- **Azure DevOps.** You waived that run.
- **An RSA key on GitHub, from Windows.** The GitHub run signed in with an Ed25519 key. RSA signing, as `rsa-sha2-512`, ran only against the test server. GitHub accepted `rsa-sha2-512` from the same transport in the Android app's [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work).

### Every part targets .NET 11

Every part targets .NET 11: the shared library, the desktop app, and the Android app. .NET 11 ships November 10, 2026, and standard-term releases such as .NET 11 get two years of support ([.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)).

Android support is shorter. Each .NET for Android version is supported only six months after the next .NET ships ([.NET MAUI support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/maui)), so the Android app moves to each new .NET within six months of its release, next to .NET 12. Proposed: the shared library and the desktop app move with it, so every part stays on one .NET version. This is a decision to confirm.

On Android, .NET 11 runs only on Android 7.0 (API level 24) and later ([minimum Android API level](https://learn.microsoft.com/en-us/dotnet/core/compatibility/maui/11/android-minimum-api-level)), so the Android app can't support older phones. The [Android app](android/README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material) picks its exact minimum.

This page owns the language, the .NET version, and the main libraries; the technology stack row owns test tools and the build command.

### Each pattern serves one requirement

The table lists each pattern the design uses, where, the requirement it serves, and its cost:

| Pattern | Where | Serves | Trade-off |
| --- | --- | --- | --- |
| Offline-first synchronization | The local clone and background sync | Instant operations that work offline, and the same list on every device | Conflicts, deletions, and format changes, which [Tasks](tasks.md) and [Sync through git](sync.md) handle |
| Ports and adapters: interfaces the library owns, implemented apart from the rules that use them | The secure key store, the file system, the clock, and image shrinking. Not git, which [stays inside Sync](modules.md#the-platform-serves-every-module-and-knows-none) | Each platform's own code, such as its secure key store, behind one interface; unit tests with no real I/O, as [AGENTS.md](../../AGENTS.md#test-design) requires, including time-based deletion | Extra interfaces, so a port exists only where two implementations or a test need one |
| Modular monolith, one per app | Each app is one deployable built from [modules](modules.md) | A change stays inside one module | Boundaries need enforcement, which costs one C# project per module |
| Model-View-ViewModel (MVVM) | Each app's screens, with thin view models of its own | Screen logic tested without a screen; WPF binds views to view models natively | Android views are updated from view models by hand-written code |

The design doesn't use client-server or microservices, since there's no server. It doesn't use an event broker, since each device runs one process, or plugins, since nothing is extended at run time. It doesn't split code into technical layers, since modules split by capability.

### Risks the later rows must resolve

The table lists each known risk and the design that resolves or accepts it:

| Risk | Why | Resolved by |
| --- | --- | --- |
| Our SSH transport is unproven | Before the proofs, nobody had run LibGit2Sharp with an SSH transport of its own built on SSH.NET. SSH.NET targets .NET 8 and later ([SSH.NET](https://github.com/sshnet/SSH.NET)), but hasn't run on .NET 11 for Android, whose runtime is new (see below). | The Windows proof [above](#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport), which passed except against Azure DevOps, and the Android app's [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work), which passed on a Pixel 10 Pro and an emulator |
| LibGit2Sharp's operating system check throws on Android | LibGit2Sharp doesn't support Android: its check knows only Windows, Linux, and macOS ([Platform.cs](https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/Core/Platform.cs)), and .NET reports Android as `ANDROID`, not Linux ([OperatingSystem.cs](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/OperatingSystem.cs)). The check runs only when `GlobalSettings.NativeLibraryPath` is set; otherwise LibGit2Sharp loads libgit2 by name, through .NET's default loading ([NativeMethods.cs](https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/Core/NativeMethods.cs), [GlobalSettings.cs](https://github.com/libgit2/libgit2sharp/blob/master/LibGit2Sharp/GlobalSettings.cs)). | Android app: it never sets `NativeLibraryPath`, and ships our libgit2 under the name LibGit2Sharp loads. The proof on a phone showed it loads, on arm64-v8a and x86_64. |
| SSH.NET hasn't signed with a key the Android Keystore won't export | The Keystore never lets the key out, so SSH.NET must hand each signature to it. SSH.NET allows this: a host key algorithm takes any signature class ([KeyHostAlgorithm](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/Security/KeyHostAlgorithm.cs), [DigitalSignature](https://github.com/sshnet/SSH.NET/blob/develop/src/Renci.SshNet/Security/Cryptography/DigitalSignature.cs)). The Keystore signs with RSA, PKCS#1 v1.5 padding, and SHA-256 or SHA-512 ([KeyProperties](https://developer.android.com/reference/android/security/keystore/KeyProperties)), as SSH's `rsa-sha2-256` and `rsa-sha2-512` need ([RFC 8332](https://www.rfc-editor.org/rfc/rfc8332)). Nobody had tried it. | The proof on a phone, which signed with a key held in the phone's trusted execution environment |
| Our transport must read both SSH address forms, in both apps | libgit2 hands `ssh://` addresses and scp-style addresses such as `git@host:owner/repo.git` to our transport unchanged, as [above](#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport). A form the transport can't parse fails every sync for that repository. | Sync through git, whose transport parses both forms. Each proof clones once with each form; both did. |
| Each ABI the app ships makes the APK bigger | Each ABI needs its own libgit2. .NET 11 doesn't support 32-bit x86, but keeps x86_64 for emulators ([Android x86 isn't supported](https://learn.microsoft.com/en-us/dotnet/core/compatibility/maui/11/android-x86-unsupported)). | Android app, which [proposes arm64 only](android/README.md#the-app-runs-on-android-80-and-later-with-androidx-and-material) in the APK it releases. The proof [measured the sizes](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work): 22.1 MB with arm64-v8a only, 39.1 MB with x86_64 too. |
| We maintain libgit2's Android build | No LibGit2Sharp package holds an Android libgit2 ([native binaries](https://github.com/libgit2/libgit2sharp.nativebinaries)). So each libgit2 security fix, such as libgit2 1.8.6 in the LibGit2Sharp 0.32.0 security release ([changes](https://github.com/libgit2/libgit2sharp/blob/master/CHANGES.md)), reaches Android only when we rebuild it with the NDK, from the libgit2 version LibGit2Sharp binds to. | Technology stack, which [rebuilds it with a script in this repository](technology-stack.md#a-script-in-this-repository-builds-libgit2-for-android-and-its-binaries-are-committed) whenever LibGit2Sharp moves to another libgit2 commit, and commits the binaries |
| Host keys are checked in our own code | No OpenSSH known-hosts file is used, so a wrong check would let another server read pushed tasks or send false ones | Sync through git, which decides [which host keys a device trusts](sync.md#the-app-checks-each-host-key-against-its-own-list) |
| The Windows private key must stay protected | Anyone with the key can push to the task repository. | Sync through git: the key file is [encrypted, and decrypted only in memory](sync.md#the-windows-key-file-is-encrypted-and-only-the-users-account-unlocks-it) during sync |
| .NET 11's Android runtime is brand new: CoreCLR replaces Mono | [CoreCLR replaces Mono](https://learn.microsoft.com/en-us/dotnet/core/compatibility/maui/11/mobile-runtime-replacement) | Android app: the [proof on a phone](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) ran on CoreCLR |
| The task repository only grows | Git history keeps every task and image ever saved, including deleted ones ([removing files from history](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github#removing-files-from-a-repositorys-history)). GitHub recommends at most 10 GB on disk ([repository limits](https://docs.github.com/en/repositories/creating-and-managing-repositories/repository-limits#repository-size)): at least 10,000 images at the 1 MB cap. | Accepted. [Tasks](tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) caps each image at 1 MB. |

## Public interface

None. This page defines no module; the shared library's public interface is its modules' public interfaces, which [Modules](modules.md) lists and marks.

## Failure modes

None. [Tasks](tasks.md#failure-modes), [Sync through git](sync.md#failure-modes), and each app's design own their failures.

## Ruled out

The options below were considered and rejected, grouped by the decision they belong to. [Purpose and scope](purpose.md#ruled-out) lists the sign-in options ruled out.

- Language and sharing:
  - **A C# desktop app and a Kotlin Android app**: the sync rules would be written twice, which breaks the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication).
  - **A shared Rust core**: a third language, native builds for two platforms, and harder debugging.
- Git and SSH:
  - **JGit on Android, through the [Gazman-Dev/jgit](https://github.com/Gazman-Dev/jgit) core-only fork with Apache MINA SSHD, behind a Java wrapper called through [Native Library Interop](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/)**: JGit's only SSH part is MINA SSHD ([JGit](https://github.com/eclipse-jgit/jgit)), whose Android support is unchecked and "not a stated goal" ([MINA SSHD on Android](https://github.com/apache/mina-sshd/blob/master/docs/android.md)). JGit keeps raising its Java version: 7.0 needs Java 17 ([JGit 7.0](https://projects.eclipse.org/projects/technology.jgit/releases/7.0.0)), and 8.0 may need Java 25 ([JGit issue 292](https://github.com/eclipse-jgit/jgit/issues/292)). The fork has no SSH, 4 stars, and no push since June 14, 2025.
  - **JGit with the maintained [JSch fork](https://github.com/mwiede/jsch) for SSH, through our own Java SSH adapter**: the SSH code would exist twice, in C# and in Java, which breaks the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication), and JGit's Java version keeps rising.
  - **LibGit2Sharp's built-in SSH with the Windows OpenSSH client, which asks the app for the key's passphrase through `SSH_ASKPASS`, plus JGit with Apache MINA SSHD on Android**: two git and SSH code paths, plus Java and Gradle in the build. LibGit2Sharp's built-in SSH runs the OpenSSH client ([ssh_exec.c](https://github.com/libgit2/libgit2/blob/v1.8.4/src/libgit2/transports/ssh_exec.c)). JGit with MINA SSHD stays the first [fallback](android/README.md#a-proof-on-a-phone-comes-before-any-other-android-work) if the proof on a phone fails.
- Editor and rendering:
  - **One CodeMirror 6 web editor in WebView2 and Android WebView**: it adds TypeScript and npm, a preloaded WebView2 idles at about 40–50 MB ([WebView2 memory](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1705)), and phone keyboard input is less reliable.
  - **A JavaScript renderer in the web view**: it adds JavaScript to both apps.
- Screens:
  - **View models in the shared library**: each screen would span three folders, breaking the [one-folder rule](modules.md#a-module-is-one-capability-behind-a-small-interface), and Android needs hand-written binding anyway.
- Store:
  - **A local SQLite database as the store, with git only exchanging files**: every task would exist twice, kept consistent by hand.
- .NET version:
  - **.NET 10, with only the Android app moving each year**: the solution would hold two .NET versions.
  - **Staying on .NET 10 and deciding later**: the Android app would get no security fixes after May 11, 2027.
