# Logging: what the apps write down, and where

Status: Decided

The app writes a log so anyone can find out later what it did, why, and what went wrong, such as which files a "3 file(s) in the repository can't be read" notice means. Every module writes its entries through [Serilog](https://serilog.net)'s logger, and the desktop app keeps them in files on this PC for two weeks. This page owns how modules log, what every change must log, the desktop's log files and their limits, keeping secrets out, how errors no module handles reach the log, and the Serilog packages. The [Logs window](desktop/logs.md) shows the entries.

## Behavior

### Every module logs through Serilog's logger

Every module, shared and desktop, and later Android, logs through Serilog's `ILogger` interface, as the user decided. A module never opens a file to log, and never chooses where its entries go:

- **Each module's create method takes an `ILogger` as its last argument**, as the [public interface](#public-interface) lists. The composition root passes one logger, made once at start, to every module. It never uses Serilog's static `Log.Logger`.
- **The composition root names each module's logger after it**, with `logger.ForContext("SourceContext", name)`. `SourceContext` is Serilog's own property for where an entry comes from, and the [Logs window](desktop/logs.md#filters-narrow-the-list) reads it as the component. The names come from the [module map](modules.md#module-map), such as `Sync` or `Task list`. The shared and desktop platforms log as `Platform`, the composition root and its adapters as `App`, and the Logs window as `Logs`. A module never changes `SourceContext` itself, such as with `ForContext<T>()`, which would set it to a class name. Proposed: one name per module, not one per class, so the filter stays short. This is a decision to confirm.
- **An entry has a time, a level, a message, and, for a failure, the exception.** Modules write messages as Serilog message templates with named values, such as `"Added task {TaskId} {Title}"`, never as interpolated strings, so each value stays a field of its own in the file. They log plain values only, never a whole object through Serilog's `{@...}` capture, so an object that holds a key can't reach the log by accident. Proposed, a decision to confirm.
- **Serilog stamps each entry's time when the module writes it**, from the system clock, and the file keeps it in UTC to a ten-millionth of a second. This is the one time no module takes from the [TimeProvider](modules.md#the-platform-serves-every-module-and-knows-none): Serilog can't take a clock, and an entry's time can't be changed after it's made. So tests never check an entry's time. A recorded exception, a decision to confirm.
- **Writing an entry never throws, never waits for the disk, and works from any thread.** Serilog keeps its own failures away from the caller, and the desktop's writer works on a background thread, as [below](#a-slow-disk-never-holds-up-the-app). So logging can't slow the UI thread, Sync's worker, or a store's lock, and can't break the operation it describes.

### Five levels say what an entry records

Serilog has six levels. The app uses five of them:

- **Debug**: a choice the code made, with the values it chose from, such as whether Sync started a pass or waited, which side of a merge a task kept, or which screen a window opened on. It explains a result that's wrong but threw nothing.
- **Information**: an operation finished, or the app started or stopped.
- **Warning**: something was skipped, or will be tried again, and the app keeps working: a file that can't be read, a sync while offline, a shortcut another app holds, an update check that failed.
- **Error**: an operation the user or the app started didn't finish, or an error no module handled after which the app keeps running.
- **Fatal**: an error no module handled that [ends the app](#errors-no-module-handles-are-logged-before-theyre-shown).

The logger the composition root makes keeps Debug and up, as the user decided. So Debug entries are written to the files beside the others, and count toward the same [30 MB and two weeks](#the-desktop-app-keeps-two-weeks-of-logs-at-most-30-mb). Verbose isn't used, and the logger drops a Verbose entry before it's written: Debug already holds every choice the [third rule](#every-operation-failure-and-decision-is-logged-where-it-happens) asks for, and a finer level would invite the per-item detail that rule keeps out. Proposed, a decision to confirm.

### Every operation, failure, and decision is logged where it happens

The user asked for every operation, with sync, add, update, and remove first, and every error. The user also decided that every change keeps its feature traceable from the log, even when a result is wrong and nothing throws. Three rules cover this, so no module needs a list of its own:

- **The module that owns an operation logs it when it finishes**, at Information, naming what it changed, such as a task's ID and title.
- **The code that ends a failure's journey logs it**: a catch that turns it into a message, a status, a skipped file, a retry, or nothing at all. It logs the exception at Warning or Error, then does what it did before. A catch that wraps a failure and throws it on doesn't log, so each failure is logged once. A catch that treats an expected outcome as normal, such as a missing file meaning "none yet", a cancel while the app stops, or a commit with nothing in it, isn't a failure and doesn't log.
- **The code that makes a choice behind a result logs it at Debug**, naming the outcome it took and the values it chose from. So a reader can tell from the log alone why the app did what it did. The user decided this.

Shutdown uses one exception to the propagation rule: each lifetime logs a cleanup failure at Error, with its exception, before passing it on. This preserves the diagnosis if the next cleanup step hangs. The user chose a separate Fatal exit summary from the runner, without repeating the exception or its details. The [lifetime contract](../../src/Desktop/Platform/IAppLifetime.cs) requires this immediate reporting.

A choice is behind a result when it picks between outcomes a user could see, such as the examples under [Debug](#five-levels-say-what-an-entry-records). Proposed, a decision to confirm: a choice gets no Debug entry of its own when:

- **another entry already explains it**, such as an operation's Information entry that names the same values, or a failure's entry;
- **it's made per item, per frame, or per keystroke**: a choice made over many items logs once per operation, with counts, never once per task, per drawn frame, per key typed, or per timer tick that changes nothing;
- **no user could see its outcome**, such as a null check or the end of a loop.

Every change that adds or changes the app's behavior follows the three rules in the code it adds or changes, as [AGENTS.md](../../AGENTS.md#code-design) requires, and the reviewer checks it. Code no change touches isn't reworked to catch up. Proposed, a decision to confirm.

Messages are written for a person reading the log: they name the files, IDs, and counts involved. Task titles and contact names may appear, since the log never leaves the PC; descriptions and file contents don't, because they're long. Debug entries follow the same rules. Proposed, a decision to confirm.

Logging is the requirement here, so each module's tests check the entries it writes, through the [in-memory sink](#tests-read-entries-from-one-in-memory-sink): their level, their component, and the values they must name, such as a file's path, not their exact wording. They don't check whether code writes a Debug entry, as the user decided: a test per choice would add many tests for little benefit. The reviewer checks Debug entries by reading the code. The log's own tests still check that the log files keep Debug entries and that the Logs window shows them.

The table lists what each part logs at Information, Warning, and Error, beyond the failures the second rule covers. Debug entries have no list: each change logs the choices its own code makes.

| Part | Logs at Information | Logs at Warning or Error |
| --- | --- | --- |
| Platform: the [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload) | Each load of the clone, after a selection, a sync, or on the desktop a [change on disk](desktop/README.md#the-composition-root-reloads-the-clone-when-a-file-in-it-changes), with the folder and how many files each store skipped | Each skipped file, at Warning, with its path relative to the clone and its reason, and whether it needs a newer app. This is what names the "3 files". |
| Platform: [running the app](desktop/README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) | Start, with the app's version; the end, with its exit code | Each error no module handles, at Error or Fatal, as [below](#errors-no-module-handles-are-logged-before-theyre-shown) |
| Platform: [store failures](desktop/README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them) | | Each store failure it turns into a message, at Error, so every window that shows one logs it in one place |
| [Tasks](tasks.md) | Add, with a draft or without, save an edit, move, resolve, reopen, delete, each automatic deletion, keep and discard the draft, save an image, add an image to a task, and each change to the shared settings, tag colors, and tag order, with the task's ID and title, or the setting's old and new value | |
| [Contacts](contacts.md) | Save, delete, and move a group, with the contact's ID, name, and group file; move a mention template first, with the templates file | |
| [Sync](sync.md) | Each pass's start, with what started it, and its end, with what it fetched, merged, and pushed, and how long it took; each status change; each setup step: a new key, with its fingerprint only; a forgotten key, with its fingerprint, or without one when the key can't be opened; the device name; an accepted host key, with the host and fingerprint; and a clone or repository change, with its address and folder | Each file the merge skipped, at Warning, with its path and reason; offline, at Warning; every other failed pass, at Error, with its kind, details, and exception; a forgotten key that can't be opened, at Warning, with the exception. Logging never stops the key from being deleted. |
| [Shell](desktop/shell.md) | Each tray item and global shortcut used; a later start handing over to the running one; contacts becoming available or not; each sync notification it shows | A shortcut another app holds, at Warning |
| [Updates](desktop/updates.md) | Each check and what it found; each download's start and end; Auto update turned on or off; a restart into a new version | A check or download that failed; a link in What is new that won't open, at Warning. A failed restart is logged only by the platform, at Fatal, as an [error no module handles](#errors-no-module-handles-are-logged-before-theyre-shown). A failed check is no longer silent: today [the update runner](../../src/Desktop/Updates/UpdateRunner.cs) drops it. |
| [Configure](desktop/configure.md), [Task editor](desktop/task-editor.md), [Task list](desktop/task-list.md), [Contact list](desktop/contact-list.md) | Nothing of their own: their changes are Tasks', Contacts', Sync's, or App's operations, which log themselves | Each failure they catch and show, at Error, such as a kept draft that can't be read, or a link that won't open |
| App: the composition root and its adapters | Start with Windows turned on or off; the repository folder chosen; the clone watched for changes, with its folder, or no longer watched | Each failure an adapter catches, such as the web view's, the clipboard's, or the key file's; a log file the log source can't read, at Warning; entries the writer lost, at Warning, as [below](#a-log-that-cant-be-written-never-stops-the-app); a [watch of the clone](desktop/README.md#the-composition-root-reloads-the-clone-when-a-file-in-it-changes) that can't start or stops, at Warning; a reload after a change on disk that fails, at Error |
| [Logs window](desktop/logs.md) | | A log folder it can't list, at Error |
| [Local API](local-api.md) | Each call's end, with its method, the IDs it names, its status, and its duration; the server's start and stop, with the pipe's name; each stream's open and close | A call that ends with a status the program caused, such as not found, at Warning; an internal failure, at Error, with the exception; a server that can't start, such as when another process holds the pipe's name, at Error |
| [MCP server](desktop/mcp-server.md) | Listening started, with the pipe's name, and stopped; the switch turned on or off; each connection opened and closed; each tool call's end, with the tool's name and the IDs it names | A tool's invalid input, at Warning; a switch file it can't read or that holds another value, at Warning; a failed operation, at Error; a pipe it can't make, at Error. Never a description, a contact's fields, or an image's bytes. |
| The [MCP server's bridge](desktop/mcp-server.md#an-assistant-reaches-it-through-a-pipe-and-a-bridge-the-app-provides) | Nothing in the log files, which the running app holds | Its own failures, on its standard error, which the assistant shows |

The [Formatted view](desktop/formatted-view.md) passes each failure to the window that shows it, which logs it. Its [hover cards](desktop/README.md#a-hover-card-shows-while-the-pointer-rests-on-a-part) use the caller's logger for their Debug entries.

The user decided that a sync pass's start and end are entries of their own, apart from the status changes. The start carries a `Trigger` value: `Start`, `Manual`, `Edit`, `Retry`, or `Periodic`. The end carries `Fetched` (the remote had new commits), `Merged` (they were combined with local ones), and `Pushed` (local commits went up), each true or false. It also carries `Skipped`, the count of files the merge skipped, and `DurationMs`. A pass that fails still logs its end, after its failure entry, with `DurationMs` only: what Git did before the failure isn't known. Git reports no finer counts without extra work on every pass. A skipped file's Warning carries `RequiresUpdate`, true or false. A tag-order change names the tag only, since its old and new places are internal ranks. A group move names the group's file.

The notifications, the tray tooltip, and Configure's sync status stay as they are. The log adds detail; it replaces no message. Today the "can't be read" notice counts the files [Sync](../../src/Shared/Sync/Service/SyncEngine.cs) found, and only Configure's status lists them, in [SyncText](../../src/Desktop/Configure/SyncText.cs); with the log, each file and its reason also sit just before the entry for the notice's sync failure.

### The desktop app keeps two weeks of logs, at most 30 MB

The composition root writes the log files through [Serilog.Sinks.File](https://github.com/serilog/serilog-sinks-file), beside the app's other files on this PC, so the user never manages them:

- **The folder is `logs` in the app's folder under `%LocalAppData%`**, beside the draft and the browser data that [Program.cs](../../src/Desktop/App/Program.cs) places there. It isn't in the clone, so logs never sync. It's apart from Velopack's install folder, so updating or uninstalling leaves it, as it leaves the [device folder](sync.md#configure-keeps-the-selected-clone-or-makes-a-new-one).
- **A new file starts each day, and when a file reaches 1 MB.** The file's name holds its local date and, after the first, a number, such as `memoria-20261119.log` and `memoria-20261119_001.log`. Serilog checks the size before each entry, so the entry that crosses 1 MB still goes into the file whole; the limit is approximate, and an ordinary entry passes it by a few KB. The user decided this.
- **Files are kept while both limits hold: at most 30 files, and none from a day more than 14 days ago.** So the folder holds at most 30 files of about 1 MB, about 30 MB, and two weeks fit when a day writes at most 2 MB. By our estimate, not yet measured, a busy day writes about a megabyte. Serilog judges a file's age by the date in its name, and deletes old files only when it starts a new one: at start, each day, and at each 1 MB. The file being written is never deleted. Debug entries count toward both limits, as the user decided, so a day that writes more than about 2 MB leaves fewer than 14 days in the folder. The third rule's one entry per choice per operation keeps Debug's share small; not measured.
- **Each line is one entry, as one JSON object, in Serilog's compact format** ([CompactJsonFormatter](https://github.com/serilog/serilog-formatting-compact)): the UTC time, the level, the message template and its named values, the component, and the exception's full text with its stack trace, when there is one. JSON keeps a message or a stack trace that spans lines inside one entry, and the file still opens in any text editor. The line holds the template and its values, not the rendered text as well, so the files hold more entries.
- **The format has one home: the composition root.** It names the folder, the file names, and the formatter once, and its log source reads the same files back with Serilog's own reader for that format ([LogEventReader](https://github.com/serilog/serilog-formatting-compact-reader)), for the [Logs window](desktop/logs.md#public-interface).
- **A second instance writes its own file.** A later start [hands over to the running one and ends](desktop/shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view), but it logs its start first. The running instance holds today's file open, so Serilog's file sink moves the second one to the next numbered file, as it does for any file another program holds. So two processes never write one file, and Serilog's `shared` option, which lets them, isn't used. Found in the sink's source, not confirmed by a run.

The user decided on 1 MB files, 30 files, and 14 days.

Proposed, a decision to confirm: the install and uninstall steps keep writing to [Velopack's log](desktop/configure.md#start-with-windows-is-the-run-key-itself), not this one. They run for a moment with no window, and uninstalling must not leave a new file behind.

### A slow disk never holds up the app

The file sink sits inside [Serilog.Sinks.Async](https://github.com/serilog/serilog-sinks-async), as the user decided, so one background thread does all the writing:

- **A module's call only puts the entry in a queue in memory**, and returns. The background thread writes queued entries in the order they came, and each written entry reaches the disk at once, since the file sink's buffered mode is off.
- **The queue holds at most 10,000 entries.** When the writer falls that far behind, new entries are dropped and counted, never waited for, since a log must not stop the app. That's Serilog.Sinks.Async's default.
- **When the app ends, the writer gets at most 2 seconds to write what's waiting.** The composition root disposes the logger once, at the end of `Main` or in .NET's [ProcessExit](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.processexit), which `Environment.Exit` raises too, so it also runs when the app ends after an error. Disposing waits for the queue to empty with no limit of its own, so the composition root stops waiting after 2 seconds; entries still waiting are lost.
  The bound uses the [pipeline's clock](../../src/Desktop/App/Logging/LogPipeline.cs): the system clock in the app, and a fake clock in its tests.
  A timed wait also enforces the real bound if thread-pool starvation prevents the clock's callback from running.

Proposed: the 10,000-entry queue and the 2-second wait. Each a decision to confirm.

### A log that can't be written never stops the app

Logging serves the user's debugging, not the user's tasks, so its own failures are kept out of the way. This is a recorded exception to [fail fast](../../AGENTS.md#code-design): the failure isn't swallowed, it's counted and reported in the log and the Logs window.

- **The composition root listens to the writer's failures**, through Serilog's failure listener, which Serilog 4.1 added ([`WriteTo.Fallible`](https://nblumhardt.com/2024/10/fallback-logging/)). Both the queue and the file sink report to it: each dropped entry, each file that couldn't be opened or written, and each old file that couldn't be deleted. An integration test confirms that Serilog.Sinks.File 7.0.0 reports a file it couldn't delete to the listener, not only to Serilog's internal [SelfLog](https://github.com/serilog/serilog/wiki/Debugging-and-Diagnostics).
- **When the file sink can't open a file, such as on a full disk or a folder it may not use, it drops entries until it tries again**, about 30 minutes later, or at the next day's file. Found in the sink's source, not confirmed for 7.0.0.
- **When writing works again, the Warning comes right after the first entry that writes,** saying how many entries were lost, since when, and why. A write can't know it's the first after a failure until it succeeds. Entries dropped because the queue was full are counted the same way. The user decided this.
- **While writing fails, the [Logs window](desktop/logs.md#a-status-line-says-what-the-list-holds) says so**, with the reason, so a user who opens it isn't left wondering why the newest entries are missing. An old file that can't be deleted is only logged.
- **No message box and no notification**: an error about the log would interrupt the user for something only debugging needs.

### Passwords and keys never reach the log

No PII rule applies, since the log stays on this PC, but secrets never enter it:

- **The app has keys, not passwords.** Its secrets are the device's private SSH key, the random passphrase that encrypts it, and the encrypted [Windows key file](sync.md#the-windows-key-file-is-encrypted-and-only-the-users-account-unlocks-it) that holds both, and the signatures the key makes. No module ever logs one, not even in part. A key is named by its fingerprint, which is public. The public key, host keys' fingerprints, and the repository's SSH address aren't secrets.
- **Exception texts aren't ours to shape**, such as a git error that quotes an address. So, as a safety net, the composition root wraps the compact formatter in a redacting formatter of its own. After the formatter writes an entry's line, and before the line reaches the file, it replaces any private key block (`-----BEGIN … PRIVATE KEY-----` to its end line, or to the end of its text when the end line is missing) and the whole user part of an address such as `ssh://user:password@host` or `https://token@host` with `***`, since a token can stand alone where a user name goes. An address without a scheme, such as `git@github.com:owner/repo.git`, keeps its user name. One place covers the message, every value, and the exception's text alike. A replacement never crosses a JSON string's end, so the line stays valid JSON. The user decided this.
- **Sync's tests check that no module logs a secret**: a test runs each key operation with fake key material, then checks that no entry's message, value, or exception text contains it. The composition root's unit tests check the redacting formatter on its own.

### Errors no module handles are logged before they're shown

The [desktop platform](desktop/README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) catches errors no module handles, and decides what the app does next: after start, an error on the UI thread no longer ends the app, as the user decided. It logs each error first, with where it happened. Before shutdown it also shows the error; once shutdown has begun, no error dialog opens:

- **On the UI thread**, through [DispatcherUnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception): at Error when the app keeps running, and at Fatal while it starts or stops, when the app ends. An app that kept running after an error later ends with exit code 0, as any normal exit does. The user decided this.
- **On any other thread**, through [UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception), at Fatal. .NET ends the process after this event, whatever the app does, so the [2-second wait](#a-slow-disk-never-holds-up-the-app) gives the entry its chance to reach the disk.
- **In a task nobody waited for**, through [UnobservedTaskException](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception), at Error. .NET raises it only when the failed task is cleaned up from memory, so its entry can come late. The platform logs it and marks it observed, which changes nothing else: such an error doesn't end the app today either.

### Tests read entries from one in-memory sink

Tests see what a module logs through one hand-written sink, `InMemoryLogSink`, in `Memoria.ToDo.Shared.Fakes` with the other [shared fakes](technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test). It implements Serilog's `ILogEventSink`, keeps every entry in memory, and may be written from any thread. It also makes a logger that writes only to it, at every level, so a test passes that logger to the module and then reads the entries back: level, component, rendered message, values, and exception. It needs no package beyond Serilog. A test that doesn't check logging passes Serilog's silent logger, `Logger.None`. Proposed, a decision to confirm. [Serilog.Sinks.InMemory](https://www.nuget.org/packages/Serilog.Sinks.InMemory), a package that does the same, would be a new dependency for one small class.

### The Android app passes a logger too

The shared modules take Serilog's `ILogger` on Android as on the desktop, so the [Android app](android/README.md)'s composition root makes a logger when it's built, names each module's as the desktop's does, and passes it. Serilog's packages target .NET Standard 2.0 and .NET 6, which .NET 11 for Android can use; not confirmed by a build. Where Android keeps its entries, and whether it shows them, are for the Android design. Proposed, a decision to confirm: until then the Android design adds no sink, and nothing is built for Android now.

### Five Serilog packages do the work

The user picked Serilog with its file sink, its compact JSON format and reader, and its background writer. [Directory.Packages.props](../../Directory.Packages.props) pins each version, as the [technology stack](technology-stack.md#three-files-pin-every-version) says; the versions below are the newest stable ones on nuget.org when this design was checked. No package lists .NET 11 itself; each targets older .NET and .NET Standard 2.0, which .NET 11 can use, not confirmed by a build. Licenses were read in each project's repository, not in the package's own details.

| Package | Version | Used by | License | Targets |
| --- | --- | --- | --- | --- |
| [Serilog](https://www.nuget.org/packages/Serilog/4.4.0) | 4.4.0 | Every module, both platforms, both composition roots, the shared fakes | Apache-2.0 | .NET 6, 8, 9, and 10; .NET Standard 2.0; .NET Framework 4.6.2 and 4.7.1 |
| [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0) | 7.0.0 | The desktop composition root | Apache-2.0 | .NET 6; .NET Standard 2.0; .NET Framework 4.6.2 |
| [Serilog.Sinks.Async](https://www.nuget.org/packages/Serilog.Sinks.Async/2.1.0) | 2.1.0 | The desktop composition root | Apache-2.0 | .NET 6; .NET Standard 2.0; .NET Framework 4.6.2 |
| [Serilog.Formatting.Compact](https://www.nuget.org/packages/Serilog.Formatting.Compact/3.0.0) | 3.0.0 | The desktop composition root | Apache-2.0 | .NET 6; .NET Standard 2.0; .NET Framework 4.6.2 |
| [Serilog.Formatting.Compact.Reader](https://www.nuget.org/packages/Serilog.Formatting.Compact.Reader/4.0.0) | 4.0.0 | The desktop composition root | Apache-2.0 | .NET 6 and 8; .NET Standard 2.0; .NET Framework 4.6.2 and 4.7.1 |

- **The reader brings one more package, [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)**, 13.0.3 or later, under the MIT license; its newest stable version is 13.0.4. It comes in as the reader's own dependency, at the version the reader asks for, and [Directory.Packages.props](../../Directory.Packages.props) doesn't name it, as it doesn't name LibGit2Sharp's native libraries. The user decided this.
- **Only the desktop composition root references the four packages that write and read files.** Proposed, a decision to confirm: an [architecture test](technology-stack.md#architecture-tests-check-the-module-map) checks it, as one already checks that only the composition root references Velopack and the tray icon's package.
- Not checked: the lowest Serilog version each of the four needs.

## Public interface

No new type: modules use Serilog's `ILogger`. Each create method below takes the module's logger as its last argument, `ILogger logger`, from the `Serilog` namespace, and throws ArgumentNullException when it's null. Only the argument is new; each keeps its other arguments and promises:

- The shared platform's [synced repository](modules.md#the-synced-repository-gives-every-store-one-clone-one-lock-and-one-reload): `new SyncedRepository(ILogger logger)`, which today takes nothing.
- [Tasks](tasks.md): `TaskStore.Create(..., IRepositoryAccess repository, ILogger logger)`.
- [Contacts](contacts.md): `ContactStore.Create(..., IRepositoryAccess repository, ILogger logger)`.
- [Sync](sync.md): `SyncService.Create(..., TimeSpan? periodicInterval, ILogger logger)`.
- The desktop platform's [Run the app](../../src/Desktop/Platform/AppRunner.cs), with the shutdown policy in the [desktop design](desktop/README.md#the-platform-runs-the-app-and-the-composition-root-builds-it).
- The desktop platform's [store failures](desktop/README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them): `StoreFailures.Run(Action call, Func<Exception, string?> messageOf, Action<string> report, ILogger logger)`, which logs the failure at Error when it reports a message.
- The desktop platform's [window parts](desktop/README.md#windows-open-on-the-screen-with-the-mouse-pointer): `WindowPlacement.ShowOnPointerScreen(Window window, ILogger logger)`, `ModalDialog.Show(Window dialog, ILogger logger)`, `new SingleWindow(Func<Window> createWindow, ILogger logger)`, and `QuestionDialog.Ask<T>(..., T cancelAnswer, ILogger logger)`. The caller passes its own logger, as the user decided, so each Debug entry they write names the module whose window it is.
- The desktop platform's [hover card](desktop/README.md#a-hover-card-shows-while-the-pointer-rests-on-a-part) and the [Formatted view's constructors](desktop/formatted-view.md#public-interface) also take the caller's logger, under the same rule as the window parts.
- [Shell](desktop/shell.md#public-interface): `TrayApp.Create`, which also takes the [Logs window](desktop/logs.md#public-interface).
- [Task editor](desktop/task-editor.md#public-interface): `AddWindow.Create`.
- [Task list](desktop/task-list.md#public-interface): `ViewTasksWindow.Create`.
- [Contact list](desktop/contact-list.md#public-interface): `ContactsWindow.Create`.
- [Configure](desktop/configure.md#public-interface): `ConfigureWindow.Create`.
- [Updates](desktop/updates.md#public-interface): `AppUpdates.Create`.
- [Logs](desktop/logs.md#public-interface): `LogsWindow.Create`, a new module.
- [MCP server](desktop/mcp-server.md#public-interface): `McpServer.Create`, a new module.

The shared fakes add the in-memory sink:

```csharp
namespace Memoria.ToDo.Shared.Fakes;

/// Keeps every log entry written to it, in order, for tests to read. Safe to write from any thread.
public sealed class InMemoryLogSink : Serilog.Core.ILogEventSink
{
    /// A logger that writes only to this sink, at every level.
    public Serilog.ILogger CreateLogger();

    /// A copy of the entries written so far, oldest first.
    public IReadOnlyList<Serilog.Events.LogEvent> Events { get; }

    public void Emit(Serilog.Events.LogEvent logEvent);
}
```

## Failure modes

- **The log folder can't be created, or a file can't be opened or written**: the entries are dropped and counted, the next entry that's written says how many were lost and why, and the Logs window shows the problem. The app keeps working.
- **The writer falls 10,000 entries behind**: new entries are dropped and counted the same way.
- **An old file can't be deleted**, such as one another program holds open: it's kept, the failure is logged, and the next clean-up tries again. The folder may pass 30 MB until then.
- **The app ends before the writer catches up**: it gets 2 seconds; entries still waiting after that are lost. A crash that .NET ends at once, such as a stack overflow, loses what's waiting.
- **A file is cut off mid-line by a crash or a full disk**: the [Logs window](desktop/logs.md#failure-modes) skips the broken line and counts it.
- **A secret reaches a message anyway**, in a shape the safety net doesn't know: it's on this PC only, in the user's own folder, for at most 14 days.
- **The logger's settings are wrong**, such as a missing folder name: making the logger throws at start, before the app does anything else, as [fail fast](../../AGENTS.md#code-design) asks.
- **A change logs a Debug entry per item**, against the [third rule](#every-operation-failure-and-decision-is-logged-where-it-happens): the files fill faster, and the folder keeps fewer days, down to the newest 30 MB. The reviewer flags it; nothing at run time limits it.

## Ruled out

- Which library writes the log, decided by the user:
  - **Our own small log port and file writer, with no new package**: we'd write and test rolling, clean-up, and the background writer ourselves, which Serilog already does.
  - **[Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)'s `ILogger`, with our own file writer**: Microsoft ships no file writer for it, so we'd write that part anyway.
- How modules reach the log:
  - **A small log port of ours in the shared platform, with Serilog only in the composition root**, decided by the user: one more interface to keep in step with what Serilog already offers every module.
  - **Serilog's static `Log.Logger`**, decided by the user: no create method would change, but every test would share one logger, tests running at once would see each other's entries, and the composition root would no longer choose the implementation, as [AGENTS.md](../../AGENTS.md#code-design) requires.
  - **Wrappers in the composition root around each module's interface**, logging each call: no module would change, but the wrappers can't see what happens inside, such as a merge, an automatic deletion, or the exception behind a failed sync. Every interface would need a wrapper kept in step with it.
  - **Logging every exception when it's thrown**, through .NET's [FirstChanceException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.firstchanceexception): it logs failures that are handled as normal, such as a missing file, and logs one failure many times.
- How much each change logs, decided by the user:
  - **Operations and failures only**, the first two rules with no Debug level: a wrong result that throws nothing would leave no trace.
  - **Debug entries written only when a new Configure setting turns them on**: a problem would have to happen again with the setting on before it could be traced, and it needs a new setting.
- **Verbose, for detail finer than Debug**: it would invite an entry per item, which would crowd two weeks out of 30 MB.
- The file format, decided by the user:
  - **Plain text lines through an output template, read back by a parser of ours**: a stack trace spans lines, so the parser would have to guess where an entry ends.
  - **Compact JSON read back by a reader of ours**: Serilog's reader already reads the format its formatter writes, so the format keeps one home.
  - **The rendered compact format** (`RenderedCompactJsonFormatter`): it writes each message's text beside its values, so files fill faster, and Serilog's reader reads either.
- How entries reach the disk:
  - **The file sink alone, in its buffered mode**, decided by the user: buffering spares the disk, but a write still runs on the caller's thread, so a slow disk can slow the UI.
  - **A queue that waits when it's full**, Serilog.Sinks.Async's `blockWhenFull`: a stuck disk would stop every thread that logs.
  - **Serilog's `shared` option**, so two instances write one file: the [one-instance rule](desktop/shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view) already keeps the second one's life short, and its few entries can sit in a file of their own.
- **Retention by total size**: Serilog's file sink has none, only a file count and an age ([its README](https://github.com/serilog/serilog-sinks-file#rolling-policies)), so the 30 MB cap is 30 files of 1 MB.
- **Redacting each value as it's logged**, through a Serilog enricher or capture policy: it can't reach an exception's text, which is where a stranger's secret is most likely, so the formatter is the one place that sees everything.
- How Newtonsoft.Json comes in, decided by the user:
  - **Pinning it in Directory.Packages.props at 13.0.4**, so a version change shows in review: one more line to keep in step, for a package no project of ours names.
  - **Reading the files with .NET's own System.Text.Json instead**: no new package, but it brings back the reader of ours the user ruled out.
- Whether tests check Debug entries, decided by the user:
  - **Yes, the same way as other entries: level, component, and values**: a refactor couldn't quietly drop an entry, but every choice would add tests, for little benefit.
  - **Only that each choice writes a Debug entry, not its values**: the same number of added tests, for even less.
- How the platform's static window parts log, decided by the user:
  - **One window shower, made by the composition root with the `Platform` logger and passed to each window module**: entries would log as `Platform`, but six create methods would change, and a static part would become an object to wire.
  - **Leaving them as they are, with a backlog row to make them log later**: until that row was built, a window that never appears would leave no trace.
