# Editing: the formatting, snippet, and list commands

Status: Decided

Editing is the shared module that changes task text when the user formats it, inserts a snippet, or continues a list. Both apps use the same commands, so a command works the same on the desktop and the phone; each app decides which key or touch runs it.

## Behavior

### Commands take text and a selection, and return new text and a selection

Proposed: every command is a pure text operation. It takes the field's text and the selection, and returns the new text and the new selection. This is a decision to confirm.

- **No command reads a device.** Whatever a command needs from outside, such as the current time, a URL on the clipboard, a saved image's path, or a task's link, the app passes in. That keeps the commands fast and testable, and the same on both apps.
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

Proposed, each a decision to confirm:

- With no selection, a command inserts both markers with the cursor between them.
- On text that already has the markers, the command removes them. The same heading level removes the heading, and another level replaces it.
- Choosing a text's own color again removes the color, and another color replaces it: blue turns `:red[urgent]` into `:blue[urgent]`.
- The link command and the link snippet are one command. A URL on the clipboard becomes the address; otherwise the cursor waits in the empty address.

### Lists continue as you type

In the Description, Enter continues a bulleted or numbered list, and Enter on an empty item ends the list. Tab and Shift+Tab indent and outdent list items. Proposed: a continued numbered list counts on from the item above. This is a decision to confirm.

Proposed: a bulleted-list command adds `- ` at the start of each selected line in the Description, or removes it when every selected line has it. The Android toolbar's list button runs it, as the Android [Task editor](android/task-editor.md#a-toolbar-above-the-keyboard-runs-the-common-commands) decides. This is a decision to confirm.

### Snippets insert common blocks

The apps have a built-in set of snippets only. The table lists each snippet, what it inserts, what the app passes in, and whether it works in the Title:

| Snippet | Inserts | The app passes in | Title too |
| --- | --- | --- | --- |
| Date stamp | The local date, `yyyy-MM-dd` | The current time | Yes |
| Date and time stamp | The local date and time, `yyyy-MM-dd HH:mm` | The current time | Yes |
| Note, tip, or warning | An info block, such as `> [!NOTE]` | Nothing | No |
| Link | `[text](address)`, filled from a URL on the clipboard | The clipboard's text | Yes |
| Image | An image, from the clipboard or a file | The saved image's path, from Tasks | No |
| Code block | A fenced code block | Nothing | No |
| Reference to another task | A link that opens the task, with its title as the text | The task's link and title, from Tasks | Yes |
| Table | A table with 2 columns | Nothing | No |
| Horizontal line | `---` | Nothing | No |

For the task reference, the app lets the user pick a task by title, and Tasks decides the link's format, as [the desktop app page](desktop/README.md#later-designs-answer-what-these-files-leave-open) lists.

Proposed: a snippet of more than one line starts on a line of its own, with a blank line before it where Markdown needs one. For example, `---` right under a line of text would turn that text into a heading. This is a decision to confirm.

### Typing a slash opens the snippet picker

Each snippet has a key, and a picker offers them all. Typing "/" at the start of a line or after a space opens a list of snippets, filtered as the user types. Enter inserts the chosen snippet in place of the "/" and the filter text, and Esc closes the list. The picker works only in the Description, and the same way on the phone, whose keyboard has no Ctrl keys.

Editing decides which snippets match the typed text; each app draws the list. Proposed, each a decision to confirm:

- A snippet matches when any word of its name starts with the typed text, ignoring case.
- When nothing matches, the list closes and the typed text stays.

## Public interface

Each app's Task editor uses these parts:

- **Formatting commands**: bold, italic, strikethrough, inline code, color, link, and headings, each from text and a selection to new text and a selection.
- **List commands**: Enter, Tab, and Shift+Tab in a list. Each says whether it applied, so outside a list the key keeps its usual meaning. Proposed: also the bulleted-list command, from text and a selection to new text and a selection.
- **The snippet catalog**: each snippet's name, whether it works in the Title, the input it needs, and its insertion.
- **The picker**: whether a typed "/" opens it, and the snippets that match the typed text.

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
