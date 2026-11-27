# Contact list: the Contacts window

Status: Decided

The Contact list is the desktop window where the user browses, searches, edits, adds, and deletes contacts, and orders their groups. [Contacts](../contacts.md) decides the data: the files, the fields, search, saving, and the group order. This page decides only the window.

## Behavior

### The groups show on the left, and the selected contact's details on the right

The user decided the window shows the list on the left and the details on the right, split by the platform's [split panes](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), as Task View is.

- **The left side lists every group in [Contacts' order](../contacts.md#groups-keep-the-order-of-their-positions)**, each under its heading, which Contacts names, such as "Ex-Team". The user's own group shows like the others.
- **Each row shows the contact's full name, then its alias in italics between brackets when it has one**, as "Diego Carretero Alvarez (*diegocarr*)", the user's choice; the title shows only in the details.
- **A click selects a contact, and the right side shows its details.**

Proposed, each a decision to confirm:

- Nothing is selected when the window opens, and the right side says "Pick a contact to see the details."
- The window opens at Task View's size and remembers nothing between openings.
- An empty group still shows its heading, so the user can add to it.
- The list shows the new contacts at once when they change, such as after a sync, and keeps the selection when the contact is still there.
- **The window's title is "Contacts".** Its size is Task View's, 1100 by 720 with a minimum width of 680, as the window's own numbers, since the two windows' sizes change for different reasons.
- **The list takes 40 parts of the width and the details 60**, at least 260 and 340 pixels wide, since a contact's form has more fields than a task's details.
- **The top of the window holds, in order: the search band, the [message line](#messages-and-problems-show-at-the-top), and the problems line.** They span the window above both sides, as Task View's header does. The window has no status bar.
- **Selecting moves with the keyboard too**: Down and Up in the search box select the first or last contact shown, through the search band's edge-row event, and Down and Up in the list move the selection. Down on the last contact shown, or Up on the first, goes back to the search box with its text selected, so typing replaces it. The user decided these.
- **Tab stops once in each group's rows**, at its selected contact or else its first, so Tab goes from a group's rows to the next group's Add, then its rows. Landing on a contact selects it, so the focus always shows as the selection, and a row shows no separate focus frame. The user decided this. When selecting would leave a changed form and the user cancels, a click leaves the focus where it was, and Tab moves it to the contact still selected, so the question isn't asked twice.
- **When the clone has no contact files, or no clone is selected**, the list says "There are no contact files in this repository.", and Add, Delete, Edit, and the drag are off. `IContacts.HasContactFiles` tells the two cases apart from a clone whose groups are all skipped, which still shows its problems line.

### Search filters the list

A search band at the window's top, the platform's [search band](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them), filters the list as the user types, through [Contacts' search](../contacts.md#search-matches-every-typed-word-in-any-field), the same search the "@" list uses. The user decided it matches every typed word in any field, ignoring case.

Proposed, each a decision to confirm: a group with no match hides; matches keep their groups and order; with no match at all, the list says "No contact matches the search." An empty search shows every group, empty ones included. The selection stays when the selected contact still matches, and clears when it doesn't. Two files can hold contacts with the same ID, which Contacts reports as a [shared name](../contacts.md#a-name-two-contacts-share-points-to-nobody), so the list shows each matching contact in its own group, and hides one that doesn't match even when it shares an ID with one that does.

### The details show every field and edit in place

The user decided that the form shows [team fields](../contacts.md#team-groups-show-more-fields) only for a contact in a team group, as Contacts' `ContactGroup.IsTeam` says. Contacts' field table lists which fields those are.

Proposed, each a decision to confirm:

- **The details show every known field, named in plain words**, such as "Full name" and "Reports to", and leave out missing ones while not editing. The heading is the full name, with the group's heading under it. Each field is one row: its label, then its value on a subtle background a few pixels larger than the text, the user's choice, with corners rounded by 4 pixels, as mentions have; a field with several values, such as "Direct reports", stacks them in its row.
- **The details hide team fields outside team groups too, while not editing**, so the view and the form agree. A save keeps the hidden fields' values unchanged, since Contacts allows them in every group.
- **"Reports to" shows the manager's full name**, from Contacts, or the name as written when it finds nobody. **"Direct reports" lists the contacts who report to this one.** A click on either selects that contact.
- **A key field shows its entry**: the entry's name, or its ID when it has none, as [Contacts](../contacts.md#key-fields-choose-from-the-users-data-type-files) decides.
- **F2, a double click, or the Edit button turns the details into a form**, with every field the group shows. Save, on the platform's [Save keys](README.md#the-save-shortcut-saves-in-any-window-that-edits) or the Save button, saves through Contacts and leaves the form. Esc leaves without saving, asking Save, Discard, or Cancel when there are changes.
- **A date field is a date picker**, and the time field accepts the forms [Contacts reads](../contacts.md#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts), checked as it's typed.
- **A key field is a drop-down list of only its data type's entries**, as the user decided, each shown as its name, or its ID when it has none. No typing in it is allowed, and it has no empty choice, so the user can't set a key the file doesn't list or clear a listed one. A stored key that's missing or that the file doesn't list shows the drop-down empty, as the user decided, and stays stored until the user picks an entry.
- **When the key field's data type file is missing or can't be read**, the field shows its stored key, read-only, and says under it which file is wrong. Saving keeps the key.
- **The ID can be typed only when adding.** It's read-only for an existing contact.
- **A save Contacts rejects keeps the form**, with the reason under the field it names, or at the top.
- **The form checks only what its own fields can tell**: the ID and full name aren't empty, the time reads, and, when adding, no loaded contact holds the ID, as Contacts compares names. The user decided this last check, since Contacts' Save replaces a contact of the same group with that ID; under the ID it says "Another contact has this ID." Each problem shows under its field, and Save does nothing until they're fixed. The time's message is "Write the time as HH:mm, such as 09:30." Every other rule is Contacts': its rejection shows on the message line at the top, as Contacts words it, since its failures don't name a field.
- **The time field reads and shows the time through Contacts' `ContactTime`**, the user's choice, so the forms the field accepts are the forms Contacts reads, with one home. `ContactTime` sits in [Contacts' public interface](../contacts.md#public-interface).
- **A date shows in the details in the user's short date format**, as the date picker shows it, since a date's look on screen isn't the file's format.
- **An empty field saves as missing**, as Contacts treats empty values.
- **While a form is open, selecting another contact or Add asks first** when the form has changes, with Save, Discard, and Cancel, as Esc does; with no changes the form just leaves. F2 and Delete do nothing while a form is open, since the form is already open and Delete is offered only without one. The drag is off, as [below](#dragging-a-groups-heading-reorders-the-groups) says.
- **After a save, the form leaves, and the saved contact is selected**, also a new one.

Proposed for building, each a decision to confirm:

- **The fields come from [Contacts' `ContactFields`](../contacts.md#public-interface)**, the user's choice: their order, kinds, and which are team fields. The window keeps only their labels.
- **The fields' names** are ID, Alias, Full name, First name, Nickname, Title, Location, Level, Working type, Pronouns, Career path, Reports to, Direct reports, Day start time, Level start date, Team member since, and Exit date. The details and the form show them in the order of [Contacts' field table](../contacts.md#the-clone-keeps-contacts-in-group-files-under-autocompletecontacts), with Direct reports after Reports to; the form leaves out Direct reports, which isn't a stored field.
- **An empty ID says "Write an ID."** and an empty full name "Write the full name.", under the field.
- **An unlisted stored key is kept, not cleared, while the drop-down shows empty**, so opening and saving a form never changes a key the user didn't touch. A key the user picked that a reload of its file no longer lists also shows empty; Contacts then rejects the save, and the message line says why.
- **Clicking "Reports to" or a direct report that the search hides empties the search first**, so the selected contact shows in the list. With a changed form open, it asks first, and empties the search only once the form leaves; Cancel keeps both.
- **Nickname shows and edits only what the file holds**, as a plain text field. Proposed, a decision to confirm: a contact with no nickname shows no Nickname row, like any missing field, though templates [show its first name](../contacts.md#a-nickname-falls-back-to-the-first-name).

### A contact card shows a mentioned contact's details

The user asked that resting the mouse pointer on a mention, anywhere one shows, open a card with the contact's details: the common ones by default, and every one, including a team member's extra fields, while Ctrl is held. The platform's [hover card](README.md#a-hover-card-shows-while-the-pointer-rests-on-a-part) decides when it opens, where, and when it closes. The user decided:

- **The Contact list publishes the card**, since the details' labels and the way each value shows already live here, and the [Task editor](task-editor.md#resting-the-pointer-on-a-chip-shows-the-contacts-card) and the [Task list](task-list.md#resting-the-pointer-on-a-mention-shows-the-contacts-card) show it.
- **The card is for the contact the mention's address names**, as Contacts' [`ContactOf`](../contacts.md#public-interface) finds it, the way it finds it for the mention's live text: [by ID only](../contacts.md#a-mentions-text-is-rebuilt-from-the-contact-each-time-it-shows), and nobody when no contact or two contacts hold the ID. With nobody, there's no card, since the mention already shows its stored text.
- **By default it shows the common fields**: Title, Alias, Pronouns, Career path, and Reports to, the same for every group.

Proposed, each a decision to confirm:

- **Its heading is the full name**, with the group's heading under it, as the details show them.
- **While Ctrl is held, it shows every field the details show**, in the details' order: the common fields, plus ID, First name, Nickname, and Direct reports, and, for a contact in a [team group](../contacts.md#team-groups-show-more-fields), Location, Level, Working type, Day start time, Level start date, Team member since, and Exit date.
- **Each field shows on one line, as in the details**: its label in the help style, then its values beside it, each on a background just a little different from the card's.
- **Each value shows as in the details**: a key field as its entry's name, a date in the user's short date format, "Reports to" as the manager's full name or the name as written, and Direct reports as a list of full names. The card and the details share one way of turning a field into its label and text, so the two can't differ.
- **A field with no value doesn't show**, in either view. A contact with none of the common fields shows only its heading.
- **The card shows no line about Ctrl**, the user decided; holding Ctrl still shows every field.
- **Pressing Ctrl while the card shows switches it to every field at once, and releasing Ctrl switches it back**, without closing it; the card grows or shrinks in place, up to the [hover card's height limit](README.md#a-tall-card-scrolls-and-the-pointer-can-reach-it), past which it scrolls. Ctrl already held when the card opens opens it with every field. Ctrl is the only key that changes the card; the other modifiers do nothing to it.
- **Nothing on the card acts on a click or takes the focus**, so "Reports to" and Direct reports aren't links there, unlike in the details. The pointer can still move onto the card, and the wheel scrolls it, as the platform's [hover card](README.md#a-tall-card-scrolls-and-the-pointer-can-reach-it) decides; the card sets no height limit of its own.
- **It shows the contact as it was when the card opened**; a change to contacts while it shows reaches the next card.

The user decided how the card uses the theme:

- **The heading, the contact's full name, shows in the theme's heading color**: [Rendering's text look](../rendering.md#public-interface) for the light or dark theme, at heading level 1, the color of a description's top heading. Only its color changes; its size stays the details'.
- **The heading's color follows a switch between light and dark while the card shows**, through the platform's [theme follower](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it).
- **The card's surface stands out from the window behind it**, the user decided: the Windows theme's popup background, a shade lighter than a window's in both light and dark, with the theme's popup outline and rounded corners. Its text keeps the theme's colors.

The user decided the Contact list may use Rendering, as the [module map](../modules.md#module-map) says, so the heading color keeps its one home in Rendering's text look. Rendering uses no module, so no loop forms. The user also decided only the card shows the name in the heading color; the Contacts window's details pane keeps the normal text color.

### Add and Delete change the contacts

The user decided the window offers Add and Delete, and no Move between groups. The user's own contact can't be deleted, and its group takes no new contact.

Proposed, each a decision to confirm:

- **Add is a button on each group's heading**, and on the right-click menu of the group's contacts. It opens an empty form for that group, with the cursor in the ID.
- **Delete is on a contact's right-click menu and the Delete key** on the selected contact, when no form is open. It asks first with the platform's [question dialog](README.md#later-designs-answer-what-these-files-leave-open): "Delete {full name}?", with Delete and Cancel, Cancel the default.
- **After a delete, nothing is selected.** Others who reported to the contact show the name as written, and mentions of it show their stored text, as [Contacts](../contacts.md#saving-adding-and-deleting-change-one-group-file) decides.
- **The user's own group shows no Add button, and its contact has no Delete**, on the menu or the Delete key, so the window never asks Contacts for what it rejects.
- **A failed delete keeps the contact**, selected, and the message line says why.

### Dragging a group's heading reorders the groups

The user decided the user can drag a group's heading to move the group, and the app rewrites the positions, as [Contacts](../contacts.md#groups-keep-the-order-of-their-positions) decides. The user's own group takes part like any other.

Proposed, each a decision to confirm: the drag works as Task View's [group drag](task-list.md#drag-and-drop-moves-tasks-and-groups-and-a-line-shows-where-they-land) does, through the platform's [drag parts](README.md#split-panes-search-band-and-drag-parts-serve-every-window-that-needs-them): the same distance before a drag starts, a heading and its rows counted as one item, a drop line, Esc to cancel, and scrolling near the edges. A drag is off while a search is typed or a form is open. A move that fails puts the group back and says why at the top.

### A global shortcut and the tray menu open Contacts

The user decided Contacts has a fourth global shortcut, **Win+Alt+O** by default, which the user changes in [Configure](configure.md#each-setting-has-one-owner) like the others. The tray menu has a Contacts item. Both show only while the clone has contact files, as [Shell](shell.md#four-global-shortcuts-and-the-tray-menu-open-the-apps-four-functions) decides, which also decides how the shortcut switches between windows.

Proposed, each a decision to confirm: the shortcut, with Contacts open, brings it to the front, empties its search, and puts the cursor there, as Task View's does. Closing the window with a changed form asks Save, Discard, or Cancel first.

Proposed for building, each a decision to confirm:

- **A new window opens with an empty search and the cursor in it.** Show on an open window empties the search by setting the band's text, so the list filters again through the one path, and puts the cursor there; a form that's open stays open, with its typed values.
- **Close works as the close button does**: with a changed form it asks Save, Discard, or Cancel, and returns false when the user cancels, the save fails, or the question already shows. When Windows ends the session or the app exits, the window closes without asking, as [Task View](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) does: only the user's own close command asks.
- **Esc with no form open closes the window**, through the platform's Esc rule, as Task View does.
- **`IsAvailable` follows `IContacts.HasContactFiles`.** The window reads it when asked, and raises `AvailableChanged` on the UI thread only when the answer differs from the last one it reported, starting from the answer at creation. It follows `IContacts.Changed` from creation, also while the window is closed, and moves each notice to the UI thread, since Contacts raises it on the thread of the call or of the repository's reload.

### Messages and problems show at the top

The user left open whether Contacts' message line at the top shares the Task list's look. Proposed, a decision to confirm: **it does, through the platform's [message line](README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them)**, which Task View moves onto with no change users notice. A window's failure looks the same in both.

- **The message line shows why the last action failed**: a save, delete, or move that Contacts rejected, or that a file error stopped. It also shows a change Contacts saved before a notice failed, as [Failure modes](#failure-modes) says. The next action that succeeds, or the next one the user starts, clears it.
- **Known failures get the platform's message**: no clone, a file error, and a rejection Contacts words. Contacts' `InvalidOperationException` for a group file skipped since the window showed it gets "This group's file was skipped. See the problems line." Any other failure isn't caught, so the app's unhandled-error path shows it.

Proposed, a decision to confirm: when Contacts reports skipped files or shared names, a line above the list says how many, such as "2 contact files skipped, 1 name shared by more than one contact", and clicking it lists each file with its reason and each shared name. Sync's status also lists the skipped files.

Proposed for building, each a decision to confirm:

- **Clicking the problems line opens or closes the list under it**, in the window, with no dialog. Each skipped file shows its path relative to the clone and its reason, and each shared name shows as "Shared by more than one contact: {name}". A file that needs a newer app says so, as its reason does.
- **The line hides when there's nothing to report**, and follows each change.
- **It's written in the help style**, not the error color, since a skipped file isn't the last action's failure.
- **It names only the kinds there are, singular for one**: "1 contact file skipped", "2 names shared by more than one contact", or both joined by a comma.
- **When every group file is skipped**, the list says "No contact file could be read. See the problems line."

## Public interface

The Contact list may use only [Contacts](../contacts.md#public-interface), and the desktop platform. Shell shows and closes the window; the composition root creates it. Proposed, each a decision to confirm: these declarations, built in step 15's slice (e) as the project `Memoria.ToDo.Desktop.ContactList` in [`src/Desktop/ContactList`](../../../src/Desktop/ContactList/ContactsWindow.cs), its tests in the desktop tests project under `ContactList`:

```csharp
namespace Memoria.ToDo.Desktop.ContactList;

public static class ContactsWindow
{
    /// Creates the Contacts window. It opens only on IContactsWindow.Show.
    public static IContactsWindow Create(IContacts contacts, ITheme theme, TimeProvider time,
        CloseWindowKeys closeWindowKeys, SaveKeys saveKeys, IDragPointer pointer);
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

Proposed, each a decision to confirm: `Create` throws `ArgumentNullException` for a null argument, before it makes anything, and makes no window until Show. `IsAvailable` and `AvailableChanged` work from creation, with the window never opened. The Contacts window's [shared fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test), for Shell's tests, lives in the desktop fakes project beside Task View's.

For [the contact card](#a-contact-card-shows-a-mentioned-contacts-details), the user decided that the Contact list also publishes the card, which the Task editor and the Task list may use, as the [module map](../modules.md#module-map) says, and that it finds the contact through Contacts' new [`ContactOf`](../contacts.md#public-interface). Proposed, a decision to confirm: the card's shape below.

```csharp
namespace Memoria.ToDo.Desktop.ContactList;

/// A mentioned contact's details, to show while the pointer rests on the mention.
public static class ContactCard
{
    /// The card for the contact address names, as Contacts' ContactOf finds it, or null when it finds nobody.
    /// The card shows the common fields, and every field while Ctrl is held, switching as Ctrl goes down and up;
    /// it shows the contact as it is now, its heading in theme's heading color, following theme switches,
    /// and nothing on it acts on a click or takes the focus.
    /// Throws ArgumentNullException for a null argument. Use it on the UI thread.
    public static System.Windows.FrameworkElement? For(IContacts contacts, ITheme theme, string address);
}
```

## Failure modes

- **No clone is selected, or the clone has no contact files**: the list is empty and says there are no contact files; Add, Delete, Edit, and the drag are off. Shell offers the window only while there are contact files, so this shows only in a window left open.
- **A save, delete, or move fails**: nothing changes on screen but a message saying why, under the form or at the top; the form keeps the typed values.
- **Contacts saved the change, and then a notice or the local save report failed** (the platform's [`NoticeFailedException`](../modules.md#steps-that-must-all-run-throw-their-failures-together)), even only one failure: the window does what it does after that save, delete, or move succeeds, so a form closes as on success. The message line at the top shows the platform's [message for it](README.md#the-message-line-and-store-failures-serve-every-window-that-shows-them), "Saved, but the app couldn't finish updating: " and the first failure's message, and the window logs the failure once, at Error. It never says the change failed. Proposed, a decision to confirm: the list asks Contacts for its groups again, since the failed notice may be the one that rebuilds it. When a group move was only partly written and a notice failed too, Contacts throws several failures at once, and the message says why, as for any other failure.
- **A sync changes the contact being edited**: the form keeps the user's values, and Save writes them over the synced ones. If the sync deleted it, Save adds it back. This is an accepted risk; the user sees the contact again and can delete it.
- **The user selects another repository in Configure while a form is open**, which the tray menu allows: Save writes the form into the newly selected clone. The user accepted this risk, as for Task View's edits: it's rare, and the user sees the result.
- **A contact file is skipped**: its contacts don't show, and the problems line says so.

## Ruled out

- **Details on the left or on top**: the user chose the list on the left, as in Task View.
- **A fixed group order, changed only in the files**: the user chose to drag headings.
- **Free text for key fields, or an editor for data types and templates**: the user chose drop-downs from hand-written files, with no editor for now.
- **Move between groups**: the user chose Add and Delete only.
- **An Undo bar after Delete**: the question first is simpler, and a delete is rare.
- **A fixed Contacts shortcut**: the user chose to change it in Configure like the others.
- **A split bar and search band of the window's own**: they'd copy Task View's; the user chose to move them into the platform.
- **A message line of the window's own, in the error color**: it would copy Task View's look, which would then change in two places.
- **The window's own rule for reading a time**: the forms would live in two places, which the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) forbids.
- **A time picker of hour and minute lists**: a slower pick, and the field would differ from how the file reads.
- **A dialog that lists the problems**: the inline list needs no new platform part, and stays beside the list while the user fixes the files.
- Where the contact card lives:
  - **A new desktop module, Contact card, that the Contact list, the Task editor, and the Task list use**: a ninth module, project, and test folder for one small control, and the details' labels would move out of the Contact list.
  - **Field labels and value text move into Contacts, in the shared library, and the Task editor draws the card**: Contacts [stays out of the screens](../contacts.md#contacts-stays-out-of-the-screens), and the Task list would still reach the card only through the Task editor.
- Where the contact card's labels and values come from:
  - **A card of the Task editor's own, with its own labels**: the labels and value rules would live in two places, which the [no-duplication rule](../../../AGENTS.md#core-rule-no-duplication) forbids.
- Which fields the card shows without Ctrl:
  - **Title and Alias only**: Ctrl would be needed for nearly everything.
  - **The common fields, plus Location and Working type for a team member**: the card's size would differ by group even without Ctrl.
- What a mention whose contact Contacts can't find shows on hover:
  - **A card that says "This contact isn't in your contacts." under the stored text**: a card with no details, for what the stored text already shows.
- How the card shows every field (proposed, a decision to confirm):
  - **A "More" button on the card**: the card would need the focus or a click, and the user asked for Ctrl.
- What color the card's heading shows in:
  - **The normal text color, as in the details**: the card would take the theme only to pass it on, and the name would stand out less.
- How the card reaches the heading color:
  - **`ContactCard.For` takes the color from its caller**: a public interface change, and the Task editor and the Task list would both pick the card's color, two places for one rule.
  - **The desktop platform gives the heading color**: the platform would copy Rendering's colors.
- **The details pane's name in the heading color too**: a visible change to the Contacts window nobody asked for.
- **Showing the first name in place of a missing nickname, in the details or as a hint in the form**: the details would show a value the file doesn't hold, and the card would need the same rule, or the form would need a hint style it doesn't have.
- **Nickname among the card's common fields, shown without Ctrl**: the default card the user chose would grow by a row.
