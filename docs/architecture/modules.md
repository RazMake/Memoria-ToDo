# How code is split into modules

Status: Decided

Code is split into modules, one per capability, so a change or a mistake stays inside one module. This page owns the module rules and the module map; the [code design rules](../../AGENTS.md#code-design) cover the components inside a module.

## Behavior

Every module follows the rules below, and the [module map](#module-map) lists the modules. Each module's own design file decides what the module does.

### A module is one capability behind a small interface

- **One module, one design file.** A module does one thing a user would name, in one place: the shared library or one app. Its design file, under `docs/architecture/`, is its spec. When both apps need the same capability, each app has its own module of that name, with its own project and its own design file in that app's folder, `docs/architecture/desktop/` or `docs/architecture/android/`.
- **One C# project per module, in one folder** named after it, holds its code. Its tests live in a folder of the same name in its part's [test project](technology-stack.md#each-part-keeps-its-tests-in-one-test-project).
- **A small public interface.** Other code uses the module only through its public interface. Everything else in the module is `internal`, which the compiler hides from other projects.
- **A module's own tests may also use its internal classes that hold its logic**, such as a screen's view model or Sync's git-free rules, so unit tests can check that logic without a screen or real git. No other module's code or tests use them. The compiler can't stop that, since every module's tests in a part share one test project, so the reviewer checks it.
- **Two files list the public interface.** The [public API analyzer](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) keeps `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` in each project, listing every public declaration, and the build fails when they're out of date. A reviewer reads only the diff of those files to see every public-interface change.
- **It owns its data.** Its stored data, messages, and screen state are its own. Other modules reach them only through its public interface.
- **It keeps its failures.** A module reports its own failures, and the rest of the application keeps working.

### The platform serves every module and knows none

- **The platform is the code outside module projects and the composition root**: what every module shares, such as startup, settings, and storage access. It never names a module. The shared library's platform holds the [file system interface](#the-file-system-interface-reads-and-writes-whole-files), the [synced repository](#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), and the [contact address](#a-contact-address-has-one-home), and each app's platform holds its startup. The shared library's platform is one project, `Memoria.ToDo.Shared.Platform`, in `src/Shared/Platform/`.
- **Every module takes the time from .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)**, which gives the time and timers, and the composition root passes the system's. The shared library has no clock interface of its own.
- **Every project outside the modules marks its public interface as modules do.** The platform's projects, and each app's projects outside its modules, such as the one with its composition root, keep everything else `internal` and list their public interface in the two public API files.
- **Code shared by two modules moves to the platform**, as the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) requires. Code shared only inside one module stays in that module.
- **Only the composition root names modules.** Each app has one. It creates each module's parts and connects them, and it chooses the implementation of every interface a module or the platform publishes, as [AGENTS.md](../../AGENTS.md#code-design) says. A module wires its own `internal` code itself, as Sync does with its git code.
- **Each app's own implementations of shared interfaces live beside its composition root**, such as its image shrinking for Tasks, because they name a module's interface.
- **The git code both apps share lives inside Sync**, the only module that uses git. Sync's project references LibGit2Sharp and SSH.NET, which [How the apps fit together](overview.md#both-apps-reach-git-through-libgit2sharp-and-one-ssh-transport) picks, and keeps its git code `internal`. Sync's unit tests check its git-free rules and its worker through its internal classes, with a fake of its internal git interface; tests that run real git are integration tests. How two versions of a task merge belongs to Tasks, which doesn't touch git, so those rules keep unit tests.

### The file system interface reads and writes whole files

Modules reach files on the device only through the shared platform's file system interface, so their tests run on a fake with no real I/O. It works on whole files by full path, and names no module and no folder of its own.

