# Shared platform: the file system, device store, and clock the shared modules use

Status: Decided

The shared platform is the code every shared module may use: one interface for files, one store for files that stay on this device, one clock, and the text rules more than one module follows. [Modules](modules.md#the-platform-serves-every-module-and-knows-none) puts them here; each app supplies the file system's implementation and the store's folder, as [How the apps fit together](overview.md#two-apps-share-one-c-library) decides.

## Behavior

### Files are read and written whole, through one interface

Shared modules reach files only through the [file system interface](#public-interface), so their tests replace it with a fake and need no disk, as [AGENTS.md](../../AGENTS.md#test-design) requires for unit tests. Each app implements it beside its composition root, and its real implementation has integration tests.

- **A file is read or written whole.** Task files and images are small, at most 1 MB, so no caller needs streams.
- **A write never leaves a half-written file.** The bytes go to a temporary file beside the target, which then replaces the target. A crash leaves the old file whole, and at worst the temporary file `<name>.tmp`, which the next write of that file replaces. Tasks reads only `.json` task files, and [Sync ignores](sync.md#each-sync-commits-fetches-merges-and-pushes-one-branch) `.tmp` files, so one left behind never syncs.
- **Paths are full paths.** Each module builds them from the folders the composition root gives it, such as the clone's.

### Files that stay on this device live in the device store

The device store is one folder for files that belong to this device and never sync, such as the [draft](tasks.md#a-draft-waits-on-this-device-until-its-saved) Add keeps. It sits outside the clone, so git never sees it. Each app's composition root names the folder: on the desktop, `%LocalAppData%\Memoria-ToDo`, which already keeps [the repository folder's path](desktop/configure.md#the-sync-settings-section-sets-the-devices-clone); on Android, the app's private storage.

Proposed, each a decision to confirm:

- **The device store is a class in the shared platform, not a port.** It turns a file's name into its full path in the store's folder, and modules read and write the file through the [file system interface](#public-interface), so their tests use the same fake. Only the folder differs between apps, and the composition root passes it in.
- **A name is relative to the store's folder**, such as `Tasks/draft.json`. An empty name, a full path, or a name that leads out of the folder, such as one with `..`, is rejected before any file is touched.
- **Each module keeps its files in a folder of the store named after the module**, such as `Tasks/`, so two modules never pick the same name.

### The clock is .NET's `TimeProvider`

The shared modules' clock is .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider), which [Shell](desktop/shell.md#public-interface) also uses for its update checks. The platform publishes no clock type of its own. The composition root passes `TimeProvider.System`, and tests pass the [shared fake](technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test), which moves time only when told.

### Text rules two modules follow live here once

Editing and Tasks may not use each other, as the [module map](modules.md#module-map) says, so a text rule both follow lives here, as the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) requires:

- **Typed text matches a name as [Editing's snippet picker](editing.md#typing-a-slash-opens-the-snippet-picker) matches it.** Tasks' [tag suggestions](tasks.md#tags-and-search-ignore-case) use the same rule. A word starts at the beginning of the name or after a space, and empty typed text matches every name.
- **Text is made to show exactly as given in Markdown by a backslash before each character Markdown could read as formatting**, which CommonMark allows before any ASCII punctuation ([backslash escapes](https://spec.commonmark.org/0.31.2/#backslash-escapes)). Editing's [task reference](editing.md#snippets-insert-common-blocks) uses it for the title, and Tasks for an [image's alt text](tasks.md#images-are-at-most-1-mb-and-live-beside-their-task).

## Public interface

Every shared module may use the file system interface, the device store, and the text rules:

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
    void WriteFile(string path, System.ReadOnlySpan<byte> content);

    /// The full paths of the files directly in folder, in no set order; none when the folder doesn't exist.
    System.Collections.Generic.IReadOnlyList<string> ListFiles(string folder);

    /// Moves a file, creating the target's folders. Throws IOException when the target exists.
    void MoveFile(string sourcePath, string targetPath);

    /// Deletes a file. Does nothing when there's none.
    void DeleteFile(string path);

    /// Deletes a folder and everything in it. Does nothing when there's none.
    void DeleteFolder(string path);
}
```

Proposed, a decision to confirm: the device store's class.

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// The folder for files that stay on this device and never sync, outside the clone.
/// Modules read and write its files through IFileSystem.
public sealed class DeviceStore
{
    /// Throws ArgumentException unless folder is a full path. Touches no file.
    public DeviceStore(string folder);

    /// The full path of the file name names, relative to the store's folder, such as "Tasks/draft.json".
    /// Throws ArgumentException when name is empty, a full path, or leads out of the folder.
    public string PathOf(string name);
}
```

The two text rules:

```csharp
namespace Memoria.ToDo.Shared.Platform;

/// How typed text matches a name, such as a snippet's or a tag's.
public static class TypedTextMatch
{
    /// True when name, from the start of any of its words, starts with typed, ignoring case.
    /// A word starts at the beginning of name or after a space. Empty typed text matches every name.
    public static bool Matches(string name, string typed);
}

/// Text placed into Markdown.
public static class MarkdownText
{
    /// text with a backslash before each character Markdown could read as formatting, so it shows exactly as given.
    public static string Escape(string text);
}
```

## Failure modes

- **A file can't be read or written**, such as on a full disk: the method throws the system's error, and the module that called it reports it, as its own design file says.
- **A device store name is invalid**: the store throws before any file is touched. It's a programming error, since each module names its own files.

## Ruled out

- **Shared modules use .NET's file classes directly**: their file rules, such as skipping a broken task file, would need real files, so they'd be integration tests, not unit tests.
- **Streams instead of whole files**: more to implement and fake, and each caller would have to keep its own writes whole.
- **One implementation in the shared platform now**: only the desktop app exists, and [How the apps fit together](overview.md#two-apps-share-one-c-library) has each app implement the file system.
- **A clock interface of our own**: `TimeProvider` already gives the time and timers, with no package.
- Where files that stay on the device live:
  - **A device folder only Tasks gets, for its draft**: you chose a store in the shared platform, which any module's device-only files can use.
  - **A device store with its own read, write, and delete methods**: it would repeat the file system interface, and tests would need a second fake.
  - **A port each app implements**: both apps would turn a name into a path the same way; only the folder differs, which the composition root passes in.
- Where a text rule two modules follow lives:
  - **A copy in each module**: the [no-duplication rule](../../AGENTS.md#core-rule-no-duplication) forbids it.
  - **Tasks or Editing uses the other for it**: a change to the [module map](modules.md#module-map) for one small rule, and Tasks would depend on editor commands, or Editing on task files.
