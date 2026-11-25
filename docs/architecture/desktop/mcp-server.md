# MCP server: how an AI assistant uses the desktop app

Status: Decided

The MCP server lets an AI assistant on this PC, such as GitHub Copilot in VS Code, Claude, or Codex, do what the user does in the desktop app: read, add, edit, order, resolve, and delete tasks; manage contacts; sync; and change a few settings. It speaks the [Model Context Protocol](https://modelcontextprotocol.io/specification/2026-07-28) (MCP), the open protocol AI tools use to call an app's operations. It runs the whole time the app runs, as the user asked, unless the user turns it off in Configure.

## Behavior

### It runs while the app runs, through the services the windows use

The user decided that the MCP server is a desktop module of its own, `Memoria.ToDo.Desktop.MCPServer`, which [Shell](shell.md#public-interface) starts and stops, as it does [Updates](updates.md).

- **It starts once the app holds its [one-instance claim](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view) and the clone has loaded**, when [Shell starts it](shell.md#the-mcp-server-starts-once-the-clone-has-loaded), so no call sees the task list before it's read.
- **It stops before Sync in [Shell's shutdown](shell.md#shutdown-joins-owned-work-without-blocking-the-dispatcher)**, so no call changes the clone while Sync stops.
- **It uses the same Tasks, Contacts, Sync, and Updates objects as the windows.** A change an assistant makes raises the same change notices a sync does, so an open Task View or Contacts window shows it at once. The windows already update on notices from other threads, since Sync's changes arrive that way ([Task View](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button)).
- **An assistant's change syncs like the user's.** Tasks' and Contacts' saves already start [sync soon after a change](../sync.md#on-the-desktop-sync-runs-soon-after-a-change-and-every-30-minutes).
- **It never opens a window or moves the user's screen.**
- **Calls to Updates and to Start with Windows run on the UI thread**, where those parts expect their callers. Tasks, Contacts, Sync, and the log source are safe to call from any thread, so the server calls them from the call's own thread.
- **Several assistants can be connected at once**, each through its own connection, with no limit. Tasks and Contacts already run one call at a time.

### An assistant reaches it through a pipe and a bridge the app provides

The user decided on a Windows named pipe in the app, and a small bridge the assistant starts. Every MCP assistant can start a program and talk to it over its standard input and output, which MCP calls stdio ([transports](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports)). The tray app can't be that program: it starts at sign-in and runs once, so no assistant owns it.

- **The app listens on a named pipe while the server runs.** Each message is one line of JSON, as in stdio, which the spec recommends for other transports ([custom transports](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports#custom-transports)).
- **The bridge is the app's own program started with `--mcp`**, such as `Memoria.ToDo.Desktop.App.exe --mcp`: a second start mode of the [composition root](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it). It copies its standard input to the pipe and the pipe to its standard output, unchanged. It never claims the [one instance](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view), starts WPF, or shows a window. It ends when the assistant closes its input, or when the app closes the pipe.
- **The bridge runs the app's program in Velopack's `current` folder, not Velopack's starter**, the program the [`Run` key](configure.md#start-with-windows-is-the-run-key-itself) names. The starter starts the app and ends at once, without passing on input or output ([stub.rs](https://github.com/velopack/velopack/blob/develop/src/bins/src/stub.rs)). The `current` folder's path stays the same across updates ([Velopack on Windows](https://docs.velopack.io/packaging/operating-systems/windows)). A copy run from a build folder names its own program.
- **An update ends every running bridge**, since Velopack's updater ends each program running from the install folder. The assistant starts the bridge again when it next needs it; this is an accepted risk.
- **The composition root names the pipe and gives the name to both ends**, so it has one home. The user decided that the name joins the [app's name](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), the signed-in user's security ID, and the Windows session's number, so two sign-ins never share a pipe, as one instance runs per sign-in session.
- **When there's no pipe, the bridge starts the app and waits.** The user decided that the bridge starts the app as a start at sign-in does, with only the tray icon, so an assistant works after the user quit the app. It starts Velopack's starter with the [sign-in argument](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view), or its own program in a build-folder copy. It starts the app outside any job object the assistant puts the bridge in, where Windows allows it, so the app outlives the bridge. Then it waits for the pipe. The user decided that it waits at most 30 seconds, then fails.
- **The bridge tells a running app from a stopped one by the [one-instance signal](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view)**, which it reads without claiming. When the app runs but there's no pipe, as while the server starts or after the user turned it off, it starts nothing, so a start never shows Task View. It waits for the pipe the same 30 seconds, then fails with a message that says to turn on AI assistant access in Configure, or to read the reason there. The user decided this check.
- **The bridge writes its own failures to its standard error**, which assistants show in their MCP logs, and not to the app's [log files](../logging.md#the-desktop-app-keeps-two-weeks-of-logs-at-most-30-mb), which the running app holds. The app logs each connection from its side. The user decided this. Each message includes the cause, and the bridge ends with code 1 on every failure. The composition root's [decided rules](README.md#the-composition-root-names-the-mcp-servers-pipe-and-runs-its-bridge) list the rest.

### Only the signed-in user's programs on this PC can reach it

The user decided that the pipe's access list does the job a token would, and that there's no token:

- **The pipe's access list allows only the signed-in user, and denies the Network logon group**, so a program on another computer can't connect, even with the user's password. Windows refuses any other caller before the app sees it.
- **No token.** Any program this user runs can already read and write the clone's files directly, so a token would add a secret to manage without closing a door.
- **The bridge connects only to a pipe the same user made**, through .NET's [`PipeOptions.CurrentUserOnly`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions), so another user's program that took the pipe's name first can't pose as the app. The app makes its pipe as the first instance of the name, so it never joins such a pipe either.
- **No web page can reach it.** There's no port, so a browser page has nothing to send a request to.
- **An assistant run as administrator can't connect** to an app that isn't, since `CurrentUserOnly` also compares the elevation. The bridge says so. This is an accepted risk; the app runs without administrator rights.

### Each tool is one operation the user can do

The user decided on one MCP tool per operation, reads included, and no MCP resources, since every assistant supports tools and support for resources varies.

- **Each tool calls one public operation of a module, or a few to read, and adds no rules of its own**, so the rules keep their one home in that module. Its result is JSON. The user decided that tool names are a verb and a noun in lowercase with underscores, such as `add_task` and `resolve_task`.
- **Errors come back as the module words them.** A tool fails before any change on the same invalid input the module rejects, and returns its message as a tool error, never as a success.
- **Each tool says what kind it is**, through MCP's [tool annotations](https://modelcontextprotocol.io/specification/2026-07-28/server/tools): reads are read-only. Deleting and changing the setting every device shares are destructive, so an assistant that supports it asks the user first. Every other change is marked not destructive, since MCP otherwise counts a change as destructive.
- **The user decided which operations get a tool**: everything the user can do except security setup, Update now, and shortcuts. There's no read-only switch: an assistant's own approval of each tool does that job.

The table lists every operation a user can do in the desktop app, the public operation behind it, and its tool. **Read** and **Change** are tools; **Excluded** means no tool, with the reason.

| Area | Operation | Public operation | Tool |
| --- | --- | --- | --- |
| Tasks | List every task, open and resolved, in rank order, without descriptions | [`ITasks.All`](../../../src/Shared/Tasks/ITasks.cs#L9) | Read |
| Tasks | Read one task, with its description | [`ITasks.All`](../../../src/Shared/Tasks/ITasks.cs#L9) | Read; an unknown ID gets a not-found error |
| Tasks | Search by words, optionally within some tags | [`ITasks.Search`](../../../src/Shared/Tasks/ITasks.cs#L34), [with tags](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it) | Read |
| Tasks | See the tags in use, with counts, colors, and the user's order | [`ITasks.TagsInUse`](../../../src/Shared/Tasks/ITasks.cs#L22), [`ITagColors.ColorOf`](../../../src/Shared/Tasks/ITagColors.cs#L13), [`ITagOrder.Sort`](../../../src/Shared/Tasks/ITagOrder.cs#L15) | Read |
| Tasks | Add a task with a title, a description, and tags | [`IDirectTaskChanges.Add`](../../../src/Shared/Tasks/IDirectTaskChanges.cs), [below](#adding-and-editing-never-touch-the-users-open-work) | Change |
| Tasks | Edit a task's title, description, or tags | [`IDirectTaskChanges.Update`](../../../src/Shared/Tasks/IDirectTaskChanges.cs), [below](#adding-and-editing-never-touch-the-users-open-work) | Change |
| Tasks | Add an image to a task | [`IDirectTaskChanges.AddImage`](../../../src/Shared/Tasks/IDirectTaskChanges.cs), [below](#adding-and-editing-never-touch-the-users-open-work) | Change: appends the image to the description and returns the task |
| Tasks | Link to another task | [`ITasks.LinkAddress`](../../../src/Shared/Tasks/ITasks.cs#L40) | Read: returns the address to put in a description |
| Tasks | Move a task between two others | [`ITasks.Move`](../../../src/Shared/Tasks/ITasks.cs#L87) | Change |
| Tasks | Resolve, or reopen | [`ITasks.Resolve`](../../../src/Shared/Tasks/ITasks.cs#L90), [`Reopen`](../../../src/Shared/Tasks/ITasks.cs#L93) | Change |
| Tasks | Delete a task | [`ITasks.Delete`](../../../src/Shared/Tasks/ITasks.cs#L100) | Change, destructive, as [below](#deleting-acts-at-once-and-resolving-is-the-undoable-path) |
| Tasks | Keep, load, or discard Add's draft | [`StartDraft`, `LoadDraft`, `KeepDraft`, `DiscardDraft`](../../../src/Shared/Tasks/ITasks.cs#L46) | Excluded: the draft is the Add window's own text |
| Tasks | Paste an image into Add or an open edit | [`ITasks.SaveImage`](../../../src/Shared/Tasks/ITasks.cs#L63) | Excluded: those images wait with the user's open work; assistants add images through `IDirectTaskChanges.AddImage` |
| Tags | Change a tag's color | [`ITagColors.SetColor`](../../../src/Shared/Tasks/ITagColors.cs#L22) | Change |
| Tags | Move a tag group among the groups | [`ITagOrder.Move`](../../../src/Shared/Tasks/ITagOrder.cs#L26) | Change |
| Settings | Read and change the days before a resolved task is deleted | [`ISharedTaskSettings.DeletionDelayDays`](../../../src/Shared/Tasks/ISharedTaskSettings.cs#L11) | Read; Change, destructive, since it changes every device |
| Settings | Change the global, Close window, and Save shortcuts | [`ISharedTaskSettings`' shortcut setters](../../../src/Shared/Tasks/ISharedTaskSettings.cs#L47) | Excluded: Configure's [shortcut check](configure.md#a-new-shortcut-is-checked-at-once) isn't a public part a second caller can use, and a bad shortcut breaks the user's keys on every device |
| Settings | Read and switch Start with Windows | [`IStartWithWindows`](../../../src/Desktop/Platform/IStartWithWindows.cs#L4), which the [desktop platform](README.md#start-with-windows-is-a-port-the-platform-publishes) publishes | Read; Change |
| Contacts | List groups and contacts, the user's own contact, and the files that couldn't be read | [`IContacts.Groups`](../../../src/Shared/Contacts/IContacts.cs#L12), [`Me`](../../../src/Shared/Contacts/IContacts.cs#L15), [`SkippedFiles`](../../../src/Shared/Contacts/IContacts.cs#L101) | Read |
| Contacts | Find one; search | [`IContacts.Find`](../../../src/Shared/Contacts/IContacts.cs#L27), [`Search`](../../../src/Shared/Contacts/IContacts.cs#L36) | Read |
| Contacts | See a contact's manager and direct reports | [`ManagerOf`](../../../src/Shared/Contacts/IContacts.cs#L30), [`DirectReportsOf`](../../../src/Shared/Contacts/IContacts.cs#L33) | Read |
| Contacts | Mention a contact in a task | [`IContacts.MentionOf`](../../../src/Shared/Contacts/IContacts.cs#L62), [`MentionTemplates`](../../../src/Shared/Contacts/IContacts.cs#L21) | Read: returns the mention to put in a description |
| Contacts | Add or edit a contact | [`IContacts.Save`](../../../src/Shared/Contacts/IContacts.cs#L82), with a [change](../contacts.md#saving-adding-and-deleting-change-one-group-file) | Change, as [below](#each-tool-is-one-operation-the-user-can-do) |
| Contacts | Delete a contact | [`IContacts.Delete`](../../../src/Shared/Contacts/IContacts.cs#L89) | Change, destructive |
| Contacts | Move a group among the groups | [`IContacts.MoveGroup`](../../../src/Shared/Contacts/IContacts.cs#L98) | Change |
| Sync | See sync status, the last successful sync, the skipped files, and the device name | [`ISyncService.Status`](../../../src/Shared/Sync/ISyncService.cs#L6), [`Setup`](../../../src/Shared/Sync/ISyncService.cs#L7) | Read |
| Sync | Sync now | [`ISyncService.SyncNowAsync`](../../../src/Shared/Sync/ISyncService.cs#L27) | Change |
| Sync | Set the device name | [`ISyncSetup.SetDeviceNameAsync`](../../../src/Shared/Sync/Setup/ISyncSetup.cs#L21) | Change |
| Sync | Make or forget the device's SSH key; pick or clone the repository; trust a server's host key | [`ISyncSetup`](../../../src/Shared/Sync/Setup/ISyncSetup.cs) | Excluded: an instruction hidden in text the assistant reads could send every task to a stranger's repository, or trust a forged server |
| Updates | See the version and whether an update waits; turn Auto update on or off | [`IUpdateStatus.State`, `SetAutoUpdate`](../../../src/Desktop/Updates/IUpdateStatus.cs#L9) | Read; Change |
| Updates | Update now | [`IUpdateStatus.UpdateNow`](../../../src/Desktop/Updates/IUpdateStatus.cs#L24) | Excluded: it restarts the app, which ends the server in the middle of the call |
| Logs | Read the log, filtered as in the Logs window | [`ILogSource.ReadAsync`](../../../src/Desktop/Logs/ILogSource.cs#L10), [`LogFilter`](logs.md#one-filter-decides-which-entries-match) | Read, as [below](#reading-the-log-filters-as-the-logs-window-does) |
| App | Open or close a window, the tray menu, the global shortcuts, What is new, Exit | Shell, window modules | Excluded: they move the user's screen, not the data; Exit would end the server |
| Editing | Bold, lists, headings, and the other formatting keys | [Editing](../editing.md) | Excluded: an assistant writes the Markdown itself |

The server never reads or changes the AI assistant access switch through a tool: only the user turns it off and on.

The contact tools follow these rules:

- **Listing contacts also returns the data types**, such as pronouns and career paths, with their keys, so an assistant picks the values the contact form offers.
- **Saving an existing contact changes only the fields given**, as editing a task does. A field left out keeps its latest value, and an empty value clears it. A new contact gets only the fields given. Proposed, each a decision to confirm, from the independent review's findings; they change Contacts' public interface, so they get a [re-review](../../contributing/process.md#independent-review):
  - **The tool applies the given fields through Contacts' [save through a change](../contacts.md#saving-adding-and-deleting-change-one-group-file)**, which reads the contact and saves it under one lock. So a sync's reload or a window's save between the read and the write isn't undone for the fields left out.
  - **The full name is optional too.** Left out on an existing contact, it keeps its value. Contacts' own rules refuse a new contact without a full name, and an empty full name, as they refuse any invalid contact.
- **A mention uses the first mention template when the call names none**, as the editor's mention popup starts on it. That's the template the user [chose last](../contacts.md#mention-templates-come-from-a-synced-file) in an editor; the server never moves a template.

### Adding and editing never touch the user's open work

The user decided that an assistant's new task, edit, and image go straight to the task list, never through the Add window's draft or an open edit. The user decided that all three go through Tasks' [direct task changes](../tasks.md#direct-task-changes-are-a-capability-of-their-own), which the [local API](../local-api.md) uses too, so changing a task without an editor has one home.

- **Adding a task** takes a title, and optionally a description and a list of tags, and calls `IDirectTaskChanges.Add`. The draft the user left in Add stays as it is. Tasks [normalizes](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) each tag, as for the Add window, and keeps them in the order given. It returns the new task.
- **Editing a task takes only the fields to change**: its ID, and any of title, description, and tags. The user decided that the server reads the task as it is now and calls `IDirectTaskChanges.Update` with only those fields and that task's `Updated` time, so the assistant never sends a time. A field left out stays as it is. It returns the task as saved, with its description; when nothing would change, Tasks writes nothing and it returns the task as it is.
- **The tags input sets the task's whole tag list, in order.** A task has [several tags in order](../tasks.md#a-tasks-tags-are-an-ordered-list-and-the-first-one-groups-it), and the first one groups it in Task View. The user decided these rules:
  - **`add_task`, `edit_task`, and `search_tasks` take `tags`, a list of strings, in place of `tag`.** One input per tool and one rule, which the model uses most reliably. An assistant reads the tools' inputs anew each session, so nothing written against the old input breaks.
  - **In `edit_task`, `tags` left out keeps every tag, and an empty list removes them all.** Any other list replaces the whole list, so moving a tag first is an edit that sends the list in its new order.
  - **Every tag is [normalized](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case), and a repeated tag is dropped, keeping its first place, with no error.** Tasks does both, in every tool, so `My Work` is saved and matched as `my-work`.
  - **In `search_tasks`, `tags` keeps the tasks that have every tag named**, each anywhere in the task's list, as [Tasks' filter](../tasks.md#tags-are-lowercase-with-hyphens-and-search-ignores-case) does. The words still match any of a task's tags. The user decided that `tags` left out or an empty list means no tag filter, so the tool searches by its words alone.
  - **The tool descriptions say that the first tag groups the task**, so an assistant that wants to regroup a task puts the new tag first.
  - **Every task the tools return already lists its tags in the task's order**, so the output doesn't change.
- **A list of tags the task can't take fails the call** with Tasks' message, before anything is written. Decided by the session, each a decision to confirm: a tag that's empty once normalized, and, in `add_task` and `edit_task`, more than 15 tags after normalizing and dropping repeats. The tool descriptions name the limit.
- **The edit never touches the user's open edit.** The images the user pasted into an open edit of the task keep waiting with it, unsaved, until the user saves or discards. The user's later save still [merges field by field](../tasks.md#two-versions-of-a-task-merge-field-by-field): it replaces only the fields the user changed. So an assistant's new title and the user's new description both survive; when both changed the same field, the user's later save wins.
- **A change that lands between the server's read and its save fails the edit.** Tasks refuses it as stale before writing, and the tool returns an error that says the task changed meanwhile, so the assistant reads it again and retries. That window is a few milliseconds, such as when a sync or the user's save lands in it. Proposed, a decision to confirm: the server never retries on its own, since a retry would write the assistant's fields over a change the assistant hasn't seen.
- **Adding an image** takes a task's ID, the image's bytes, and its alt text, and calls `IDirectTaskChanges.AddImage`. Tasks shrinks the image by its [image rules](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task), so it accepts any format the PC's image tools read, and keeps it at most 1 MB. It writes the image into the task's folder in the clone and appends its Markdown on a new line at the end of the description, in one save that sets the task's `Updated`. When that save fails, Tasks deletes the image again, so nothing changes.
- **The image tool returns the task as saved**, with its description, as the add tool does. The user decided this, so the image and its link arrive together, and no image is left in the clone, synced but unlinked, when an assistant never makes the edit. An assistant that wants the image elsewhere moves its Markdown with the edit tool.
- **An image the user is editing around**: an assistant's image changes the task's file, so it meets an open edit as any change does. When the user then saves a changed description in the Task list, the user's text wins and the image's link is dropped; the image stays in the task's folder until the task is deleted, by Tasks' rule that [an image is deleted only with its task](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task). An edit that leaves the description as it was keeps the link.
- **An image's bytes over 20 MB are refused** before Tasks sees them, so one call can't hold the app's memory. The user decided on 20 MB, which keeps any screenshot or phone photo.

### Deleting acts at once, and resolving is the undoable path

The user decided that the delete tools act at once and are marked destructive, so the assistant asks the user before calling them where its settings say to. Task View's [Undo bar](task-list.md#keys-and-the-mouse-reorder-resolve-and-delete) and Contacts' [question](contact-list.md#add-and-delete-change-the-contacts) are screens an assistant doesn't have.

- **The delete-task tool's description says that resolving is the undoable path**: a resolved task can be reopened, and Tasks deletes it after the [delay every device shares](../tasks.md#resolved-tasks-are-deleted-after-a-delay-every-device-shares).
- **The delete-contact tool's description says that a deleted contact can't be restored.**

### Reading the log filters as the Logs window does

The user decided that the `read_log` tool takes the [Logs window's](logs.md#filters-narrow-the-list) four filters, through the Logs module's one [log filter](logs.md#one-filter-decides-which-entries-match), so each filter rule has one home. The time presets stay in the window: the tool takes moments, and its result gives the time now to count back from. Every input is optional:

| Input | What it takes | Left out | Refused when |
| --- | --- | --- | --- |
| Level | The minimum level's name, one of the filter's five, Debug to Fatal, ignoring case | Information, the window's default | Any other name, or a number |
| Components | A list of component names, as the log spells them | Every component | The list is empty, or a name isn't in the log; the error lists the log's components |
| From | A moment, as an ISO 8601 date and time with its offset or `Z`, such as `2026-11-23T10:40:00-08:00`; entries at it or later | No start | It isn't such a time, or has no offset |
| To | The same; entries before it | No end | The same, or it's earlier than From |
| Text | Text the message or the exception contains, ignoring case | Every entry | Never |
| Limit | How many entries to return, 1 to 500 | 100 | It isn't a whole number from 1 to 500 |

The result holds:

- **The matching entries, newest first, at most the limit.** Each has its time, in this PC's local time with its offset and milliseconds, its level, its component, its whole message, and its exception's text, or none.
- **How many entries matched, and how many the log holds**, as the window's "Showing N of M", so the assistant knows when the limit cut the list.
- **Every component in the log**, as the filter lists them, whatever the inputs, so the assistant can choose one.
- **The time now**, in this PC's local time with its offset, so the assistant can ask for "the last hour" itself.
- **The count of lines that couldn't be read, and any write problem**, as [`LogRead`](../../../src/Desktop/Logs/LogRead.cs) holds them.

The tool checks every input before it reads, except the component names, which it checks against the read's components before filtering. It reads once through the log source, filters in one pass, and keeps only the newest entries up to the limit, never a sorted copy of every match.

The user decided on the default level, Information; the limit, 100 by default and at most 500; a To that leaves out its own moment; times that must carry an offset; an unknown component refused, not matching nothing; and the time now and local times in the result.

### A switch in Configure turns it off on this device

The user decided that the server is on by default, and that a switch in [Configure](configure.md#the-ai-assistants-card-lets-assistants-in-and-shows-how-to-connect-them) turns it off on this device only.

- **The switch is kept in the device folder**, `%LocalAppData%\Memoria-ToDo\`, beside Updates' [Auto update](updates.md#auto-update-updates-each-night-once-no-window-is-open) and the [repository folder's path](configure.md#the-sync-settings-section-sets-the-devices-clone), in a file the MCP server owns. It never travels to other devices. The user decided that the file is `mcp-server.txt`, and no file means on. Its messages call it "The MCP server's switch file".
- **The platform's [on-or-off setting file](README.md#an-on-or-off-setting-file-keeps-one-switch-on-this-device) reads and writes it**, so a damaged file never lets assistants in that the user shut out. Configure shows why beside the switch when it can't be read. The user decided this.
- **Turning it off stops listening at once**, ends every connection, and lets the calls in progress end. A call that arrives on an ending connection is refused, as while the app closes, so no change gets in after the user shut assistants out. Turning it on starts listening at once, even while the old connections still finish their calls, and never ends the new connections.
- **The server reports its state**: on and listening, on and starting, on but failed with the reason, or off. Configure shows it.
- **The server makes the lines each assistant needs to connect**, from the path of the program the bridge runs, so the lines have one home. Configure shows them, ready to copy, for VS Code, Claude Desktop, Claude Code, and Codex. Each text is only what goes in the assistant's file, or the command to run, so pasting all of it gives a valid file; the [user's guide](#the-users-guide-says-how-to-connect-an-assistant) says where it goes.
- **The bridge's argument, `--mcp`, has one home**: `McpServer.BridgeArgument`, which the lines use and the composition root checks for.

### Every call is logged

The server logs what [Logging's table](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens) lists for it, under the component name "MCP server". The module each tool calls logs its own change, as it does for a window.

- **A refused or failed call's log entry names the tool, the IDs, and the exception's type, never the module's message**, since a message can quote a contact's fields, such as an alias another contact holds. The assistant still gets the whole message. An Error entry, for an operation that failed, also carries the exception, so its stack trace explains the failure; invalid input, which is what quotes a contact's fields, is logged at Warning with the type only.
- **An edit refused as stale is logged at Warning** with the exception's type, as invalid input is, since the assistant fixes it by reading the task again.
- **Input the SDK refuses before a tool runs**, such as a number where a name is asked, or a missing required input, is logged at Warning with the tool's name, like any other invalid input.
- **`read_log` writes a Debug entry with its effective level, bounds, components, and limit, and the counts read, matched, and returned**, so a short answer can be told from a cut one. It never logs the searched text or the entries it returns.

### The user's guide says how to connect an assistant

The user decided on a user page and the copy-ready lines in Configure. The page is `docs/user/connect-an-ai-assistant.md`, written with this module, and the [README](../../../README.md) links to it then. It says, for VS Code, Claude Desktop, Claude Code, and Codex, where the lines go, and to copy them from Configure. The user decided that the page doesn't repeat the lines, so their format has one home, in the code that makes them.

### It uses the official C# SDK, which a proof checked

The user decided on the official C# SDK's core package, with no ASP.NET Core, provided a small proof shows that its server runs over a stream such as the pipe. The proof passed.

| Package | Version | Used by |
| --- | --- | --- |
| [ModelContextProtocol.Core](https://www.nuget.org/packages/ModelContextProtocol.Core) | 2.4.0, the newest stable version when the proof ran | The MCP server module only |

- **What the proof showed**, on .NET 11 RC 3 with 2.4.0:
  - The SDK's server (`McpServer.Create` over a `StreamServerTransport`) serves a client over a named pipe opened with `PipeOptions.CurrentUserOnly`, and negotiates protocol version 2026-07-28.
  - Tools carry their annotations: a tool made read-only lists `readOnlyHint` true, and one made destructive lists `destructiveHint` true.
  - The SDK hides an exception's message from the assistant, unless the exception is an `McpException`. So each tool turns a module's exception into an `McpException` with the module's message, which then reaches the assistant as "An error occurred invoking '<tool>': <message>".
  - The SDK has no in-memory transport. The unit tests join a `StreamClientTransport` and a `StreamServerTransport` through in-process streams instead.
  - A Windows program with no console, as the bridge is, reads and writes the standard input and output that its starter redirects.
  - The package brings one other package, Microsoft.Extensions.AI.Abstractions 10.11.0. The package's license is Apache-2.0.
- **Not shown by the proof**: how the pipe's access list behaves beside `CurrentUserOnly` for a client with another elevation, and for a network logon. The integration tests check the access list.
- **[Directory.Packages.props](../../../Directory.Packages.props) pins the version**, as the [technology stack](../technology-stack.md#three-files-pin-every-version) says, when the module is built.
- **Only the MCP server module references the package**, which an [architecture test](../technology-stack.md#architecture-tests-check-the-module-map) checks. The bridge copies bytes and needs no SDK.

### Tests drive each tool through the SDK, over the shared fakes

The user decided the tests, following the [test kinds](../technology-stack.md#attributes-mark-each-tests-kind):

- **Unit tests** call each tool through the SDK's client and server joined by in-process streams, over the [shared fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test) for Tasks, Contacts, Sync, Updates, Start with Windows, the log source, and the file system, and the shared fake of the time. They cover each tool's result and errors, that the excluded operations have no tool, the switch and its file, and the Information, Warning, and Error entries the server writes.
- **Integration tests** start the real pipe: a program of this user connects, and the access list denies network logons. They also run the bridge: it copies a call through, and when no pipe exists it starts the app and connects once the app listens.
- **No UX tests**, since the server has no screen. Configure's card gets Configure's own tests.

## Public interface

Shell, Configure, and the composition root use the module, `Memoria.ToDo.Desktop.MCPServer`. Its shape follows [Updates'](../../../src/Desktop/Updates/PublicAPI.Unshipped.txt): one object with a part for Shell and a part for Configure.

```csharp
namespace Memoria.ToDo.Desktop.MCPServer;

/// What Shell uses.
public interface IMcpServerLifetime
{
    /// Reads the switch and, when it's on, starts listening. Called once, on the UI thread.
    void Start();

    /// Stops listening, ends every connection, cancels the calls in progress, and waits for them.
    /// Repeated calls join the same stop.
    Task StopAsync();
}

/// What Configure uses.
public interface IMcpServerStatus
{
    /// The state now.
    McpServerState State { get; }

    /// Raised on the UI thread after State changes.
    event EventHandler? Changed;

    /// Turns the server on or off, saves it on this device, and starts or stops listening at once
    /// once Start has run. Fails, keeping the previous value, when the switch can't be saved.
    void SetOn(bool on);

    /// What each assistant needs to connect, in the order Configure shows them.
    IReadOnlyList<McpConnection> Connections { get; }
}

public interface IMcpServer : IMcpServerLifetime, IMcpServerStatus { }

/// IsOn is the switch. Listening is true while the pipe takes connections. Failure says why the
/// server can't listen, or why the switch can't be read or saved; null when nothing failed.
public sealed record McpServerState(bool IsOn, bool Listening, string? Failure);

/// One assistant's name, such as "VS Code", and the text to copy for it.
public sealed record McpConnection(string Assistant, string Text);

public static class McpServer
{
    /// The argument that starts the app's program as the bridge: "--mcp".
    public const string BridgeArgument = "--mcp";

    /// deviceFolder holds the switch's file. pipeName is the pipe the bridge connects to, and
    /// bridgePath the program the bridge runs. time gives the time now and this PC's time zone, for the log tool.
    /// direct adds and edits tasks and adds images, as ITaskStore.Direct gives it.
    /// Throws ArgumentNullException for a missing argument,
    /// and ArgumentException for a deviceFolder that isn't fully qualified, before anything starts.
    public static IMcpServer Create(ITasks tasks, IDirectTaskChanges direct, ITagColors tagColors, ITagOrder tagOrder,
        ISharedTaskSettings settings, IContacts contacts, ISyncService sync, IUpdateStatus updates,
        IStartWithWindows startWithWindows, ILogSource logs, IFileSystem files, string deviceFolder,
        string pipeName, string bridgePath, TimeProvider time, ILogger logger);
}
```

The user decided on these declarations, and on `IStartWithWindows` from the [desktop platform](README.md#start-with-windows-is-a-port-the-platform-publishes). The user decided that adding a task, editing one, and adding an image go through [direct task changes](../tasks.md#direct-task-changes-are-a-capability-of-their-own), and that `Create` takes them as `IDirectTaskChanges direct`, after `tasks`, which the composition root passes as the store's `Direct`. `Create` already takes each of the store's capabilities it uses, `ITagColors`, `ITagOrder`, and `ISharedTaskSettings`, on its own; taking the whole `ITaskStore` instead would give two ways to reach them, and hand the server `Synchronization`, which it never uses.

The [module map](../modules.md#module-map) has the module's row. The user's decisions also change four other designs:

- **[Tasks](../tasks.md#direct-task-changes-are-a-capability-of-their-own)** names the MCP server as the second caller of its direct task changes, beside the local API.
- **[Shell](shell.md#public-interface)** starts and stops the server, and takes its lifetime.
- **[Configure](configure.md#the-ai-assistants-card-lets-assistants-in-and-shows-how-to-connect-them)** shows the switch, the state, and the lines to copy, and takes the server's status.
- **The [desktop platform](README.md#start-with-windows-is-a-port-the-platform-publishes)** publishes Start with Windows, which Configure and the server both use.

The composition root makes the pipe's name, the server, and the bridge mode, and passes the server to Shell and Configure.

## Failure modes

- **The pipe can't be made**, for example because another program holds its name: the rest of the app works. The server logs why at Error, and Configure shows it beside the switch. Nothing retries until the next start, or until the user turns the switch off and on. A new listener claims the name as its first instance only when the server holds no instance of its own; if the last old instance closes just between that check and the claim, the name is made without the claim. That leaves a short gap that only another program of the same user can use, since the pipe's access list lets only the current user make instances; this is an accepted risk.
- **The switch can't be read**: the server stays off, logs it at Warning, and Configure shows why. **It can't be saved**: the switch keeps its previous value, and Configure shows why.
- **A tool's input is invalid**, such as an empty title, an unknown task ID, an image's bytes that aren't valid or are over 20 MB, or a log From later than its To: the server or the module refuses it before any change, and the tool returns the message as an error. The server logs it at Warning.
- **An operation fails**, such as a file that can't be written: the tool returns the module's message as an error, and the server logs it at Error.
- **The task changed between the edit tool's read and its save**, such as by a sync: Tasks refuses the edit before writing, the tool returns an error that says the task changed meanwhile and to read it again, and the server logs it at Warning.
- **The task or contact was deleted meanwhile**, such as by a sync: the tool returns the module's not-found error. `save_contact` is the exception: it treats the contact as new, so a full name adds it back and no full name is refused, as a save from the window adds it back.
- **No clone is selected**: every tool that reads or changes tasks or contacts returns an error that says to choose a repository in Configure, the same message [the windows show](README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them).
- **The app stops during a call**: the server stops taking calls, cancels the calls in progress, and waits for them, then Shell stops Sync. A call that arrives meanwhile gets an error that the app is closing, and the bridge ends when the pipe closes.
- **The bridge finds no pipe**: it starts the app, or waits for the running one, at most 30 seconds, then ends with a message on its standard error, as [above](#an-assistant-reaches-it-through-a-pipe-and-a-bridge-the-app-provides).
- **The pipe belongs to another user, or the assistant runs as administrator**: the bridge refuses to connect and says why.
- **An assistant's job object forbids the app to leave it**: the app the bridge started ends when the assistant ends. The next start at sign-in or by hand brings it back; this is an accepted risk. Not confirmed for each assistant.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Which operations get a tool:
  - **Literally everything, security setup included**: an injected instruction could send every task to a stranger's repository, or make the device trust a forged server.
  - **Tasks, tags, and contacts only**: less than the user asked for.
  - **A read-only switch in Configure**: the assistant's own approval of each tool does that job.
- How an assistant reaches the server:
  - **The app speaks MCP on its own standard input and output**: the assistant starts its stdio server as its own child program and ends it, but the tray app starts at sign-in and runs once.
  - **Streamable HTTP on `127.0.0.1`**: it needs a port, a token, checks against DNS rebinding, and ASP.NET Core, and Claude Desktop would need a bridge anyway.
  - **Both the pipe and HTTP**: twice the surface to secure and test.
  - **A second, separate server program with its own copy of the data**: two programs writing one clone would need a lock across processes, and the open windows wouldn't see the changes.
- Who may call it:
  - **A token**, a random one per device or OAuth: any program of this user can already read the clone's files, so a token adds a secret without closing a door. It came with HTTP, which is ruled out.
  - **No token on a local port**: it went with HTTP, which is ruled out.
- What the bridge does when the app isn't running:
  - **It fails with a message saying to start Memoria-ToDo**: the assistant would stop working whenever the user quit the app.
- Which port it listens on, and how assistants find it: no port, since HTTP is ruled out. That rules out a fixed port that Configure changes, a random port written to a file, and a fixed port that falls back to another.
- Where the server lives:
  - **The composition root starts and stops it**: the composition root would then own shutdown ordering, which Shell owns.
  - **A shared module, for the Android app too**: the Android app isn't always running, and assistants don't run on the phone.
- Deleting:
  - **The app asks in a dialog before an assistant's delete**: a window pops up over the user's work, and the assistant waits on it.
  - **No delete tools**: contacts couldn't be deleted, and the user asked for every operation.
- Adding a task without touching the draft:
  - **`Add` deletes the kept draft only when the draft it adds is the kept one, matched by task ID**: the user chose a separate operation, now [direct task changes](../tasks.md#direct-task-changes-are-a-capability-of-their-own)' `Add`, so `Add` with a draft keeps its rule.
  - **No add tool**: it rules out the most common use.
- Which operation the edit tool uses:
  - **`ITasks.SaveEdit`, with the task the server read as the started version**: it moves the images waiting in the user's open edit into the clone, so an assistant's edit would publish images the user pasted but hasn't saved, and the user's discard couldn't remove them. The user chose direct task changes' `Update`.
- An edit while the task changes:
  - **The assistant sends the task's `Updated` time, and the edit fails when the task changed since the assistant read it**: assistants would re-read and retry more often, unlike how a sync's change meets an open edit. The server reads the time itself, just before the save.
  - **The server retries a stale edit on its own**: it would write the assistant's fields over a change the assistant hasn't seen.
- Saving part of a contact:
  - **The tool reads the contact from `Groups`, then calls `Save`**: a sync's reload or a window's save between the two is undone for the fields left out.
  - **A required full name**: an edit couldn't leave it out.
- Tool shape:
  - **Reads as MCP resources, changes as tools**: some assistants can't read resources.
  - **A few broad tools, such as one `tasks` tool with an action argument**: each tool's input is vaguer and harder for the model to use.
- Images:
  - **No image tools**: the user wanted assistants to add images.
  - **The image tool writes only the image and returns its Markdown, which the assistant links with the edit tool**: an image whose edit never comes stays in the clone and syncs, unlinked, and it needed a second `ITasks` operation beside direct task changes. The user chose direct task changes' `AddImage`.
- Reading the log:
  - **An earliest time only, with the assistant filtering the newest 500 itself**: the user decided the tool filters as the Logs window does.
  - **The window's time presets as an input**: they fill the window's boxes from "now"; moments and the result's time now do the same job.
  - **A tool of its own that lists the components**: one more call, while every read already returns them.
  - **A time without an offset read as this PC's local time**: friendlier, but a time in the hour the clock repeats in autumn has two meanings.
- The SDK:
  - **`ModelContextProtocol.AspNetCore` with Kestrel**: needed only for HTTP, and it adds the ASP.NET Core shared framework to the app.
  - **Writing the JSON-RPC by hand**: the protocol is large and changes.
- Connecting an assistant:
  - **The user page only**: the user would have to find the bridge's path by hand.
- Turning it off:
  - **No switch**: the only way to stop it would be to quit the app.
  - **Off until the user turns it on**: against the user's request that it run while the app runs.
- How the task tools set several tags:
  - **One text input with the tags separated by commas**: a tag can hold a comma, and the input's meaning would be hidden in its text.
- Whether the task tools keep their one-tag input:
  - **Keeping `tag` beside `tags`, failing when both are given**, as the [local API](../local-api-contract.md#a-tasks-tags-travel-as-a-list-beside-the-older-one-tag-field) does: two ways to say the same thing, and the model may pick the wrong one. The local API keeps both only because compiled programs depend on it.
## Open questions

None.
