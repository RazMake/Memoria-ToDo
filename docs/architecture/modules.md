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

- **The platform is the code outside module projects and the composition root**: what every module shares, such as startup, settings, and storage access. It never names a module. The shared library's platform holds the [file system interface](#the-file-system-interface-reads-and-writes-whole-files), the [synced repository](#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload), the [JSON style and file rules](#stores-share-one-json-style-and-one-set-of-file-rules) every store follows, the [exception for a skipped file](#a-call-that-needs-a-skipped-file-throws-one-type), and the [contact address](#a-contact-address-has-one-home), and each app's platform holds its startup. The platform holds no log interface: every module and platform project depends on the [Serilog](https://serilog.net) package and logs through its `ILogger`, as [Logging](logging.md#every-module-logs-through-serilogs-logger) decides. The shared library's platform is one project, `Memoria.ToDo.Shared.Platform`, in `src/Shared/Platform/`.
- **Every module takes the time from .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)**, which gives the time and timers, and the composition root passes the system's. The shared library has no clock interface of its own. The one exception is the time on a log entry, which Serilog stamps, as [Logging](logging.md#every-module-logs-through-serilogs-logger) says.
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

- **Selecting a folder loads every part, in the order they were added**, under the lock, then raises each part's notices after releasing it. With no folder, each part is empty.
- **A selection changes all or nothing**, the user's choice, since every part's files live in the one clone and change through the one sync, as Tasks' do today. Each part first reads the folder without using it; only when every part read does each make what it read current. A part that fails to read doesn't stop the others from reading; the selection then changes nothing, keeping the old folder, every part as it was, and no notices, and fails with every failure.
- **A store runs each read and write under the lock**, and after a write that Sync should carry, reports a local save, outside the lock.
- **A change that raises notices never runs inside another lock call**, the user's choice: it takes the lock through `LockedChange`, which fails before running when the thread already holds the lock. Otherwise the change would save, then fail to report it, and its notices would run under the lock.
- **Making what a part read current never fails**, the user's choice, so a selection can't change one part and not another. Everything that can fail runs in the read.
- **Every reloaded part raises its notices, even when another part's notices throw.** The failures are kept, after the selection's or update's own.
- **Sync's update keeps today's rules**: it checks the folder is still the selected one before running, runs the update under the lock, reloads every part even when the update fails, raises no local-save notice, and raises the parts' notices after releasing the lock. A failed update and a failed reload are kept together.
- **The skipped files are every part's**, in the order the parts were added, so Sync's status lists Tasks' and Contacts' problems alike.
- **The composition root creates one repository**, gives it to each store and to Sync, and selects the folder through it.

The user confirmed these declarations, and that `ISkippedRepositoryFile` and `NoRepositoryFolderException` move from Tasks into the platform with the same members. Their summaries stop naming tasks and Sync, since the platform names no module:

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// One store's files in the clone, as the repository loads them.
public interface IRepositoryPart
{
    /// Reads the folder's files, or nothing for null, without using them, and returns the call that
    /// makes what it read current, or empties the part for null. Both run under the lock and raise
    /// no notice. A failed read leaves the part as it was. The returned call never throws.
    Action Load(string? folder);

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

    /// Adds a part. Throws InvalidOperationException after the first Select, even one that failed,
    /// and ArgumentException when the part was already added.
    void Add(IRepositoryPart part);

    /// Runs the call under the lock with the selected folder, which may be null. Reentrant on one thread.
    T Locked<T>(Func<string?, T> call);
    void Locked(Action<string?> call);

    /// Runs a change that raises notices under the lock with the selected folder.
    /// Throws InvalidOperationException, before running, when the thread already holds the lock.
    T LockedChange<T>(Func<string?, T> call);
    void LockedChange(Action<string?> call);

    /// Raises LocalSaved on the calling thread, after a change Sync should carry.
    /// Throws InvalidOperationException when called under the lock.
    void ReportLocalSave();
}

/// What Sync uses.
public interface ISyncedRepository
{
    string? Folder { get; }

