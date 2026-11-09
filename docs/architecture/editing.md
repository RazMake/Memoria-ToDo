# Editing: the formatting, snippet, and list commands

Status: Decided

Editing is the shared module that changes task text when the user formats it, inserts a snippet, or continues a list. Both apps use the same commands, so a command works the same on the desktop and the phone; each app decides which key or touch runs it.

## Behavior

### Commands take text and a selection, and return new text and a selection

Every command is a pure text operation. It takes the field's text and the selection, and returns the new text and the new selection.

- **No command reads a device.** Whatever a command needs from outside, such as the current time, a URL on the clipboard, a saved image's Markdown, or a task's link, the app passes in. That keeps the commands fast and testable, and the same on both apps.
- **Each app maps keys or touch to commands.** The desktop keys are in the [Task editor](desktop/task-editor.md#every-formatting-command-and-snippet-has-a-key), and the phone's toolbar is in the Android [Task editor](android/task-editor.md#a-toolbar-above-the-keyboard-runs-the-common-commands).
- **Some commands also work in the Title.** The Title is one line of inline Markdown, so commands that insert blocks or headings work only in the Description. The tables below say which work in the Title.

### Formatting commands wrap the selection

The table lists each formatting command, the Markdown it adds around the selection, and whether it works in the Title:

| Command | Markdown it adds | Title too |
| --- | --- | --- |
| Bold | `**text**` | Yes |
| Italic | `*text*` | Yes |
| Strikethrough | `~~text~~` | Yes |
| Inline code | `` `text` `` | Yes |
| Color | `:red[text]`, with a [palette](rendering.md#six-colors-read-well-in-light-and-dark-themes) name | Yes |
| Link | `[text](address)` | Yes |
| Heading 1, 2, or 3 | `#`, `##`, or `###` at the start of the line | No |

The color command is the quick way to color text: it wraps the selection in the chosen color's [markers](rendering.md#raw-html-shows-as-text-and-redtext-colors-text), such as `:red[` and `]`. The app passes in the chosen name and the palette's names, which [Rendering](rendering.md) owns.

The link command and the [link snippet](#snippets-insert-common-blocks) are one command, so Editing has one way to insert a link.

Proposed, each a decision to confirm:

- With no selection, a command inserts both markers with the cursor between them.
- On text that already has the markers, the command removes them. The same heading level removes the heading, and another level replaces it.
- Choosing a text's own color again removes the color, and another color replaces it: blue turns `:red[urgent]` into `:blue[urgent]`.
- A URL on the clipboard becomes the link's address; otherwise the cursor waits in the empty address.

### Lists continue as you type

In the Description, Enter continues a bulleted or numbered list, and Enter on an empty item ends the list. Tab and Shift+Tab indent and outdent list items. Proposed: a continued numbered list counts on from the item above. This is a decision to confirm.

Proposed: a bulleted-list command adds `- ` at the start of each selected line in the Description, or removes it when every selected line has it. The Android toolbar's list button runs it, as the Android [Task editor](android/task-editor.md#a-toolbar-above-the-keyboard-runs-the-common-commands) decides. This is a decision to confirm. The desktop app has no key for it, so it joins the public interface with the Android Task editor.

### Snippets insert common blocks

The apps have a built-in set of snippets only. The table lists each snippet, what it inserts, what the app passes in, and whether it works in the Title:

| Snippet | Inserts | The app passes in | Title too |
| --- | --- | --- | --- |
| Date stamp | The local date, `yyyy-MM-dd` | The current time | Yes |
| Date and time stamp | The local date and time, `yyyy-MM-dd HH:mm` | The current time | Yes |
| Note, tip, or warning | An info block, such as `> [!NOTE]` | Nothing | No |
| Link | `[text](address)`, filled from a URL on the clipboard | The clipboard's text | Yes |
| Image | An image, from the clipboard or a file | The saved image's Markdown, from [Tasks](tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) | No |
| Code block | A fenced code block | Nothing | No |
| Reference to another task | A link that opens the task, with its title as the text | The task's link address, from Tasks, and its title's own Markdown | Yes |
| Table | A table with 2 columns | Nothing | No |
| Horizontal line | `---` | Nothing | No |

For the task reference, each app lets the user pick a task, as the desktop [Task editor](desktop/task-editor.md#every-formatting-command-and-snippet-has-a-key) does, and [Tasks decides the link's address](tasks.md#a-time-ordered-id-names-each-task-its-file-and-links-to-it), `task:<id>`. The snippet writes the link around it.

Proposed, each a decision to confirm:

- A snippet of more than one line starts on a line of its own, with a blank line before it where Markdown needs one. For example, `---` right under a line of text would turn that text into a heading.
- The task reference's link text is the title's own Markdown, so `*urgent* milk` gives `[*urgent* milk](task:ID)`. Only `[`, `]`, and `\` get a backslash before them, so the title can't break the link.

### Typing a slash opens the snippet picker

Each snippet has a key, and a picker offers them all. Typing "/" at the start of a line or after a space opens a list of snippets, filtered as the user types. Enter inserts the chosen snippet in place of the "/" and the filter text, and Esc closes the list. The picker works only in the Description, and the same way on the phone, whose keyboard has no Ctrl keys.

Editing decides which snippets match the typed text; each app draws the list. Proposed, each a decision to confirm:

- A snippet matches when any word of its name starts with the typed text, ignoring case.
- When nothing matches, the list closes and the typed text stays.

## Public interface

Each app's Task editor uses these parts:

- **Formatting commands**: bold, italic, strikethrough, inline code, color, and headings, each from text and a selection to new text and a selection. A link is the Link snippet.
- **List commands**: Enter, Tab, and Shift+Tab in a list. Each returns null outside a list, so the key keeps its usual meaning.
- **The snippet catalog**: each snippet's name, whether it works in the Title, the input it needs, and its insertion.
- **The picker**: whether a typed "/" opens it, the snippets that match the typed text, and the insertion in place of the "/" and that text.

Each app fills a snippet's input from a table keyed by what the snippet needs, never a `switch`, so a new snippet adds one entry. The code below is the whole interface, in `Memoria.ToDo.Shared.Editing`, and every member is a pure function:

```csharp
namespace Memoria.ToDo.Shared.Editing;

/// A field's text and its selection: the input and the result of every command.
public sealed record TextAndSelection
{
    /// Throws ArgumentOutOfRangeException when the selection isn't inside the text.
    public TextAndSelection(string text, int selectionStart, int selectionLength);
    public string Text { get; }
    public int SelectionStart { get; }
    public int SelectionLength { get; }
}

public static class Formatting
{
    public static TextAndSelection Bold(TextAndSelection field);
    public static TextAndSelection Italic(TextAndSelection field);
    public static TextAndSelection Strikethrough(TextAndSelection field);
    public static TextAndSelection InlineCode(TextAndSelection field);

    /// Throws ArgumentException when color isn't one of paletteNames.
    public static TextAndSelection Color(TextAndSelection field, string color, IReadOnlyCollection<string> paletteNames);

    /// Throws ArgumentOutOfRangeException when level isn't 1, 2, or 3.
    public static TextAndSelection Heading(TextAndSelection field, int level);
}

/// Each returns null when the cursor isn't in a list.
public static class Lists
{
    public static TextAndSelection? Enter(TextAndSelection field);
    public static TextAndSelection? Indent(TextAndSelection field);
    public static TextAndSelection? Outdent(TextAndSelection field);
}

public enum SnippetNeeds { None, Time, ClipboardText, ImageMarkdown, TaskToReference }

/// A built-in snippet. Apps list Snippets.All and never switch on a kind.
public interface ISnippet
{
    string Name { get; }
    bool WorksInTitle { get; }
    SnippetNeeds Needs { get; }

    /// Throws ArgumentException when the input this snippet needs is missing.
    TextAndSelection Insert(TextAndSelection field, SnippetInput input);
}

/// What the app passes in: only the part the snippet needs.
public sealed record SnippetInput
{
    public DateTimeOffset? Now { get; init; }
    public string? ClipboardText { get; init; }
    public string? ImageMarkdown { get; init; }
    public TaskToReference? Task { get; init; }
}

/// The task a reference links to: its address from Tasks, and its title's own Markdown.
public sealed record TaskToReference(string Address, string Title);

/// The built-in snippets; All lists them in the picker's order.
public static class Snippets
{
    public static ISnippet DateStamp { get; }
    public static ISnippet DateAndTimeStamp { get; }
    public static ISnippet Note { get; }
    public static ISnippet Tip { get; }
    public static ISnippet Warning { get; }
    public static ISnippet Link { get; }
    public static ISnippet Image { get; }
    public static ISnippet CodeBlock { get; }
    public static ISnippet TaskReference { get; }
    public static ISnippet Table { get; }
    public static ISnippet HorizontalLine { get; }
    public static IReadOnlyList<ISnippet> All { get; }
}

public static class SnippetPicker
{
    /// True when the character at slashIndex is a "/" at a line start or after a space.
    public static bool OpensAt(string text, int slashIndex);

    /// The snippets matching the text between the "/" and the cursor.
    /// Throws ArgumentOutOfRangeException when slashIndex isn't a "/" before the cursor.
    public static IReadOnlyList<ISnippet> Matching(TextAndSelection field, int slashIndex);

    /// Inserts the snippet in place of the "/" and the text typed after it. Throws as Matching does.
    public static TextAndSelection Insert(TextAndSelection field, int slashIndex, ISnippet snippet, SnippetInput input);
}
```

## Failure modes

- **A selection outside the text** is a programming error: the command throws.
- **Missing input**, such as no time for a date stamp, is rejected before the text changes.
- **No URL on the clipboard** isn't a failure: the link waits for an address.

## Ruled out

The options below were considered and rejected, grouped by decision.

- Which snippets exist:
  - **Built-in snippets plus the user's own, synced through the repository**, or **only the user's own, seeded from the built-in set**: user snippets need a stored format, an editor for them, and sync; the built-in set covers the user's needs.
  - **A checklist item** and **a footnote**: offered as built-in snippets, and not chosen.
- How snippets are picked:
  - **Key shortcuts plus a Ctrl+Space picker**: the phone has no Ctrl key, so it would need another way.
  - **Key shortcuts only**: the same gap on the phone, and every snippet's key to remember.
- Where the palette names live:
  - **Editing reads the palette from Rendering**: Editing would depend on Rendering for a list of names; the app passes them in instead.
- How a snippet asks for its input:
  - **A typed input request on each snippet**, which the app answers: more types for the same five needs, and the app would still have to choose an answer for each kind.
