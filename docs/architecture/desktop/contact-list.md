# Contact list: the Contacts window

Status: Decided

The Contact list is the desktop window where the user browses, searches, edits, adds, and deletes contacts, and orders their groups. [Contacts](../contacts.md) decides the data: the files, the fields, search, saving, and the group order. This page decides only the window.

## Behavior

### The groups show on the left, and the selected contact's details on the right

The user decided the window shows the list on the left and the details on the right, split by the platform's [split panes](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), as Task View is.

- **The left side lists every group in [Contacts' order](../contacts.md#groups-keep-the-order-of-their-positions)**, each under its heading, which Contacts names, such as "Ex-Team". The user's own group shows like the others.
- **Each row shows the contact's full name, and its title after it in the help style.**
- **A click selects a contact, and the right side shows its details.**

Proposed, each a decision to confirm:

- Nothing is selected when the window opens, and the right side says to pick a contact.
- The window opens at Task View's size and remembers nothing between openings.
- An empty group still shows its heading, so the user can add to it.
- The list shows the new contacts at once when they change, such as after a sync, and keeps the selection when the contact is still there.

### Search filters the list

A search band at the window's top, the platform's [search band](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), filters the list as the user types, through [Contacts' search](../contacts.md#search-matches-every-typed-word-in-any-field), the same search the "@" list uses. The user decided it matches every typed word in any field, ignoring case.

Proposed, each a decision to confirm: a group with no match hides; matches keep their groups and order; with no match at all, the list says so.

### The details show every field and edit in place

The user decided that the form shows [team fields](../contacts.md#team-groups-show-more-fields) only for a contact in a team group, as Contacts' `ContactGroup.IsTeam` says. Contacts' field table lists which fields those are.

Proposed, each a decision to confirm:

- **The details show every known field, named in plain words**, such as "Full name" and "Reports to", and leave out missing ones while not editing. The heading is the full name, with the group's heading under it.
- **The details hide team fields outside team groups too, while not editing**, so the view and the form agree. A save keeps the hidden fields' values unchanged, since Contacts allows them in every group.
- **"Reports to" shows the manager's full name**, from Contacts, or the name as written when it finds nobody. **"Direct reports" lists the contacts who report to this one.** A click on either selects that contact.
- **A key field shows its entry**: the entry's name, or its ID when it has none, as [Contacts](../contacts.md#key-fields-choose-from-the-users-data-type-files) decides.
- **F2, a double click, or the Edit button turns the details into a form**, with every field the group shows. Save, on the platform's [Save keys](README.md#the-save-shortcut-saves-in-any-window-that-edits) or the Save button, saves through Contacts and leaves the form. Esc leaves without saving, asking Save, Discard, or Cancel when there are changes.
- **A date field is a date picker**, and the time field accepts the forms [Contacts reads](../contacts.md#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts), checked as it's typed.
- **A key field is a drop-down list of its data type's entries**, as the user decided, each shown as its name, or its ID when it has none. No typing in it is allowed. A stored key the file doesn't list shows first, as written, marked "Not in the list", and stays until the user picks another.
- **When the key field's data type file is missing or can't be read**, the field shows its stored key, read-only, and says under it which file is wrong. Saving keeps the key.
- **The ID can be typed only when adding.** It's read-only for an existing contact.
- **A save Contacts rejects keeps the form**, with the reason under the field it names, or at the top.

### Add and Delete change the contacts

The user decided the window offers Add and Delete, and no Move between groups. The user's own contact can't be deleted, and its group takes no new contact.

Proposed, each a decision to confirm:

- **Add is a button on each group's heading**, and on the right-click menu of the group's contacts. It opens an empty form for that group, with the cursor in the ID.
- **Delete is on a contact's right-click menu and the Delete key** on the selected contact, when no form is open. It asks first with the platform's [question dialog](README.md#later-designs-answer-what-these-files-leave-open): "Delete {full name}?", with Delete and Cancel, Cancel the default.
- **After a delete, nothing is selected.** Others who reported to the contact show the name as written, and mentions of it show their stored text, as [Contacts](../contacts.md#saving-adding-and-deleting-change-one-group-file) decides.

### Dragging a group's heading reorders the groups

The user decided the user can drag a group's heading to move the group, and the app rewrites the positions, as [Contacts](../contacts.md#groups-keep-the-order-of-their-positions) decides. The user's own group takes part like any other.

Proposed, each a decision to confirm: the drag works as Task View's [group drag](task-list.md#drag-and-drop-moves-tasks-and-groups-and-a-line-shows-where-they-land) does, through the platform's [drag parts](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them): the same distance before a drag starts, a heading and its rows counted as one item, a drop line, Esc to cancel, and scrolling near the edges. A drag is off while a search is typed or a form is open. A move that fails puts the group back and says why at the top.

### A global shortcut and the tray menu open Contacts

The user decided Contacts has a fourth global shortcut, **Win+Alt+O** by default, which the user changes in [Configure](configure.md#each-setting-has-one-owner) like the others. The tray menu has a Contacts item. Both show only while the clone has contact files, as [Shell](shell.md#three-global-shortcuts-and-the-tray-menu-open-the-apps-three-functions) decides, which also decides how the shortcut switches between windows.

Proposed, each a decision to confirm: the shortcut, with Contacts open, brings it to the front, empties its search, and puts the cursor there, as Task View's does. Closing the window with a changed form asks Save, Discard, or Cancel first.

### Problems show above the list

Proposed, a decision to confirm: when Contacts reports skipped files or shared names, a line above the list says how many, and clicking it lists each file with its reason and each shared name. Sync's status also lists the skipped files.

## Public interface

The Contact list may use only [Contacts](../contacts.md#public-interface), and the desktop platform. Shell shows and closes the window; the composition root creates it. Proposed, each a decision to confirm: these declarations. This is not implemented:

```csharp
namespace Memoria.ToDo.Desktop.ContactList;

public static class ContactsWindow
{
    /// Creates the Contacts window. It opens only on IContactsWindow.Show.
    public static IContactsWindow Create(IContacts contacts, ITheme theme, TimeProvider time,
        CloseWindowKeys closeWindowKeys, SaveKeys saveKeys);
}

/// The Contacts window, as Shell uses it.
public interface IContactsWindow
{
    /// Opens the window, or brings it forward, empties its search, and puts the cursor there.
    void Show();

    /// Closes the window as its close button does, first asking Save, Discard, or Cancel when a form
    /// has changes. True when the window is closed, or wasn't open; false when the user cancels,
    /// the save fails, or the prompt already shows.
    bool Close();

    /// Whether the window is open now.
    bool IsOpen { get; }

    /// Whether the clone has contact files, as Contacts' HasContactFiles says. Shell offers the
    /// tray item and the shortcut only while this is true.
    bool IsAvailable { get; }

    /// Raised on the UI thread when IsAvailable changes.
    event EventHandler? AvailableChanged;
}
```

The window uses the platform's [single window](README.md#a-closed-window-is-made-again-on-the-next-show), [Close window keys](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows), and Save keys, as Task View does. `TimeProvider` drives the drag's edge scrolling.

## Failure modes

- **No clone is selected**: the list is empty and says to choose the repository folder in Configure; Add, Delete, and the drag are off.
- **A save, delete, or move fails**: nothing changes on screen but a message saying why, under the form or at the top; the form keeps the typed values.
- **A sync changes the contact being edited**: the form keeps the user's values, and Save writes them over the synced ones. If the sync deleted it, Save adds it back. This is an accepted risk; the user sees the contact again and can delete it.
- **A contact file is skipped**: its contacts don't show, and the problems line says so.

## Ruled out

- **Details on the left or on top**: the user chose the list on the left, as in Task View.
- **A fixed group order, changed only in the files**: the user chose to drag headings.
- **Free text for key fields, or an editor for data types and templates**: the user chose drop-downs from hand-written files, with no editor for now.
- **Move between groups**: the user chose Add and Delete only.
- **An Undo bar after Delete**: the question first is simpler, and a delete is rare.
- **A fixed Contacts shortcut**: the user chose to change it in Configure like the others.
- **A split bar and search band of the window's own**: they'd copy Task View's; the user chose to move them into the platform.

## Open questions

None.