    /// Throws InvalidOperationException, before update runs, when expectedFolder isn't selected
    /// or when called under the lock.
    /// Reloads every part under the lock, also when update fails, then raises the notices of each part
    /// that reloaded; a part that failed keeps its state and raises none. One failure is thrown as it is;
    /// several make an AggregateException: the update's first, then each part's reload in the order they
    /// were added, then each part's notices in the same order.
    void Update(string expectedFolder, Action update);

    /// Every part's skipped files, in the order the parts were added.
    IReadOnlyList<ISkippedRepositoryFile> SkippedFiles { get; }

    /// Raised after a store's local change, outside the lock.
    event EventHandler? LocalSaved;
}

public sealed class SyncedRepository : IRepositoryAccess, ISyncedRepository
{
    /// Loads every part with the folder, or empties them for null, then raises their notices.
    /// Throws, before loading: ArgumentException for a blank folder; InvalidOperationException under the lock.
    /// Throws the part's exception, or AggregateException when several fail. When the reads succeed,
    /// every part raises its notices; failed notices are thrown the same way, in the order parts were added.
    public void Select(string? folder);
}
```

### Steps that must all run throw their failures together

The synced repository raises every part's notices even when one throws, and [Contacts](contacts.md#failure-modes) raises Changed and reports a local save even when one of them throws. Both then throw by one rule, so the user decided it lives in the platform, in one small type both use:

- **Each step is attempted**, even when an earlier one failed.
- **One failure is thrown as it is**, keeping its stack trace, so a caller catching its type still catches it.
- **Several make an `AggregateException`**, in the order they happened.

The user decided this declaration:

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// Collects the failures of steps that must all run, then throws them together.
public sealed class FailureList
{
    /// Runs the step, and keeps its exception instead of throwing it.
    public void Attempt(Action step);

    /// Keeps a failure caught elsewhere. Throws ArgumentNullException for null.
    public void Add(Exception failure);

    /// Does nothing when no failure was kept. Throws the one failure as it is, keeping its stack trace,
    /// or an AggregateException with every failure, in the order they were kept.
    public void ThrowIfAny();
}
```

### Stores share one JSON style and one set of file rules

