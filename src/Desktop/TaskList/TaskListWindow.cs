using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The Task View window: the open tasks grouped by tag on the left, the selected task's description and tags on the right,
/// above them the search, the tag filter, and the resolved tasks, and below them the status bar. It works on the tasks through <see cref="ITasks"/> and
/// shows what they say.
/// </summary>
internal sealed class TaskListWindow : Window
{
    private const string GoneText = "This task no longer exists.";

    private readonly ITasks _tasks;
    private readonly ITagColors _tagColors;
    private readonly ITheme _theme;
    private readonly IDefaultBrowser _browser;
    private readonly Action<string> _carry;
    private readonly FormattedDescription _description;
    private readonly DetailsPane _details;
    private readonly GroupedRows _rows;
    private readonly GroupCollapse _collapse = new();
    private readonly ListFilter _filter = new();
    private readonly ListHeader _header;
    private readonly ListSync _sync;
    private readonly UpdateStatusBar _statusBar;
    private readonly PendingUndo _pending;
    private readonly ListContent _content;
    private readonly ListLinks _links;
    private readonly ListActions _actions;
    private readonly TaskDeletion _deletion;
    private readonly EditCleanup _cleanup;
    private readonly TagChanges _tagChanges;
    private readonly SaveKeys _saveKeys;
    private readonly ListSelection _selection = new();
    private readonly ListEditing _editing;
    private readonly CloseDecision _decision;
    private readonly HeldModifierKeys _held = new();
    private readonly Dictionary<RowAction, Action<TaskItem, TaskRow?>> _rowActions;
    private ListSnapshot _snapshot = new([], [], false);
    private TaskItem? _selected;
    private TagEditor? _tagEditor;
    private TagBadges? _editBadges;
    private RowPosition? _titleAt;
    private string? _detailsId;
    private bool _previewing;
    private bool _closing;
    private bool _closed;

