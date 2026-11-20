# Logs: the window that shows the app's log

Status: Decided

The Logs window lets the user read what the app did and what went wrong, without opening files: the entries the [log](../logging.md) keeps for two weeks, newest first, narrowed by time, component, severity, and a quick search. The tray menu opens it. It's a desktop module of its own, Logs, which uses no other module, as the user decided.

## Behavior

### The tray menu's Logs item opens one window

- **The tray menu has a Logs item, after Configure and before Exit.** It always shows, also before the device is configured, since that's when setup problems need reading. Unlike Add and Task View, it never redirects to Configure. Shell owns the [menu](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions). Clicking a sync error notification opens it too, as the user decided, since the log holds the error's details.
- **Opening it closes every other window**, as the user decided: Add keeps its draft, and Task View and Contacts may first ask about a changed edit. Logs opens whatever the answer. Proposed, a decision to confirm.
- **No global shortcut opens it.** Reading logs is rare, and the shortcuts are kept for daily work. Proposed, a decision to confirm.
- **One window at a time.** Logs again with the window open brings it to the front. It uses the platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show), opens on the [screen with the mouse pointer](README.md#windows-open-on-the-screen-with-the-mouse-pointer), follows the theme, and closes with Esc and the [Close window shortcut](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows), as the other windows do.
- **It stays out of the shortcuts' switching rules.** The global shortcuts [switch](shell.md#a-shortcut-switches-between-add-and-task-view) as if it weren't open, and an update's restart doesn't wait for it, since it holds nothing to save; it closes with the app. Proposed, a decision to confirm.

### It loads every kept entry off the UI thread

- **Opening the window reads every kept log file** through the [log source port](#public-interface), off the UI thread, with "Loading…" in the list until it's done. Two weeks at the [30 MB cap](../logging.md#the-desktop-app-keeps-two-weeks-of-logs-at-most-30-mb) load in about two seconds or less. Proposed, a decision to confirm.
- **A Refresh button, and F5, read the files again**, keeping the filters and search. The list doesn't follow new entries by itself, so it never moves while the user reads it, and the window reads files only when asked. F5 does nothing while a load runs. The user decided this.
- **Bringing an open window to the front doesn't reload it**, so the user's place stays.

### The list shows newest first, one row per entry

- **Columns: Time, Severity, Component, Message.** Time is this PC's local time, with the date and milliseconds. Message shows the first line of the rendered message; a row with an exception shows a mark beside its severity. Error and Fatal rows show in red and Warning rows in orange, as the user decided, in shades for the light and the dark theme. The list, its headers, and its rows follow the theme.
- **Newest first**, since the question is usually "what just happened". Proposed, a decision to confirm.
- **Selecting a row shows the whole entry below the list**, in a pane the user resizes by dragging the bar above it up or down. The pane opens at its smallest height, 65 pixels, as the user decided, so the list gets the room. The bar is Logs' own, since the platform's [split panes](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them) only place two sides beside each other. The pane holds the full message and the exception's text with its stack trace. The text can be selected and copied.
- **Ctrl+C copies the selected rows** as text, one line each with time, severity, component, and message, followed by any exception, so the user can paste them into an issue. Proposed, a decision to confirm.
- **The list scrolls smoothly through every entry**: only the rows on screen are drawn.

### Filters narrow the list

A bar above the list holds four filters, as the user decided. Each change applies at once, and every filter must match for a row to show:

- **Time range: presets that fill a From and a To bound the user can edit.** The presets are Last hour, Today, Last 24 hours, Last 7 days, and All, which is the default.
  - A preset fills From with its start, in this PC's local time, and empties To, which means "up to now". Today starts at midnight; All empties both bounds.
  - Only the bounds a choice uses show, as the user decided: All shows neither, the other presets show only From, and Custom shows both.
  - Each bound is a date picker and a time box, as the user decided, so the user can look at the exact minute an error happened. WPF has no time picker, so the time box is an editable list of every half hour that also takes any time typed, such as `10:43`, checked as the user types. No date sets no bound, and the time box is off until a date is picked. Picking a date while the time is empty fills in 00:00 for From and 23:59 for To, so a To date includes its whole day. Proposed, a decision to confirm. A To of `9999-12-31 23:59` sets no bound either. Each bound includes its own minute.
  - Editing a bound changes the preset list to Custom. Choosing Custom in the list fills an empty To with the current minute, as the user decided. A time that isn't a time shows red, a From later than To shows both bounds red, and the time range doesn't filter until it's fixed.
  - Refresh and F5 work a preset out again from the current time, so Last hour still means the last hour. The user decided this.
- **Severity: a minimum level.** The list holds Debug and up, Information and up, the default, Warning and up, Error and up, and Fatal only: the five [levels the app uses](../logging.md#five-levels-say-what-an-entry-records). Proposed, each a decision to confirm: Debug and up is first in the list but not the default, since most looks are for operations and errors, and the user picks it to see the [choices behind a result](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens); and Fatal gets its own item, for the errors that ended the app.
- **Component**: a list of the components found in the loaded entries, in name order ignoring case, as the user decided, such as `Sync` or `Tasks`, each with a check box, all checked by default, with one box to check or clear them all. The component is each entry's `SourceContext`, which the [log](../logging.md#every-module-logs-through-serilogs-logger) names after each module. An entry without one shows as `Unknown`. Proposed, a decision to confirm.
- **Quick search**: a text box, through the platform's [search band](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them). A row shows when its rendered message or its exception's text contains the typed text anywhere, ignoring case. So a search for `IOException` or a file named only in a stack trace finds the entry. An empty box matches every row.

Each change of a filter or the search shows its result within a quarter second on 30 MB of logs. Proposed, a decision to confirm.

### A status line says what the list holds

The parts that apply share one line, joined by " · ":

- **"Showing N of M entries"**, which follows every filter change.
- **"K lines couldn't be read"**, when some lines in the files are broken, such as one cut off by a crash.
- **"The log can't be written: reason"**, while the [writer is failing](../logging.md#a-log-that-cant-be-written-never-stops-the-app), so the user knows the newest entries may be missing.

## Public interface

Logs is a desktop module, `Memoria.ToDo.Desktop.Logs`, in `src/Desktop/Logs/`. It uses no other module, only the desktop and shared platforms and Serilog's core package, for its levels and its logger. Shell uses it, for the window port, as the [module map](../modules.md#module-map) says.

Proposed, a decision to confirm: the window reads the log through a port it owns, which the composition root implements with Serilog's reader, beside the writer. So the folder, the file names, and the format keep [one home](../logging.md#the-desktop-app-keeps-two-weeks-of-logs-at-most-30-mb), and the window's unit tests read entries from a fake source, with no files:

```csharp
namespace Memoria.ToDo.Desktop.Logs;

/// One log entry as the window shows it.
/// Level is Serilog's. Component is the entry's SourceContext, or "Unknown" without one.
/// Message is the rendered message. Error is the exception's full text, or null without one.
public sealed record LogEntry(DateTimeOffset Time, Serilog.Events.LogEventLevel Level, string Component,
    string Message, string? Error);

/// What one read of the kept log found.
/// UnreadableLines counts broken lines that were skipped. WriteProblem is the writer's failure, or null while it writes.
public sealed record LogRead(IReadOnlyList<LogEntry> Entries, int UnreadableLines, string? WriteProblem);

/// Where the window reads the log. The composition root implements it beside its log writer.
public interface ILogSource
{
    /// Reads every kept entry, in no set order, without blocking the caller's thread.
    /// A file deleted while it's read is left out. A missing log folder reads as no entries.
    /// Throws only when the log folder can't be listed.
    Task<LogRead> ReadAsync(CancellationToken cancellationToken);
}

/// The Logs window, as Shell uses it.
public interface ILogsWindow
{
    /// Opens the window and loads the log, or brings an open window to the front.
    void Show();
}

public static class LogsWindow
{
    /// Creates the Logs window. It opens only on Show. Throws ArgumentNullException for a null argument.
    public static ILogsWindow Create(ILogSource source, ITheme theme, CloseWindowKeys closeWindowKeys,
        TimeProvider time, Serilog.ILogger logger);
}
```

`TimeProvider` gives "now" for the time presets. The window logs its own failures through `logger`, as the [log](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens) requires. The window's filtering lives in an internal view model, so unit tests check every filter with a fake log source and no window, as [the desktop app](README.md#eight-modules-make-up-the-desktop-app) does for every window module. The `ILogSource` fake, for Logs' tests, and the `ILogsWindow` fake, for Shell's, live in the desktop fakes project beside the other window fakes.

## Failure modes

- **The log folder can't be listed**: the list is empty and the status line says why. The failure is logged, which may also fail; nothing else changes.
- **A file can't be read, fails partway through, or vanishes while it's read**, such as one the writer's clean-up just deleted: it's left out, its entries don't show, and the log source logs it at Warning.
- **A line is broken**, cut off or not an entry Serilog's reader can read: it's skipped and counted in the status line.
- **There are no entries yet**, or no log folder yet: the list says "No log entries yet."

## Ruled out

- Where the window lives:
  - **Inside Shell**, decided by the user: Shell would hold a list, filters, and a view model that have nothing to do with the tray, and it would grow past one capability.
  - **Inside Configure**, as a tab: Configure is for settings, and a big list would slow a window people open to change one setting.
- **The window reads the files itself, with Serilog's reader**: no port, but the folder and file names would then live in two projects, and every filter test would need files on disk.
- **Opening the log file in Notepad**: no filters, and a two-week log in several files is hard to read.
- The time range, decided by the user:
  - **Presets only**: no way to look at, say, 10:40 to 10:45 three days ago, except by scrolling.
  - **From and To boxes only**: exact, but every look needs typing.
- **A check box per severity level**, decided by the user: it also allows "Information only", which is rarely useful.
- **Debug and up as the default**: every look would start among the choices behind results, which crowd the operations and errors most looks are for.
- **A quick search of the message only**, decided by the user: a search for an exception's type, or a file named only in its stack trace, would find nothing.
- **A list that follows new entries as they're written**: the list would move while the user reads, and the writer would need to tell the window about each entry. Ruled out by the user.

## Open questions

None yet.
