# How code is split into modules

Status: Decided

Code is split into modules, one per capability, so a change or a mistake stays inside one module. This page owns the module rules and the module map; the [code design rules](../../AGENTS.md#code-design) cover the components inside a module.

## Behavior

Every module follows the rules below, and the [module map](#module-map) lists the modules. Each module's own design file decides what the module does.

### A module is one capability behind a small interface

- **One module, one design file.** A module does one thing a user would name, in one place: the shared library or one app. Its design file, under `docs/architecture/`, is its spec. When both apps need the same capability, each app has its own module of that name, with its own project and its own design file in that app's folder, `docs/architecture/desktop/` or `docs/architecture/android/`.
- **One C# project per module, in one folder** named after it, holds its code. Its tests live in a matching folder and use only its public interface.
- **A small public interface.** Other code uses the module only through its public interface. Everything else in the module is `internal`, which the compiler hides from other projects.
- **Two files list the public interface.** The [public API analyzer](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) keeps `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` in each project, listing every public declaration, and the build fails when they're out of date. A reviewer reads only the diff of those files to see every public-interface change.
- **It owns its data.** Its stored data, messages, and screen state are its own. Other modules reach them only through its public interface.
- **It keeps its failures.** A module reports its own failures, and the rest of the application keeps working.

### The platform serves every module and knows none

- **The platform is the code outside module projects and the composition root**: what every module shares, such as startup, settings, and storage access. It never names a module. The shared library's platform holds the [file system interface](#the-file-system-interface-reads-and-writes-whole-files), and each app's platform holds its startup. The shared library's platform is one project, `Memoria.ToDo.Shared.Platform`, in `src/Shared/Platform/`.
- **Every module takes the time from .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)**, which gives the time and timers, and the composition root passes the system's. The shared library has no clock interface of its own.
- **Every project outside the modules marks its public interface as modules do.** The platform's projects, and each app's projects outside its modules, such as the one with its composition root, keep everything else `internal` and list their public interface in the two public API files.
- **Code shared by two modules moves to the platform**, as the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) requires. Code shared only inside one module stays in that module.
- **Only the composition root names modules.** Each app has one. It creates each module's parts and connects them, and it chooses the implementation of every interface a module or the platform publishes, as [AGENTS.md](../../AGENTS.md#code-design) says. A module wires its own `internal` code itself, as Sync does with its git code.
- **Each app's own implementations of shared interfaces live beside its composition root**, such as its image shrinking for Tasks, because they name a module's interface.
- **The git code both apps share lives inside Sync**, the only module that uses git. Sync's project references LibGit2Sharp and SSH.NET, which [How the apps fit together](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport) picks, and keeps its git code `internal`. Because Sync's tests use only its public interface, they can't replace git with a fake: tests of Sync's rules that touch git use real repositories and are integration tests. How two versions of a task merge belongs to Tasks, which doesn't touch git, so those rules keep unit tests.

### The file system interface reads and writes whole files

Modules reach files on the device only through the shared platform's file system interface, so their tests run on a fake with no real I/O. It works on whole files by full path, and names no module and no folder of its own.