    public TaskListWindow(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, IDescriptionWebView webView,
        IDefaultBrowser browser, ListSync sync, IUpdateStatus updates, SaveKeys saveKeys, string? message, Action<string> carry)
    {
        _sync = sync;
        _statusBar = new UpdateStatusBar(updates, new ListStatusStart(sync));
        _saveKeys = saveKeys;
        _tagColors = tagColors;
        _decision = new CloseDecision(new CloseDialog(this));
        _tasks = tasks;
        _theme = theme;
        _browser = browser;
        _carry = carry;
        _rowActions = new()
        {
            [RowAction.Toggle] = (task, _) => SelectTask(task.Id),
            [RowAction.Edit] = BeginEditOf,
            [RowAction.EditTitle] = BeginTitleEditOf,
            [RowAction.Resolve] = (task, row) => Resolve(task, row, task.Resolved is null),
            [RowAction.Delete] = (task, _) => Delete(task),
            [RowAction.MoveUp] = (_, row) => Nudge(row, -1),
            [RowAction.MoveDown] = (_, row) => Nudge(row, 1),
        };

        Title = "Task View";
        Width = 1100;
        Height = 720;
        MinWidth = 680;

        _description = new FormattedDescription(webView, theme, browser);
        _description.OtherLinkClicked += (_, address) => FollowLink(address);
        _description.OpenFailed += (_, failure) => Report(failure.Message);
        _description.ImageResized += (_, resize) => ResizeImage(resize);
        _details = new DetailsPane(_description);
        _details.TabChosen += (_, preview) => SwitchTab(preview);

        _header = new ListHeader(_filter);
        _content = new ListContent(tasks, _filter);
        _links = new ListLinks(tasks, _filter, _content);
        _actions = new ListActions(tasks, Report);
        _pending = new PendingUndo(time, post => Dispatcher.BeginInvoke(post), () => _header.ShowUndo(_pending!.IsPending ? _pending.Label : null));
        _deletion = new TaskDeletion(tasks, _content, _pending, Report, Refresh);
        _cleanup = new EditCleanup(tasks, _pending, Report);
        _tagChanges = new TagChanges(tasks, _pending, _cleanup, Report);
        _editing = new ListEditing(tasks, palette, theme, time, clipboard, imageFiles, files, saveKeys, _cleanup, RestoreEdit);
        _rows = new GroupedRows(tagColors, theme, CreateRow);
        _header.ShowMessage(message);
        _filter.Changed += (_, _) => Refresh();
        _header.UndoClicked += (_, _) => _pending.Undo();
        _rows.ResolvedClicked += (_, _) =>
        {
            _content.ShowResolved = !_content.ShowResolved;
            Refresh();
        };
        _rows.GroupToggled += (_, tag) => ToggleGroup(tag);

        Content = Layout();
        EscCloses.Attach(this);
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, e) => _held.Released(EventKey.Of(e));
        Deactivated += (_, _) => _held.Clear();
        tasks.Changed += OnChanged;
        Refresh();
    }

    /// <summary>
    /// Closes the window as its close button does: a changed edit asks first. False, with the window open, when the
    /// user cancels, the save fails, or the prompt already shows.
    /// </summary>
    public bool TryClose()
    {
        var mayClose = _decision.MayClose(ClosingEdit());

        // The application ending can close the window while the prompt shows; it is closed then, whatever the answer.
        if (_closed)
        {
            return true;
        }

        if (mayClose)
        {
            Close();
        }

        return mayClose;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        CloseCommandHook.Attach(this, () => TryClose());
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;
        _closing = true;
        _tasks.Changed -= OnChanged;
        try
        {
            _pending.Commit();
            if (_editing.Forget() is { } edit)
            {
                edit.Close(_cleanup);
            }
        }
        finally
        {
            _sync.Dispose();
            _pending.Dispose();
            _description.Dispose();
        }
    }

    // The header over the two sides, which scroll on their own and share a bar that moves the split.
    private Grid Layout()
    {
        var left = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Content = _rows,
        };
        KeyboardNavigation.SetIsTabStop(left, false);
        var splitter = new GridSplitter
        {
            Width = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Focusable = false,
            IsTabStop = false,
        };
        splitter.SetResourceReference(BackgroundProperty, "CardStrokeColorDefaultBrush");
        var sides = new Grid();
        sides.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
        sides.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sides.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 340 });
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(_details, 2);
        sides.Children.Add(left);
        sides.Children.Add(splitter);
        sides.Children.Add(_details);

        // A grid, not a dock panel: Tab follows the order the children are added, and the status bar's Sync button comes last,
        // after the list, while the bar still sits at the bottom.
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(sides, 1);
        Grid.SetRow(_statusBar, 2);
        layout.Children.Add(_header);
        layout.Children.Add(sides);
        layout.Children.Add(_statusBar);
        return layout;

    }

    private void OnChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            Refresh();
        }
        else
        {
            Dispatcher.BeginInvoke(Refresh);
        }
    }

    // ---- What shows ----

    private void Refresh()
    {
        if (_closing)
        {
            return;
        }

        _snapshot = _content.Build(_editing.Started, _snapshot);
        _selected = _selection.Update(_snapshot);
        var rowEdit = _editing.Current is { ShowsInRow: true } ? _editing.Current : null;
        _rows.Show(_snapshot, _collapse, rowEdit is null ? null : _titleAt, rowEdit?.Editor, _selection.IsSelected);
        ShowDetails();
    }

    private void ShowDetails()
    {
        var describing = _editing.Describing;
        if (describing is null)
        {
            // Preview belongs to an open description edit; whatever ends it, the next one opens on Edit.
            _previewing = false;
        }

        if (describing is not null)
        {
            _editBadges ??= new TagBadges(_tagColors, _theme);
            if (!_editBadges.Tags.SequenceEqual(_selected?.Tags ?? describing.Started.Tags))
            {
                _editBadges.Tags = _selected?.Tags ?? describing.Started.Tags;
            }

            if (_previewing)
            {
                _details.ShowPreview(describing.Draft, describing.Editor, _editBadges);
            }
            else
            {
                _details.ShowEditor(describing.Editor, _editBadges);
            }

            _tagEditor = null;
            _detailsId = null;
        }
        else if (_selected is not { } task)
        {
            _details.ShowNothing();
            _tagEditor = null;
            _detailsId = null;
        }
        else if (_detailsId != task.Id)
        {
            // A new control for each selected task, so selecting another drops what was typed in Custom.
            _tagEditor = NewTagControl();
            _detailsId = task.Id;
            _details.ShowTask(task.Description, _tagEditor);
        }
        else
        {
            _details.UpdateDescription(task.Description);

            // A change from Sync updates the badges and leaves an open menu or Custom as it is.
            if (_tagEditor is { } tagEditor && !tagEditor.Tags.SequenceEqual(task.Tags))
            {
                tagEditor.Tags = task.Tags;
            }
        }
    }

    private TagEditor NewTagControl()
    {
        var control = new TagEditor(_tasks, _tagColors, _theme) { Tags = _selected!.Tags };
        control.TagAdded += (_, tag) => ChangeTag(control, shown => _tagChanges.Add(shown, tag));
        control.TagRemoved += (_, tag) => ChangeTag(control, shown => _tagChanges.Remove(shown, tag));
        return control;
    }

    // The control already shows the change; when it can't be saved the badges go back to the task as shown.
    private void ChangeTag(TagEditor control, Func<TaskItem, TaskItem?> change)
    {
        _header.ShowMessage(null);
        if (_selected is not { } shown)
        {
            return;
        }

        if (change(shown) is null)
        {
            control.Tags = shown.Tags;
        }
        else
        {
            Refresh();
        }
    }

    private TaskRow CreateRow(TaskItem task)
    {
        var row = new TaskRow(task, _theme, _browser);
        row.SelectRequested += (_, _) => SelectTask(row.Task.Id);
        row.EditRequested += (_, _) => BeginEditOf(row.Task, row);
        row.EditTitleRequested += (_, _) => BeginTitleEditOf(row.Task, row);
        row.DeleteRequested += (_, _) => Delete(row.Task);
        row.ResolveToggled += (_, on) => Resolve(row.Task, row, on);
        row.LinkClicked += (_, address) => FollowLink(address);
        row.OpenFailed += (_, failure) => Report(failure.Message);
        row.TaskDropped += (_, drop) => DropOn(row, drop.Dropped, drop.LowerHalf);
        return row;
    }

    private void Report(string message)
    {
        if (_closing)
        {
            _carry(message);
        }
        else
        {
            _header.ShowMessage(message);
        }
    }

    // ---- Select ----

    // Selecting another task leaves an open edit, as Esc does.
    private void SelectTask(string id)
    {
        _header.ShowMessage(null);
        if (_editing.Started is { } editing && editing.Id != id)
        {
            LeaveEdit();
        }

        _selection.Select(id);
        Refresh();
    }

    private void FocusSelectedRow()
    {
        if (_selection.SelectedId is { } id && _rows.FirstRowOf(id) is { } row)
        {
            Keyboard.Focus(row);
        }
    }

    // ---- Edit ----

    private void BeginEditOf(TaskItem task, TaskRow? row) => Begin(task, row, title: false);

    // Without a row, as when a collapsed group hides the task, the group opens first so the title has a row to edit in.
    private void BeginTitleEditOf(TaskItem task, TaskRow? row)
    {
        if (row is null)
        {
            _collapse.Reveal(task);
            Refresh();
            row = _rows.FirstRowOf(task.Id);
        }

        Begin(task, row, title: true);
    }

    // Opens the title edit in the row, or the description edit on the right side. One edit is open at a time, so an edit of
    // another field or task is left first, as Esc leaves it.
    private void Begin(TaskItem task, TaskRow? row, bool title)
    {
        _header.ShowMessage(null);
        var at = (row is null ? null : _rows.PositionOf(row)) ?? new RowPosition(null, task.Id);
        if (_editing.Current is { } open && open.Started.Id == task.Id && open.ShowsInRow == title)
        {
            if (title)
            {
                // The edit goes to the row the user chose, with its text, and takes the cursor there.
                _titleAt = at;
                Refresh();
                UpdateLayout();
                open.FocusEnd();
            }
            else
            {
                _pending.CommitIfFor(task.Id);
            }

            return;
        }

        SelectTask(task.Id);
        if (_editing.Current is not null)
        {
            LeaveEdit();
        }

        // A fresh description edit starts from a clean stage. When that can't be made the edit doesn't open, and nothing else
        // changes. A title edit has no images, so it leaves the stage, and the Undo of a description edit just left, alone.
        if (title || _cleanup.BeginFresh(task.Id))
        {
            _titleAt = at;
            var edit = title
                ? _editing.BeginTitle(task, () => SaveEdit(), LeaveEdit)
                : _editing.BeginDescription(task, () => SaveEdit(), LeaveEdit);
            Refresh();

            // The editor joins the window's visual tree on layout, and only an element in it takes the focus.
            UpdateLayout();
            edit.FocusEnd();
        }
    }

    private bool SaveEdit()
    {
        if (!_editing.Save())
        {
            if (ShowEditInFull())
            {
                _editing.Current!.FocusEnd();
            }

            return false;
        }

        Refresh();
        FocusSelectedRow();
        return true;
    }

    // Closing the editor without saving: a clean description throws its images away; a dirty one can be brought back with Undo.
    private void LeaveEdit()
    {
        var hadFocus = _previewing || _editing.Current!.Editor.IsKeyboardFocusWithin;
        _editing.Leave();
        Refresh();
        if (hadFocus)
        {
            FocusSelectedRow();
        }
    }

    // Undo brings the edit back even when a filter now hides the task, so nothing typed is lost. It opens on Edit.
    private void RestoreEdit(string id)
    {
        _selection.Select(id);
        _previewing = false;
        Refresh();
        _editing.Current!.FocusEnd();
    }

    // Shows the open edit where the user can type in it: on its Edit tab, and in its row when a collapsed group hid that. True
    // when that changed anything, so the cursor needs putting back.
    private bool ShowEditInFull()
    {
        var changed = _previewing;
        _previewing = false;
        if (_editing.Current is { ShowsInRow: true } && _titleAt is { } at && !_rows.IsShown(at))
        {
            _collapse.Expand(at.Tag);
            changed = true;
        }

        if (changed)
        {
            Refresh();
            UpdateLayout();
        }

        return changed;
    }

    // ---- Tabs ----

    // Preview shows the draft as it is now, so it is shown again at each switch, and the focus goes to the right side to scroll
    // it. Edit puts the cursor back where it was.
    private void SwitchTab(bool preview)
    {
        if (_editing.Describing is not { } edit || _previewing == preview)
        {
            return;
        }

        _header.ShowMessage(null);
        _previewing = preview;
        ShowDetails();
        UpdateLayout();
        if (preview)
        {
            _details.FocusContent();
        }
        else
        {
            edit.FocusEditor();
        }
    }

    // The new width goes into the draft, as typing it would; saving and abandoning treat it as the rest of the edit.
    private void ResizeImage(ImageResize resize)
    {
        _header.ShowMessage(null);
        if (_previewing && _editing.Describing is { } edit)
        {
            edit.SetImageWidth(resize.Image, resize.Width);
        }
    }

    // The edit as closing sees it, or null when none is open.
    private IClosingEdit? ClosingEdit() => _editing.Current is { } edit ? new EditToClose(this, edit) : null;

    private sealed class EditToClose(TaskListWindow window, IFieldEdit edit) : IClosingEdit
    {
        public bool HasChanges => edit.HasChanges;

        public bool Save() => window.SaveEdit();

        // The window is going, so a failure to remove the images is carried to the next one, as when it closes.
        public void Discard()
        {
            window._closing = true;
            window._editing.Forget();
            edit.Close(window._cleanup);
        }

        // Nothing to return to when the application's exit closed the window under the prompt.
        public void ReturnToEditor()
        {
            if (!window._closed)
            {
                window.ShowEditInFull();
                edit.FocusEnd();
            }
        }
    }

    // ---- Groups ----

    private void ToggleGroup(string? tag)
    {
        _collapse.Toggle(tag);
        Refresh();
    }

    // ---- Delete, resolve, reopen ----

    private void Delete(TaskItem task)
    {
        _header.ShowMessage(null);
        var id = task.Id;
        if (_editing.Started?.Id == id)
        {
            _editing.Forget();
        }

        _deletion.Start(id);
    }

    // The row, when one shows the task, puts its checkbox back when the task isn't resolved.
    private void Resolve(TaskItem task, TaskRow? row, bool resolved)
    {
        _header.ShowMessage(null);
        var id = task.Id;

        // Checking the task being edited saves the edit first, as Ctrl+Enter does. When that fails the editor stays open
        // and says why, and the task isn't resolved.
        if (_editing.Started?.Id == id && !SaveEdit())
        {
            row?.Revert();
            return;
        }

        if (!_actions.Resolve(id, resolved))
        {
            row?.Revert();
        }
    }

    // ---- Order ----

    // Order is a row's place in its group, so a task no row shows doesn't move.
    private void Nudge(TaskRow? row, int step)
    {
        _header.ShowMessage(null);
        if (row is not null && ListOrder.Nudge(row.Group, row.Task.Id, step) is { } move)
        {
            _actions.Move(move);
        }
    }

    private void DropOn(TaskRow targetRow, DraggedTask dropped, bool lowerHalf)
    {
        _header.ShowMessage(null);
        if (ListOrder.Drop(dropped.Source, targetRow.Group, _snapshot.Shown, targetRow.Task.Id, dropped.Task.Id, lowerHalf) is { } move)
        {
            _actions.Move(move);
        }
    }

    // ---- Links ----

    private void FollowLink(string address)
    {
        var outcome = _links.Follow(address);
        if (outcome == LinkOutcome.Ignored)
        {
            return;
        }

        _header.ShowMessage(null);
        if (outcome.TaskId is not { } id)
        {
            Report(GoneText);
            return;
        }

        if (_tasks.All.FirstOrDefault(task => task.Id == id) is { Resolved: null } target)
        {
            _collapse.Reveal(target);
        }

        SelectTask(id);
        _rows.FirstRowOf(id)?.BringIntoView();
    }

    // ---- Keys ----

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = EventKey.Of(e);
        if (ModifierKey.Of(key) != ModifierKeys.None)
        {
            _held.Pressed(key);
            return;
        }

        _held.OtherKeyReached();
        if (key == Key.F5 && _held.Modifiers == ModifierKeys.None)
        {
            SyncOnKey();
            e.Handled = true;
            return;
        }

        if (e.Handled)
        {
            return;
        }

        var modifiers = _held.Modifiers;
        if (TabKeys(modifiers, key))
        {
            e.Handled = true;
        }
        else if (key == Key.Escape && modifiers == ModifierKeys.None && _editing.Current is not null && !_editing.Contains(e.OriginalSource))
        {
            // Esc with the focus outside the open editor still takes the editor's steps. An editor out of the window, such as a
            // title edit hidden by a collapsed group, can't be sent the key, so the edit is left as the key leaves it.
            e.Handled = _editing.SendEsc(e);
            if (!e.Handled)
            {
                LeaveEdit();
                e.Handled = true;
            }
        }
        else if (modifiers == ModifierKeys.Control && key == Key.F && !_editing.Contains(e.OriginalSource))
        {
            _header.FocusSearch();
            e.Handled = true;
        }
        else if (e.OriginalSource is TaskRow row)
        {
            e.Handled = ActOnRow(row, modifiers, key);
        }
        else if (ReferenceEquals(e.OriginalSource, _details) && _editing.Describing is null && _selected is { } selected)
        {
            // The pane itself has the focus and shows the selected task, so the row keys act on that task, in its first row
            // when one shows it. Up and Down stay with the pane, which scrolls on them.
            e.Handled = ActOnRowKeys(selected, _rows.FirstRowOf(selected.Id), modifiers, key);
        }
    }

    // While a description is edited, Ctrl+Tab and Ctrl+Shift+Tab switch between Edit and Preview, from anywhere. In Preview no
    // editor has the focus to see the Save shortcut, so the window saves on it.
    private bool TabKeys(ModifierKeys modifiers, Key key)
    {
        if (_editing.Describing is null)
        {
            return false;
        }

        // Save comes first: the user may set Ctrl+Tab or Ctrl+Shift+Tab as a Save key, and Save wins over the other keys.
        if (_saveKeys.Matches(modifiers, key))
        {
            SaveEdit();
            return true;
        }

        if (key == Key.Tab && modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
        {
            SwitchTab(!_previewing);
            return true;
        }

        return false;
    }

    // Up and Down select the row above or below; the other keys are the row's actions.
    private bool ActOnRow(TaskRow row, ModifierKeys modifiers, Key key)
    {
        if (ActOnRowKeys(row.Task, row, modifiers, key))
        {
            return true;
        }

        if (modifiers == ModifierKeys.None && key is Key.Up or Key.Down)
        {
            if (_rows.Neighbor(row, key == Key.Up ? -1 : 1) is { } next)
            {
                Keyboard.Focus(next);
                SelectTask(next.Task.Id);
            }

            return true;
        }

        return false;
    }

    private bool ActOnRowKeys(TaskItem task, TaskRow? row, ModifierKeys modifiers, Key key)
    {
        if (RowKeys.ActionFor(modifiers, key) is not { } action)
        {
            return false;
        }

        _rowActions[action](task, row);
        return true;
    }

    private async void SyncOnKey() => await _sync.SyncAsync();
}