- **A write replaces the whole file, or leaves the old one whole.** It writes a temporary file beside the target, then replaces the target with it, so a crash or a full disk never leaves half a task file.
- **Each call fails with the system's exception**, such as an `IOException`, and never returns a fallback. Only a missing file or folder is quietly fine: reading a missing file, or a file in a missing folder, fails with `FileNotFoundException`, but deleting a missing file or folder, or listing a missing folder, does nothing. A path of the wrong kind, such as listing a file or deleting a folder as a file, fails with an `IOException`, and so does any access failure.
- **Each app implements it beside its composition root**, as [above](#the-platform-serves-every-module-and-knows-none) says: the desktop app in `src/Desktop/App/`.
- **One fake, `FakeFileSystem`, keeps files in memory.** It sits in `Memoria.ToDo.Shared.Fakes`, in `tests/Shared/Fakes/`, as the [technology stack](technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test) places fakes.
- **One contract test keeps the fake honest.** Its checks are written once, as an abstract test class beside the fake. The shared platform's tests run them on the fake as unit tests, and the desktop composition root's tests, in `tests/Desktop/Tests/App/`, run them on the Windows implementation as integration tests. So the fake can't drift from the real thing.

Approved: the [filesystem source contract](../../src/Shared/Platform/IFileSystem.cs) also provides the physical
directory-containment check. Each app resolves its filesystem's aliases; modules never use platform-native APIs
or substitute textual paths for physical containment. The source defines equality, missing suffixes, and failures.
Sync calls this I/O operation only on its owned worker, before effects; creation remains I/O-free.

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

### The synced repository gives every store one clone, one lock, and one reload

Tasks and [Contacts](contacts.md#contacts-is-a-shared-module-of-its-own) both keep files in the same clone, and Sync updates both. So the shared platform, not Tasks, owns what they share: the selected folder, the lock that runs one change at a time, the reload after a sync, the list of skipped files, and the notice that a local change needs a sync. Each store is a part of the repository; neither knows the other, and Sync knows the repository, not how each store loads.

- **Selecting a folder loads every part, in the order they were added**, under the lock, then raises each part's notices after releasing it. With no folder, each part is empty. A part that fails to load doesn't stop the others; the selection then fails with every failure.
- **A store runs each read and write under the lock**, and after a write that Sync should carry, reports a local save, outside the lock.
- **Sync's update keeps today's rules**: it checks the folder is still the selected one before running, runs the update under the lock, reloads every part even when the update fails, raises no local-save notice, and raises the parts' notices after releasing the lock. A failed update and a failed reload are kept together.
- **The skipped files are every part's**, in the order the parts were added, so Sync's status lists Tasks' and Contacts' problems alike.
- **The composition root creates one repository**, gives it to each store and to Sync, and selects the folder through it.

Proposed, each a decision to confirm: these declarations, and that `ISkippedRepositoryFile` and `NoRepositoryFolderException` move from Tasks into the platform unchanged. This is not implemented:

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// One store's files in the clone, as the repository loads them.
public interface IRepositoryPart
{
    /// Reads the folder's files, or empties the part for null. Runs under the lock; raises no notice.
    void Load(string? folder);

    /// Raises the part's change notices. Runs after the lock is released.
    void Loaded();

    /// The files the latest Load skipped.
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }
}

/// What a store uses.
public interface IRepositoryAccess
{
    /// The selected folder, or null.
    string? Folder { get; }

    /// Adds a part. Throws InvalidOperationException after the first Select.
    void Add(IRepositoryPart part);

    /// Runs the call under the lock with the selected folder, which may be null. Reentrant on one thread.
    T Locked<T>(Func<string?, T> call);
    void Locked(Action<string?> call);

    /// Raises LocalSaved. Call it outside the lock, after a change Sync should carry.
    void ReportLocalSave();
}

/// What Sync uses.
public interface ISyncedRepository
{
    string? Folder { get; }

    /// Throws InvalidOperationException, before update runs, when expectedFolder isn't selected.
    /// Reloads every part under the lock, also when update fails; AggregateException keeps both failures.
    void Update(string expectedFolder, Action update);

    /// Every part's skipped files, in the order the parts were added.
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }

    /// Raised after a store's local change, outside the lock.
    event EventHandler? LocalSaved;
}

public sealed class SyncedRepository : IRepositoryAccess, ISyncedRepository
{
    /// Loads every part with the folder, or empties them for null, then raises their notices.
    /// Throws the part's exception, or AggregateException when several fail.
    public void Select(string? folder);
}
```

### A contact address has one home

A mention's link address names a contact by ID, as `contact:<id>`. [Contacts](contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template) makes and reads it, and [Rendering](rendering.md#a-mention-shows-on-a-colored-background) recognizes it, so its format lives in the shared platform. Proposed, a decision to confirm. This is not implemented:

```csharp
namespace Memoria.ToDo.Shared.Platform;

public static class ContactAddress
{
    /// "contact:" followed by the ID. Throws ArgumentException for an empty ID.
    public static string Of(string id);