- **A write replaces the whole file, or leaves the old one whole.** It writes a temporary file beside the target, then replaces the target with it, so a crash or a full disk never leaves half a task file.
- **Each call fails with the system's exception**, such as an `IOException`, and never returns a fallback. Only a missing file or folder is quietly fine: reading a missing file, or a file in a missing folder, fails with `FileNotFoundException`, but deleting a missing file or folder, or listing a missing folder, does nothing. A path of the wrong kind, such as listing a file or deleting a folder as a file, fails with an `IOException`, and so does any access failure.
- **Each app implements it beside its composition root**, as [above](#the-platform-serves-every-module-and-knows-none) says: the desktop app in `src/Desktop/App/`.
- **One fake, `FakeFileSystem`, keeps files in memory.** It sits in `Memoria.ToDo.Shared.Fakes`, in `tests/Shared/Fakes/`, as the [technology stack](technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test) places fakes.
- **One contract test keeps the fake honest.** Its checks are written once, as an abstract test class beside the fake. The platform's test project runs them on the fake as unit tests, and the desktop composition root's test project, in `tests/Desktop/App/`, runs them on the Windows implementation as integration tests. So the fake can't drift from the real thing.

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// Files on this device, by full path. Each app implements it beside its composition root.
/// Each method fails with the system's exception, such as IOException, and never returns a fallback.
public interface IFileSystem
{
    /// Whether a file exists at path.
    bool FileExists(string path);

    /// The whole file. Throws FileNotFoundException when there's none.
    byte[] ReadFile(string path);

    /// Replaces the whole file, or creates it and its folders. A failure leaves the old file whole.
    void WriteFile(string path, ReadOnlySpan<byte> content);

    /// The full paths of the files directly in folder, in no set order; none when the folder doesn't exist.
    /// Throws IOException when folder is a file.
    IReadOnlyList<string> ListFiles(string folder);

    /// Moves a file, creating the target's folders. Throws IOException when the target exists.
    void MoveFile(string sourcePath, string targetPath);

    /// Deletes a file. Does nothing when there's none; throws IOException when path is a folder.
    void DeleteFile(string path);

    /// Deletes a folder and everything in it. Does nothing when there's none; throws IOException when path is a file.
    void DeleteFolder(string path);
}
```

### Modules depend only on what the map allows

- **A module uses the platform and the modules the [map](#module-map) lists for it**, only through their public interfaces. A module in one app never uses a module in the other app.
- **Dependencies never form a cycle.**
- **Core rules own the interfaces they need.** Code for I/O, frameworks, and external services implements those interfaces; core rules never depend on it. This holds inside one module too: Sync's rules reach git only through an `internal` interface Sync owns, and only Sync's git code uses LibGit2Sharp and SSH.NET.

### What a change may touch

Who decides a change depends on what it touches:

| A change touches | Kind | Who decides |
| --- | --- | --- |
| One module's private code and its tests | Implementation | The agents; the reviewer checks it |
| The composition root, or the implementations of shared interfaces beside it | Implementation | The agents; the reviewer checks it |
| The platform, any module's public interface, or the module map | Architecture | You, through [Changing the design](../contributing/process.md#changing-the-design) |

### Module map

The three tables below list each part's modules, their design files, and the modules each may use. Adding, removing, or changing a row is an architecture change. An [architecture test](technology-stack.md#architecture-tests-check-the-module-map) reads the "May use" columns, so a change to the tables' format needs a change to that test. Each module's design file comes from its row in the [design backlog](README.md#design-backlog).

The shared library has four modules:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Tasks | Task files and their format, the in-memory list, every task operation, tags and search, task links, how two versions of a task merge, images and their shrinking, this device's draft of a new task, and the settings every device shares, such as the deletion delay | [Tasks](tasks.md) | None |
| Sync | Commit, fetch, merge, and push in the background through its own git code on LibGit2Sharp and SSH.NET, and the device's repository settings, SSH key, and known host keys | [Sync through git](sync.md) | Tasks |
| Editing | Formatting, snippet, and list commands on plain text and a selection, and the snippet picker's rules | [Editing](editing.md) | None |
| Rendering | Markdown to an HTML page for the web view and to styled text for titles and the editor, the color palette, and which links may open in the browser | [Rendering](rendering.md) | None |

The desktop app has four modules of its own:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Shell | The tray icon and its menu, the two global shortcuts, one running instance, what each start shows, updates, and sync status | [Shell](desktop/shell.md) | Task editor, Task list, Configure, Sync |
| Task editor | The Add window, and the editor and tag chips View Tasks uses | [Task editor](desktop/task-editor.md) | Tasks, Editing, Rendering |
| Task list | View Tasks: open tasks in rank order, expanded and edited in place, with the tag filter, search, resolving, deleting, resolved tasks, and the Sync button | [Task list](desktop/task-list.md) | Tasks, Rendering, Task editor, Sync |
| Configure | The two global shortcuts and the deletion delay, which every device shares; starting with Windows; and Sync's settings and status | [Configure](desktop/configure.md) | Tasks, Sync |

Proposed, each a decision to confirm:

- Shell may use Sync, so the tray shows sync status without going through Configure.
- The Task list may use Sync, for its Sync button and the sync when View Tasks opens.

The Android app has four modules with the same names, separate from the desktop app's:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Shell | Where the app icon, the app shortcut, and the home-screen widget lead; sync when the app opens and in the background; updates; and the theme | [Shell](android/shell.md) | Task editor, Task list, Configure, Sync |
| Task editor | The full-screen Add screen with its toolbar and tag chips, which also edits a task from View Tasks | [Task editor](android/task-editor.md) | Tasks, Editing, Rendering |
| Task list | View Tasks: open tasks in rank order, expanded in place, with swipes, reordering, the tag filter, search, resolved tasks, the floating Add button, and the top tasks the widget shows | [Task list](android/task-list.md) | Tasks, Rendering, Task editor, Sync |
| Configure | The phone's key and repository, the shared deletion delay, the swipe direction, and Sync's status | [Configure](android/configure.md) | Tasks, Sync |

Proposed, each a decision to confirm:

- On Android, Shell may use Sync, for the sync when the app opens and the background sync.
- On Android, the Task list may use Sync, for its sync status line.
## Public interface

None. This page sets rules and the module map, not a module; each module's design file states its own public interface.

## Failure modes

- **A public declaration missing from the public API files**: the build fails and names the declaration.
- **A dependency the map doesn't allow**: it needs a new project reference, so the reviewer agent finds it when checking containment, and you see it in the project file, which is part of the [review surface](../../AGENTS.md#contributions).

## Ruled out

The options below were considered and rejected, grouped by the decision they belong to.

- How a module marks its public interface:
  - **Module folders inside three projects, with a `Public` folder checked by architecture tests** such as [NetArchTest](https://github.com/BenMorris/NetArchTest): `internal` can't separate modules inside one project, so the boundary would hold only where the tests cover it.
- The first modules:
  - **A finer split**, with Images and Shared settings as modules of their own, and adding a task apart from editing one: more interfaces and projects, though images and the delay change with task files anyway.
  - **A coarser split**, with one shared Core module and one screens module per app: a change to sync and a change to editing commands would touch the same module, so a mistake could spread across capabilities.
  - **One module per screen capability, with one project in each app**: one design file would describe two platforms' screens, which change for different reasons.
- Where the git code both apps share lives:
  - **Its own project beside Sync, which each app's composition root creates and hands to Sync through a git interface Sync publishes**: one more project, a git interface in Sync's public interface to review, and a new kind of project in these rules.
  - **A general git interface and its implementation in the shared platform**: only Sync uses git, and code used by one module stays in that module. Sync would use an interface it doesn't own, and every git change, even a private one, would become a platform change, which is architecture.
- Who chooses the code inside a module:
  - **The composition root chooses every concrete implementation, with an exception in AGENTS.md for each module that picks its own, recorded on this page**: AGENTS.md, which loads into every session, would grow with each exception. Limiting the rule to published interfaces needs no list.
- Where the file system interface and the time come from:
  - **Tasks publishes the file system interface, and the shared library gets a platform project only when a second module reads files**: that second module would move the interface out of Tasks' public interface.
  - **A clock interface of ours, such as one Tasks publishes beside the file system interface**: it would repeat TimeProvider, and one app would get the time two ways.
- How the file system interface reads and writes:
  - **Streams instead of whole files**: the fake gets harder to write, and a caller could leave a file half written.
  - **The [System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) package**: a new dependency with a far larger surface than the seven calls Tasks needs.
