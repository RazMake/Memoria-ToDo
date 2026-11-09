# Task editor: how the desktop app adds a task in seconds

Status: Decided

The Task editor is the desktop app's Add window, built so one key press puts the cursor in a new task's title with no wait. View Tasks edits a task in place with the same editor.

## Behavior

### Add opens at once, with the cursor in the Title

The Add window is created when the app starts and stays hidden until needed, so showing it loads nothing. It opens centered on the active screen, on top of other windows, with three fields:

1. **Title**: one line, where the cursor starts.
2. **Description**: grows with its content, then scrolls. Proposed: it is at least 140 and at most 400 device-independent pixels tall, so the window stays on screen. This is a decision to confirm.
3. **Tags**: the task's tags, with the most-used tags as chips below.

Above the fields, a small **Clear** button empties Add and deletes its [draft](#closing-without-saving-keeps-a-draft-on-this-device). Below them, a **Save** button saves the task, exactly as [Ctrl+Enter](#keys-save-close-and-move-between-fields) does. Clear is smaller than Save and sits apart from it, so it isn't clicked by mistake.

Title and Description use the editor that [How the apps fit together](../overview.md#each-app-edits-in-its-own-text-box-with-shared-commands) picks: [AvalonEdit](https://www.nuget.org/packages/AvalonEdit/6.3.1.120) 6.3.1.120, a dependency of the Task editor's project. It restores and builds on .NET 11, but its first run there is not confirmed until the UX tests run. It references the whole desktop framework, so the project turns off the WinForms analyzer warning WFO2000, which is meant for WinForms controls. Until the formatting and image slices need more of it, the Tags line stays a plain WPF text box.

Title and Description look like the Tags line in both the light and the dark [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it): the same background, text color, border, and rounded corners. The theme styles only WPF's own text box, not AvalonEdit's editor, so one place in the Task editor, used by both fields, gives them the look from the theme's own text box resources. The Task editor copies no color, so the fields switch with the theme as the Tags line does. A Title marked as missing keeps its red border.

Proposed: Add accepts typing within 100 ms of the Add new task shortcut, measured from the key press to the first typed character showing. This is a decision to confirm.

Proposed, each a decision to confirm:

- Show Add opens Add with the desktop platform's [Show on the pointer's screen](README.md#windows-open-on-the-screen-with-the-mouse-pointer), which says what the active screen and on top mean. The pointer is also where a click on the tray icon happens.
- Closing Add, with Esc or its close button, hides it, so the next Show opens the same window at once.
- The user can't resize Add. It has a fixed width, wide enough for a long title on one line, and never opens shorter than a set height; it grows taller with what it holds.
- Title and Description also match the Tags line's padding, and its look under the pointer and with the focus.
- Clear sits at the top right, above the Title. Save sits at the bottom right, below the tag chips and the message line.
- Save has the theme's accent button look, as the window's main action. Clear has the theme's plain button look, with smaller text and padding, so it is shorter than Save.
- Save's tooltip names its key, Ctrl+Enter, so the user learns it.
- Save isn't the window's default button, so Enter keeps its meaning in each field.
- The keyboard reaches both buttons with Tab, in the order they show.
- Both buttons belong to the Add window, not to the editor the Task list reuses, so the [public interface](#public-interface) doesn't change.

### Keys save, close, and move between fields

The table lists the keys that work across the window, and which are still proposals:

| Key | Where | What it does | Status |
| --- | --- | --- | --- |
| Enter or Tab | Title | Moves to the Description | Decided |
| Tab | Description, outside a list item | Moves to the Tags line | Proposed |
| Enter | Tags | Adds the chosen suggestion, or the typed tag | Proposed |
| Ctrl+Enter | Anywhere | Saves the task, as the Save button does. Proposed: and closes Add. | Decided |
| Ctrl+Shift+Enter | Anywhere | Saves the task and clears Add for another one | Proposed |
| Esc | Anywhere | Closes Add and keeps a draft. Proposed: closes an open list, such as the snippet picker, first. | Decided |

Proposed, each a decision to confirm:

- Ctrl+Enter, Ctrl+Shift+Enter, and Esc also work while the Clear or Save button has the focus.
- A task with an empty Title can't be saved: Add stays open and marks the Title.
- Pasting text with line breaks into the Title joins its lines with spaces, since a title is one line.
- Pasting or dropping an image runs the image snippet.
- Ctrl+Shift+I inserts the clipboard's image when it holds one, and otherwise opens a file dialog to choose one. Cancelling the dialog inserts nothing and shows no error.
- Ctrl+V runs the image route only when the clipboard holds an image, and pastes as usual otherwise. Ctrl+V, Ctrl+Shift+I, and file drops work in the Description only; the Title ignores them.
- A chosen or dropped file's alt text is its name without the extension, and `image` when that is empty, as is an image from the clipboard's. A drop inserts at the cursor, in place of the selection, and uses only the first dropped file. A dropped file that isn't an image shows an error. Drops that carry no file, such as dragged text, keep their usual meaning in the editor.
- The snippet list's Image entry does what Ctrl+Shift+I does, in place of the "/" and what was typed after it. When the dialog is cancelled, the typed text stays.
- An image that can't be read, isn't an image, can't be shrunk to 1 MB, or can't be kept shows an error in the Add window's message line. The error mentions the image, nothing is inserted, and the text, cursor, and focus stay as they were. The next image that is added clears the message.
- Opening the file dialog doesn't hide Add, though the dialog takes the focus.

### Every formatting command and snippet has a key

[Editing](../editing.md) defines each command and the fields it works in; this page only maps desktop keys to them. Typing "/" in the Description also opens Editing's snippet picker, which the Task editor shows as a list under the Description.

The table maps each command to its desktop key. Proposed: these keys. This is a decision to confirm.

| Command | Key |
| --- | --- |
| Bold | Ctrl+B |
| Italic | Ctrl+I |
| Strikethrough | Ctrl+Shift+X |
| Inline code | Ctrl+E |
| Color: opens a list of the palette's colors | Ctrl+Shift+C |
| Link, filled from a URL on the clipboard | Ctrl+K |
| Heading 1, 2, or 3 | Ctrl+1, Ctrl+2, Ctrl+3 |
| Date stamp | Ctrl+; |
| Date and time stamp | Ctrl+Shift+; |
| Note, tip, or warning block | Ctrl+Shift+N, Ctrl+Shift+T, Ctrl+Shift+W |
| Image, from the clipboard or a file | Ctrl+Shift+I |
| Code block | Ctrl+Shift+E |
| Reference to another task | Ctrl+Shift+R |
| Table | Ctrl+Shift+G |
| Horizontal line | Ctrl+Shift+- (hyphen) |

No key uses Ctrl+Alt, which [Shell rules out](shell.md#ruled-out). The stamp keys follow Excel, where Ctrl+; inserts the date; Excel's Ctrl+Shift+; inserts only the time ([Excel](https://support.microsoft.com/en-us/office/insert-the-current-date-and-time-in-a-cell-b5663451-10b0-40ab-9e71-6b0ce5768138)), but here it inserts the date and time.

Proposed, each a decision to confirm:

- The color list opens under the Description, not at the cursor, and shows only while open. Click a color, or press Down to move through them and Enter to choose, as with tag suggestions. Esc closes it first. The cursor stays in the field, and the chosen color wraps the field's selection.
- Each formatting and heading key runs on the field with the cursor. Each key's result replaces only the text that changed, as one undo step, and keeps the selection Editing returns.
- Editing sees every line break as "\n". If the text holds any "\r\n" or "\r", the key converts them first.
- In the Description, Enter, Tab, and Shift+Tab run Editing's list commands in a list item. Tab on a first item that can't move is swallowed: the cursor stays in the Description. Outside a list item, Enter breaks the line as usual, Tab moves to the Tags line, and Shift+Tab moves back to the Title.
- The snippet list and the task list open under the Description, like the color list, not at the cursor. The color, snippet, and task lists work alike, and at most one is open: opening one closes the others. Down moves through the items and Enter chooses the one moved to. Enter with a list open and nothing moved to closes the list and then does what Enter does with no list.
- Items are chosen by position, so tasks with equal titles stay apart.
- The snippet list closes when nothing matches, when the cursor moves before the "/", or when the "/" no longer opens Editing's picker, such as after Backspace removes it. Choosing a snippet replaces the "/" and what was typed after it, even for the link, whose text is then empty.
- Snippet keys do nothing in the Title unless Editing says the snippet works in a Title.
- Empty or missing clipboard text counts as no web address for the link, so the link gets an empty address and no error shows.
- The task list shows every open task, newest first, until the user types; what is typed after it opens narrows it by Tasks' search. With no open task, or none matching, no list shows. Choosing a snippet that needs a task from the snippet list opens the task list, and choosing a task replaces the "/" and what was typed.

The reference to another task opens a list, as above. It lists open tasks only, and typing filters them with [Tasks' search](../tasks.md#tags-and-search-ignore-case). The link's text is the chosen task's title with its Markdown kept, and only brackets and backslashes escaped; the list itself shows the title as a reader sees it, without its Markdown, which [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html) mark.

### Closing without saving keeps a draft on this device

Closing Add without saving, with Esc or by clicking elsewhere, keeps its text as a draft. The next Add opens with the draft, and the Clear button clears it. Drafts stay on this device and never sync.

[Tasks keeps the draft](../tasks.md#a-draft-waits-on-this-device-until-its-saved), with its images, in `%LocalAppData%\Memoria-ToDo\draft\`, a folder the composition root gives it. Add keeps the draft when it hides and when the app exits, and saving the task clears it. Clicking Add's own buttons, its lists, or its file dialog isn't clicking elsewhere.

Add keeps the draft each time it hides, with every field empty meaning no draft: the kept draft is removed. Show restores the kept draft into all three fields with the cursor in the Title, and a draft whose task ID is already a task is deleted and Add opens empty. Clear empties the fields, deletes the kept draft and its images with [Tasks' `DiscardDraft`](../tasks.md#public-interface), starts a new task ID, and leaves Add open with the cursor in the Title. Clear is only the button's name: Tasks calls that step discard, so no public interface changes. A save that fails leaves the text, so Esc keeps it as the draft.

Proposed, each a decision to confirm:

- Add keeps the draft itself when its window closes as the app exits, so the composition root needs no extra call.
- After Clear, Add shows no message and no mark on the Title, as for a new task.
- Text typed in the Tags line and not yet added as a tag isn't part of the draft, but the hidden window keeps it, so the next Show of the same window finds it. Clear and saving clear it.
- A save that fails with an unreadable-data error keeps Add open and shows the message, as for other failed saves. A save with no title marks the Title with a red border until it is edited.
- While a draft that can't be read is on disk and the fields are still empty, hiding leaves that file alone, so a window opened and closed by mistake doesn't destroy it. Typing something or saving replaces it, and Clear deletes it.
- If keeping the draft fails when Add hides, the error is not caught, so the failure is loud rather than a draft silently lost.

### Tags come from the tags already in use

The Tags line suggests tags already in use as the user types, as [Tasks' tag rules](../tasks.md#tags-and-search-ignore-case) say. Below it, the five most-used tags show as chips; clicking one adds it. The user expects few tags, so five chips usually cover them.

Proposed, each a decision to confirm:

- Chips are buttons, so the keyboard reaches them with Tab.
- The Tags line shows the task's tags, each with a button that removes it, and Backspace in the empty Tags line removes the last one.
- A suggestion is chosen with Down, repeated to move on, then Enter, or with a mouse click. Enter with no suggestion chosen adds the typed text. Tab doesn't accept a suggestion. Esc closes the suggestions first, and a second Esc closes Add.
- The Save button, Ctrl+Enter, and Ctrl+Shift+Enter add any tag text still typed in the Tags line before they save, so no typing is lost.

### The editor styles Markdown in place, with no preview

Add shows Markdown source styled in place: bold looks bold, and headings, links, and code are styled. The styling comes from [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html). Add has no rendered preview and uses no web view, so it never waits for one.

Proposed, each a decision to confirm:

- Colored text shows in its palette color for the current [theme](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it), such as `:red[urgent]` in red.
- Markdown's own characters, such as `**` around bold text, show dimmed.
- A link shows underlined in the palette's blue for the current theme, and inline code in a monospace font on a subtle gray background.
- Styling never delays typing: typed text shows at once, and its styling follows.

### A new task goes to the top

A saved new task goes to the top of the order, where the user sees it first in View Tasks. Tasks computes its rank, as [the desktop app page](README.md#later-designs-answer-what-these-files-leave-open) lists.

### With no clone folder, saving points to Configure

Tasks keeps tasks in [Configure's repository folder](configure.md#the-sync-settings-section-sets-the-devices-clone). Until one is set, Add still opens and keeps a draft, but saving fails: Add stays open with the text and says to set the repository folder in Configure. The composition root gives Tasks the folder when the app starts, and again after Configure clones or picks one. It does this by wrapping Configure's repository-folder port in an App class, `TasksFollowRepositoryFolder`: it calls `UseRepositoryFolder` with the saved folder when it is built, and after each successful clone or use of an existing folder. Configure and Tasks stay unaware of each other. (Decision to confirm.)

## Public interface

The composition root, Shell, and the Task list use these parts:

- **Create the Add window**: the composition root creates it when the app starts, as [above](#add-opens-at-once-with-the-cursor-in-the-title), and gives it to Shell.
- **Show Add**: shows the Add window, with the draft if there is one. Shell calls it from the [Add new task shortcut](shell.md#two-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions), the tray icon, and the tray menu.
- **The editor**: the Title, Description, and Tags of one task, with every key above. It reports when the user saves or cancels. The Task list uses it to edit a task in place.
- **The tag chips**: the five most-used tags as buttons, reporting the chosen tag. The Task list uses them to filter.
- **Two ports**, the clipboard and the image file dialog, which the desktop app implements beside its composition root, so the Task editor's tests run on [fakes](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test).

[Rendering](../rendering.md#public-interface) and [Editing](../editing.md#public-interface) are plain functions, so the Task editor calls them directly. The Task editor reads a dropped or chosen image file through Shared's `IFileSystem`, which `Create` and the editor's constructor take last (decision to confirm), and keeps each image through [Tasks' draft image](../tasks.md#a-draft-waits-on-this-device-until-its-saved).

```csharp
namespace Memoria.ToDo.Desktop.TaskEditor;

public static class AddWindow
{
    /// Creates the Add window. It opens only on IAddWindow.Show.
    public static IAddWindow Create(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files);
}

/// The Add window, as Shell uses it.
public interface IAddWindow
{
    /// Shows the Add window, with the draft if there is one.
    void Show();

    /// Whether the window is open now.
    bool IsOpen { get; }
}

/// The Title, Description, and Tags of one task, with every key on this page.
public sealed class TaskEditorControl : UserControl
{
    public TaskEditorControl(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme,
        TimeProvider time, IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files);

    /// What the fields hold now. Setting it replaces all three fields.
    public TaskDraft Value { get; set; }

    public event EventHandler? SaveRequested;          // Ctrl+Enter
    public event EventHandler? SaveAndClearRequested;  // Ctrl+Shift+Enter
    public event EventHandler? CancelRequested;        // Esc, with no list open
}

/// The five most-used tags as buttons.
public sealed class TagChips : UserControl
{
    public TagChips(ITasks tasks);

    public event EventHandler<string>? TagChosen;
}

/// The system clipboard.
public interface IClipboard
{
    /// The clipboard's text, or null when it holds none.
    string? Text();

    /// The clipboard's image as PNG, or null when it holds none.
    byte[]? ImagePng();
}

/// The dialog that chooses an image file.
public interface IImageFileChooser
{
    /// The chosen file's full path, or null when the user cancels.
    string? Choose();
}
```

## Failure modes

- **No clone folder is set**: Add stays open with the text, says to set the repository folder in Configure, and keeps the draft.
- **Saving fails**, for example because the task file can't be written: Add stays open with the text, shows the error, and keeps the draft.
- **Saving fails and Tasks can't undo it** (it throws an `AggregateException`): Add stays open with the text and the draft, and says the add was incomplete, so the user checks the repository folder before saving again. (Decision to confirm: the wording.)
- **An image can't be read or saved**: nothing is inserted, and Add says why.
- **The draft can't be read**: Add opens empty, says the draft couldn't be loaded, and leaves the draft file as it is.

## Ruled out

The options below were considered and rejected, grouped by decision.

- The Add window:
  - **A full editor window**: Add stays compact, for a quick task.
  - **Opening on the primary screen**: on a PC with two screens, Add could open away from where the user works.
  - **Always on top**: Add would cover a window the user wants to copy from.
- The look of Title and Description:
  - **The Task editor's own colors for each theme**: they would copy the theme's colors and drift from the Tags line.
  - **The theme's text box style itself on Title and Description**: WPF applies that style only to its own text boxes, and AvalonEdit's editor isn't one.
- Add's buttons:
  - **Clear beside Save at the bottom**: a slip of the mouse would clear the task instead of saving it.
  - **Save as the default button, pressed by Enter**: Enter already moves to the Description, breaks lines, and adds tags.
  - **A button for Ctrl+Shift+Enter too**: one more button in a compact window, for a key the user can learn.
  - **Clear and Save inside the editor the Task list reuses**: the Task list's in-place edit has [its own design](task-list.md#edit-turns-the-open-task-into-the-editor-in-place).
  - **Renaming Tasks' `DiscardDraft` to match Clear**: it would change a public interface only for the name on a button.
- Closing without saving:
  - **Esc discards the text, after a confirmation**: a draft keeps the text without asking.
  - **Closing saves the task automatically**: an accidental close would create a half-written task on every device.
- Tags:
  - **Inline #tags in the Title, plus a Tags line**, or **inline #tags only**: tags would mix into the title's text.
- Where a new task goes:
  - **The bottom**: it can scroll out of sight.
  - **A setting**: one more option to test.
- Preview:
  - **A preview key**: the first preview waits about 0.2–0.85 s for WebView2 to start ([WebView2 start-up](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1909)).
  - **A live preview, always on**: about 40–50 MB more memory all day ([WebView2 memory](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1705)).
- The clipboard and the file dialog:
  - **WPF's Clipboard and OpenFileDialog, used directly in the Task editor**: its tests would touch the real clipboard and open real dialogs.
- Choosing the task to reference:
  - **A search dialog**: a second window for what a list at the cursor does, as the snippet picker already does.