    /// The ID in a contact address, or null when the address isn't one.
    public static string? IdOf(string address);
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

The shared library has five modules:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Tasks | Task files and their format, the in-memory list, every task operation, tags and search, task links, how two versions of a task merge, images and their shrinking, this device's draft of a new task, [shared settings](../../src/Shared/Tasks/ISharedTaskSettings.cs), and [each tag's color](tasks.md#each-tags-color-is-kept-in-one-synced-file) and [place in the tags' order](tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips) | [Tasks](tasks.md) | None |
| Contacts | Contact files, their data types and mention templates, relations, search, saving, the group order, how two versions merge, and mentions' stored form and live text | [Contacts](contacts.md) | None |
| Sync | Commit, fetch, merge, and push in the background through its own git code on LibGit2Sharp and SSH.NET, and the device's repository settings, SSH key, and known host keys | [Sync through git](sync.md) | Tasks, Contacts |
| Editing | Formatting, snippet, and list commands on plain text and a selection, and the snippet picker's and mention list's rules | [Editing](editing.md) | None |
| Rendering | Markdown to an HTML page for the web view and to styled text for titles and the editor, the color palette, the 30 tag colors and each tag's default color, the text look of headings, links, mentions, code, and italic text, and which links may open in the browser | [Rendering](rendering.md) | None |

The desktop app has seven modules of its own:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Shell | The tray icon and its menu, the four global shortcuts, one running instance, what each start shows, starting and stopping Updates, and sync status | [Shell](desktop/shell.md) | Task editor, Task list, Contact list, Configure, Sync, Updates |
| Task editor | The Add window, and the title editor, description editor, tag control, and tag badges Task View uses, with the "@" list and mention chips | [Task editor](desktop/task-editor.md) | Tasks, Contacts, Editing, Rendering, Updates |
| Task list | Task View: open tasks grouped by tag, in groups the user orders by dragging and that collapse, with a line showing where a drag lands, beside the selected task's description, edited there, with colored tags that change without Edit, titles edited in place, mentions with their contacts' current details, search, resolving, deleting, resolved tasks, and the Sync button and the version in the status bar | [Task list](desktop/task-list.md) | Tasks, Contacts, Rendering, Task editor, Formatted view, Sync, Updates |
| Contact list | The Contacts window: contacts grouped by file, in groups the user orders by dragging, beside the selected contact's details, with search, editing, adding, and deleting | [Contact list](desktop/contact-list.md) | Contacts |
| Configure | The four global shortcuts and the deletion delay, which every device shares; the Close window and Save shortcuts; starting with Windows; and Sync's settings and status | [Configure](desktop/configure.md) | Tasks, Rendering, Sync, Updates |
| Formatted view | A task's title and description as a reader sees them, with no Markdown characters, and opening their web links in the default browser | [Formatted view](desktop/formatted-view.md) | Rendering |
| Updates | Checking for a new version, the status bar Add and Task View share, the update card's state, What is new, Auto update, and restarting the app into the new version | [Updates](desktop/updates.md) | Formatted view |

Proposed, each a decision to confirm:

- Shell may use Sync, so the tray shows sync status without going through Configure.
- The Task list may use Sync, for its Sync button and the sync when Task View opens.

The user decided that [Contacts](contacts.md) is a shared module of its own, that the desktop has a Contacts window with a fourth global shortcut, and that the Android Task editor and Task list mention contacts too. Proposed, a decision to confirm: the desktop module keeps the name Contact list, beside the shared Contacts, as the Task list sits beside Tasks; it uses only Contacts, since its split panes, search band, and drag come from the desktop platform.

The Android app has four modules with the same names as the desktop app's first four, separate from them:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Shell | Where the app icon, the app shortcut, and the home-screen widget lead; sync when the app opens and in the background; updates; and the theme | [Shell](android/shell.md) | Task editor, Task list, Configure, Sync |
| Task editor | The full-screen Add screen with its toolbar and tag chips, the "@" list and mention chips, which also edits a task from Task View | [Task editor](android/task-editor.md) | Tasks, Contacts, Editing, Rendering |
| Task list | Task View: open tasks in rank order, expanded in place, with swipes, reordering, the tag filter with its chips in the tags' order, search, resolved tasks, mentions with their contacts' current details, the floating Add button, and the top tasks the widget shows | [Task list](android/task-list.md) | Tasks, Contacts, Rendering, Task editor, Sync |
| Configure | The phone's key and repository, the shared deletion delay, the swipe direction, and Sync's status | [Configure](android/configure.md) | Tasks, Sync |

Proposed, each a decision to confirm:

- On Android, Shell may use Sync, for the sync when the app opens and the background sync.
- On Android, the Task list may use Sync, for its sync status line.

## Public interface

None. This page sets rules and the module map, not a module; each module's design file states its own public interface. The shared platform's declarations are [above](#the-platform-serves-every-module-and-knows-none).

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
