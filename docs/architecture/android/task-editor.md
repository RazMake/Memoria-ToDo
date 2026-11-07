# Task editor: how the Android app adds a task in seconds

Status: Decided

The Task editor is the Android app's Add screen: full screen, with the keyboard up and the cursor in the Title, so typing starts at once. View Tasks edits a task with the same screen.

## Behavior

### Add opens full screen, with the keyboard up

Add fills the screen, shows the keyboard, and puts the cursor in the Title. It has the same three fields as the [desktop Add window](../desktop/task-editor.md#add-opens-at-once-with-the-cursor-in-the-title): Title, Description, and Tags. Title and Description use the standard Android text field that [How the apps fit together](../overview.md#each-app-edits-in-its-own-text-box-with-shared-commands) picks.

Proposed, each a decision to confirm:

- Add accepts typing within 1 second of a cold start, when the app isn't running, and within 300 milliseconds of a warm start, when it is. The [proof on a phone](README.md#a-proof-on-a-phone-comes-before-any-other-android-work) measured both, as below.
- The keyboard's Next key in the Title moves to the Description, as Enter does on the desktop.
- A Save button in the top app bar saves the task and closes Add. A task with an empty Title can't be saved, as on the desktop.

The proof measured Add on a Pixel 10 Pro, from when Android started to open it. A warm start adds about 25 ms before the app's own clock starts, so its slide-in figure is an estimate:

| Start | Runs | Keyboard starts to appear | Keyboard finishes sliding in |
| --- | ---: | ---: | ---: |
| Cold | 10 | 259 ms median, 371 ms at most | 574 ms median, 802 ms at most, in the 9 runs that recorded it |
| Warm | 2 | 144 and 157 ms | about 460 ms |

The cold start meets its target either way. The warm start meets its target only if typing counts from when the keyboard starts to appear. The slide-in, which Android runs, takes about 300 ms by itself. Proposed, a decision to confirm: Add accepts typing once the keyboard starts to appear with the cursor in the Title, and the slide-in doesn't count.

### A toolbar above the keyboard runs the common commands

A phone keyboard has no Ctrl key, so a toolbar sits just above the keyboard. The table lists its buttons and the [Editing](../editing.md) command each runs:

| Button | What it runs |
| --- | --- |
| Bold | Bold |
| Italic | Italic |
| List | The bulleted-list command, which [Editing proposes](../editing.md#lists-continue-as-you-type) |
| Color | Color, after the user picks from a list of the palette's colors |
| Image | The image snippet, with a picture the user picks |
| / | The [snippet picker](../editing.md#typing-a-slash-opens-the-snippet-picker), at the cursor |

Proposed, each a decision to confirm:

- Editing's other commands, such as strikethrough and headings, have no button: the user types their Markdown or picks a snippet.
- A button that doesn't work in the focused field is turned off. In the Title, that's List, Image, and "/", since Editing runs them only in the Description.
- Images come only from Android's Photo Picker, which needs no storage permission and falls back to the system's file picker on phones without it ([Photo Picker](https://developer.android.com/training/data-storage/shared/photopicker)). A pasted image isn't inserted.

Pictures are shrunk with Android's own image tools, as [Tasks](../tasks.md#images-are-at-most-1-mb-and-live-beside-their-task) decides.

### Tags sit as chips under the Title

The five most-used tags show as chips under the Title, and tapping one adds it. They're counted as on the [desktop](../desktop/task-editor.md#tags-come-from-the-tags-already-in-use).

Proposed, each a decision to confirm:

- A chosen tag shows as a checked chip, and tapping it again removes it.
- A "+" chip opens a field that suggests the tags in use, for a tag that isn't among the five.

### Back keeps a draft on this phone

Proposed: drafts work as on the [desktop](../desktop/task-editor.md#closing-without-saving-keeps-a-draft-on-this-device). Back, or leaving the app, keeps Add's text as a draft, the next Add opens with it, and a Discard button in the top app bar clears it. The draft is saved in the app's private storage, outside the clone, and never syncs. This is a decision to confirm.

### Edit opens the same screen for a task in View Tasks

An expanded task's Edit button in [View Tasks](task-list.md#swipes-resolve-and-delete-and-a-long-press-moves-a-task) opens this screen with the task. Saving works as in Add.

Proposed: Back discards the change, and View Tasks shows an Undo bar, as Esc does on the [desktop](../desktop/task-list.md#edit-turns-the-open-task-into-the-editor-in-place). This is a decision to confirm.

### The editor styles Markdown in place, with no preview

As on the [desktop](../desktop/task-editor.md#the-editor-styles-markdown-in-place-with-no-preview), the fields show Markdown source styled in place, from [Rendering's styled spans](../rendering.md#titles-and-the-editor-get-styled-spans-not-html). Add has no preview and no web view, so it never waits for one. A saved new task goes to the top of the order.

## Public interface

Shell and the Task list use these parts:

- **Show Add**: opens Add, with the draft if there is one. Shell calls it from the app shortcut and the widget, and the Task list from its floating button.
- **Edit a task**: opens this screen with one task, and reports whether the user saved or left. The Task list calls it from Edit.
- **The tag chips**: the five most-used tags as chips, reporting the chosen tag. The Task list uses them to filter.

## Failure modes

- **Saving fails, an image can't be saved, or the draft can't be read**: Add handles it as the [desktop does](../desktop/task-editor.md#failure-modes).
- **The user leaves the Photo Picker without a picture**: nothing is inserted, with no message.

## Ruled out

The options below were considered and rejected.

- **An expanding bottom sheet**, and **full screen with "/" only, and no toolbar**: offered, and not chosen.