Tasks and [Contacts](contacts.md#contacts-is-a-shared-module-of-its-own) write their files the same way and skip the same bad files, so those rules live in the shared platform, and each store uses them. The user decided to move them out of Tasks, with Tasks moved onto them and no change in its behavior:

- **One JSON style.** Every file Tasks and Contacts write in the clone uses two-space indents, `\n` line breaks, characters outside ASCII as they are, UTF-8 with no byte-order mark, and no line break at the end. So one change shows as a few changed lines in git. The user decided the rule covers only the stores' files: other files on the device, such as Sync's [known-hosts file](sync.md#device-metadata-has-one-owner-and-never-travels-in-task-commits) and the [Windows key file](sync.md#the-windows-key-file-is-encrypted-and-only-the-users-account-unlocks-it), keep their own format. The user decided Tasks' [draft](tasks.md#a-draft-waits-on-this-device-until-its-saved), a store file kept on the device, uses the style too. Bytes that aren't valid JSON make a file unreadable.
- **One version rule.** A versioned file's `version` is a whole number from 1. A newer app reads every older version. A version newer than the app reads makes the file unreadable, and it needs a newer app. A missing version, or any other value, makes it unreadable.
- **One skip rule.** A store skips a file that's there but can't be read: its parse fails, or reading it fails with an I/O or access error. It leaves the file unchanged and lists it in its skipped files, with the failure's message and whether it needs a newer app. A missing file isn't skipped; each store decides what a missing file means.
- **One relative-path rule.** A path relative to the clone uses `/`, is never rooted, and has no `.` or `..` part. Each store's file rules reject any other path with `ArgumentException`, before looking at any bytes.

Two implementation choices follow from the first bullet. .NET's built-in relaxed JSON encoder writes emoji and other characters beyond the Basic Multilingual Plane as `\u` escapes, so the platform uses its own small encoder to keep them as they are, which needs `AllowUnsafeBlocks` in the platform project. And parsing checks that the bytes are valid UTF-8 first, because .NET's parser accepts some invalid bytes inside strings. Decisions to confirm.

Proposed, a decision to confirm: **parsing also rejects text that doesn't decode**, with `InvalidDataException`, as it rejects bytes that aren't JSON. A string value or a property name, at any depth, whose escapes make invalid text, such as a lone surrogate `"\uD800"`, makes the file unreadable; an escaped surrogate pair, such as `"\uD83D\uDE00"`, is valid. .NET's parser accepts such a file, and reading the string later throws `InvalidOperationException`, which the skip rule above doesn't cover, so one bad file would stop a whole load. Every store reads through `JsonFile.Parse`, so this one check makes Tasks and Contacts skip and report such a file, and their merges reject such a side as unreadable.

The user confirmed these declarations, and the platform implements them:

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// The JSON style of every store file the apps write, and the rules for reading one.
public static class JsonFile
{
    /// The bytes of the one JSON value write writes: two-space indents, "\n" line breaks, characters outside
    /// ASCII as they are, UTF-8 with no byte-order mark, and no line break at the end.
    /// Throws InvalidOperationException when write writes no value, or leaves it unfinished.
    public static byte[] Write(Action<Utf8JsonWriter> write);

    /// The parsed JSON value, of any kind. Throws InvalidDataException when bytes aren't valid JSON,
    /// or a string value or property name in it doesn't decode to valid text.
    public static JsonDocument Parse(byte[] bytes);

    /// The object's "version": a whole number from 1 to newest. Throws InvalidDataException when file isn't
    /// an object, or its version is missing, not a whole number, or below 1; and when the version is above
    /// newest, which SkippedFile.Of then reports as needing a newer app.
    public static int RequireVersion(JsonElement file, int newest);
}

/// What reading one file found. Failure is set when the file is there but can't be read.
public sealed record FileLoad<T>(T Value, bool Present, InvalidDataException? Failure)
{
    /// Reads and parses the file, and never throws for a bad file. A missing file gives absent, not Present.
    /// A failure SkippedFile.Skips gives absent, Present, and the Failure: parse's InvalidDataException as it is,
    /// or an InvalidDataException with the same message around any other. Other exceptions pass through.
    public static FileLoad<T> Read(IFileSystem files, string path, Func<byte[], T> parse, T absent);

    /// The value. Throws InvalidDataException around Failure when the file can't be read.
    public T Require();
}

/// How a store skips a file it can't read: it leaves the file unchanged and reports it.
public static class SkippedFile
{
    /// Whether a failure to read or parse a file that's there means the store skips the file:
    /// InvalidDataException, IOException, or UnauthorizedAccessException.
    public static bool Skips(Exception failure);

    /// The report for the skipped file at relativePath: the failure's message as the reason, and RequiresUpdate
    /// true when the failure is JsonFile.RequireVersion finding a version above newest.
    public static ISkippedRepositoryFile Of(string relativePath, Exception failure);
}

/// A path relative to the clone, as Git names it.
public static class RepositoryPath
{
    /// The path's parts, split at '/'. Throws ArgumentNullException for null, and ArgumentException when the
    /// path is rooted, such as "/a", "\a", "C:/a", or anything this system counts as rooted, or has a "." or ".." part.
    public static IReadOnlyList<string> Split(string relativePath);
}
```

### A call that needs a skipped file throws one type

Tasks and Contacts each refuse some calls when a file the call needs was skipped at the latest load. The user decided that the store that owns the file decides this and says so with a typed exception, so a caller such as the [local API](local-api-contract.md#each-failure-maps-to-one-grpc-status) never matches file paths or messages. Both stores need the same type, so it lives in the platform:

- **It names the skipped file** with the report the store's skipped files list, so its path and reason match the sync status.
- **It's thrown before anything is written**, so the call changes nothing.
- **It's an `InvalidOperationException`**, so a caller that already catches that type keeps working.

Proposed, a decision to confirm: this declaration.

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// A call needs a store file that was skipped at the latest load, so it changed nothing.
/// The message names the file's relative path and the reason it was skipped.
public sealed class SkippedFileException : InvalidOperationException
{
    /// Throws ArgumentNullException for null.
    public SkippedFileException(ISkippedRepositoryFile file);

    /// The skipped file, as the store's skipped files list it.
    public ISkippedRepositoryFile File { get; }
}
```

### A contact address has one home

A mention's link address names a contact by ID, as `contact:<id>`. [Contacts](contacts.md#a-mention-is-a-link-that-names-its-contact-and-its-template) makes and reads it, and [Rendering](rendering.md#a-mention-shows-on-a-colored-background) recognizes it, so its format lives in the shared platform. The user confirmed this declaration, and the platform implements it:

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

The four tables below list each part's modules, their design files, and the modules each may use. Adding, removing, or changing a row is an architecture change. An [architecture test](technology-stack.md#architecture-tests-check-the-module-map) reads the "May use" columns, so a change to the tables' format needs a change to that test. Each module's design file comes from its row in the [design backlog](README.md#design-backlog).

The shared library has five modules:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Tasks | Task files and their format, the in-memory list, every task operation, tags and search, task links, how two versions of a task merge, images and their shrinking, this device's draft of a new task, [shared settings](../../src/Shared/Tasks/ISharedTaskSettings.cs), and [each tag's color](tasks.md#each-tags-color-is-kept-in-one-synced-file) and [place in the tags' order](tasks.md#each-tags-place-orders-task-views-groups-and-androids-tag-chips) | [Tasks](tasks.md) | None |
| Contacts | Contact files, their data types and mention templates, relations, search, saving, the group order, how two versions merge, and mentions' stored form and live text | [Contacts](contacts.md) | None |
| Sync | Commit, fetch, merge, and push in the background through its own git code on LibGit2Sharp and SSH.NET, and the device's repository settings, SSH key, and known host keys | [Sync through git](sync.md) | Tasks, Contacts |
| Editing | Formatting, snippet, and list commands on plain text and a selection, and the snippet picker's and mention list's rules | [Editing](editing.md) | None |
| Rendering | Markdown to an HTML page for the web view and to styled text for titles and the editor, the color palette, the 30 tag colors and each tag's default color, the text look of headings, links, mentions, code, and italic text, and which links may open in the browser | [Rendering](rendering.md) | None |

The desktop app has nine modules of its own:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Shell | The tray icon and its menu, the four global shortcuts, one running instance, what each start shows, starting and stopping Updates, the Local API, and the MCP server, and sync status | [Shell](desktop/shell.md) | Task editor, Task list, Contact list, Configure, Sync, Updates, Logs, Local API, MCP server |
| Task editor | The Add window, and the title editor, description editor, tag control, and tag badges Task View uses, with the "@" list and mention chips, and the "@" list for a plain text box that Task View's search box uses | [Task editor](desktop/task-editor.md) | Tasks, Contacts, Editing, Rendering, Updates, Contact list |
| Task list | Task View: open tasks grouped by tag, in groups the user orders by dragging and that collapse, with a line showing where a drag lands, beside the selected task's description, edited there, with colored tags that change without Edit, titles edited in place, mentions with their contacts' current details, search, resolving, deleting, resolved tasks, and the Sync button and the version in the status bar | [Task list](desktop/task-list.md) | Tasks, Contacts, Rendering, Task editor, Formatted view, Sync, Updates, Contact list |
| Contact list | The Contacts window: contacts grouped by file, in groups the user orders by dragging, beside the selected contact's details, with search, editing, adding, and deleting; and the contact card a mention shows | [Contact list](desktop/contact-list.md) | Contacts, Rendering |
| Configure | The four global shortcuts and the deletion delay, which every device shares; the Close window and Save shortcuts; starting with Windows; AI assistant access; and Sync's settings and status | [Configure](desktop/configure.md) | Tasks, Rendering, Sync, Updates, MCP server |
| Formatted view | A task's title and description as a reader sees them, with no Markdown characters, and opening their web links in the default browser | [Formatted view](desktop/formatted-view.md) | Rendering |
| Updates | Checking for a new version, the status bar Add and Task View share, the update card's state, What is new, Auto update, and restarting the app into the new version | [Updates](desktop/updates.md) | Formatted view |
| Logs | The Logs window: the app's log entries, newest first, with filters by time, severity, and component, and a quick search; and the one log filter the window and the MCP server share | [Logs](desktop/logs.md) | None |
| MCP server | Lets an AI assistant on this PC do the user's task, contact, sync, and settings operations through the Model Context Protocol while the app runs, over a named pipe only this user can reach | [MCP server](desktop/mcp-server.md) | Tasks, Contacts, Sync, Updates, Logs |
| Local API | The gRPC server on a named pipe that lets your own programs on this PC reach tasks, contacts, sync, and shared settings | [Local API](local-api.md) | Tasks, Contacts, Sync, Rendering, Contract |

Proposed, each a decision to confirm:

- Shell may use Sync, so the tray shows sync status without going through Configure.
- The Task list may use Sync, for its Sync button and the sync when Task View opens.

The user decided that [Contacts](contacts.md) is a shared module of its own, that the desktop has a Contacts window with a fourth global shortcut, and that the Android Task editor and Task list mention contacts too. Proposed, a decision to confirm: the desktop module keeps the name Contact list, beside the shared Contacts, as the Task list sits beside Tasks; it uses Contacts, and Rendering only for the contact card's heading color, as below, since its split panes, search band, and drag come from the desktop platform.

The user decided that the [Logs window](desktop/logs.md) is a desktop module of its own, which uses no module, and that Shell may use it, for its tray item. The user also decided that its [log filter](desktop/logs.md#one-filter-decides-which-entries-match) is public, so the MCP server, which may already use Logs, filters the log by the same rules.

The user decided that the [MCP server](desktop/mcp-server.md) is a desktop module of its own, which Shell starts and stops, and which uses the modules whose operations it offers. The user also decided that Configure may use it, for the AI assistant access switch; and Start with Windows moves from Configure to the [desktop platform](desktop/README.md#start-with-windows-is-a-port-the-platform-publishes), so the MCP server uses it without using Configure. The MCP server uses no window module, so no loop forms.

The user decided that the Task editor and the Task list may use the Contact list, for the [contact card](desktop/contact-list.md#a-contact-card-shows-a-mentioned-contacts-details) a mention shows while the pointer rests on it. The Contact list uses no desktop module, so no loop forms. The other homes considered are in the [Contact list's Ruled out](desktop/contact-list.md#ruled-out).

The user decided that the contact card's heading shows in Rendering's heading color. Proposed, a decision to confirm: so the Contact list may use Rendering, for its text look. Rendering uses no module, so no loop forms.

The user decided the [Local API](local-api.md#the-contract-and-clients-form-a-part-of-their-own) module: Shell may use it, only to start and stop it, and it may use Rendering for the tag color names. Its project, `Memoria.ToDo.Desktop.LocalAPI`, is named from its row by the naming rules.

The user also decided a fourth part, **Api**, beside the shared library and the two apps. Its contract, `Memoria.ToDo.Api.Contract`, uses no module; its .NET client, `Memoria.ToDo.Api.Client`, may use only the contract; and the Local API may use the contract too. No other app module may use either. The Node.js client sits in the part too, outside the .NET build and the map.

The Api part has two modules:

| Module | What it does | Design file | May use |
| --- | --- | --- | --- |
| Contract | The `.proto` files of the local API's version 1, and the C# generated from them | [What programs can call](local-api-contract.md) | None |
| Client | The .NET client, `MemoriaClient.Connect`, which hides the pipe | [Local API](local-api.md#the-contract-and-clients-form-a-part-of-their-own) | Contract |

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
- What a selection leaves when one part fails to read:
  - **Switch anyway, and empty the failed part**: the selected folder would change while the desktop puts the old saved path back, so Configure would show one folder and the next start load another.
  - **Undo by loading the old folder again**: it reads twice, and a failed undo leaves parts on different folders.
- Where Contacts gets Tasks' JSON style and file rules:
  - **Move only the JSON writer settings, and let Contacts write its own skip and path checks**: a few dozen lines that could drift from Tasks'.
  - **Move nothing, and let Contacts copy the settings**: a style change would need two edits, which the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) forbids.
