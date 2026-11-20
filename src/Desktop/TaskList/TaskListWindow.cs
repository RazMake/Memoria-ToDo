using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

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
    private readonly ITagOrder _tagOrder;
    private readonly IContacts _contacts;
    private readonly GroupActions _groupActions;
    private readonly ITheme _theme;
    private readonly TimeProvider _time;
    private readonly IDefaultBrowser _browser;
    private readonly Action<string> _carry;
    private readonly ILogger _logger;
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
    private readonly IDragPointer _pointer;
    private readonly ListSelection _selection = new();
    private readonly ListEditing _editing;
    private readonly LeaveDecision _decision;
    private readonly HeldModifierKeys _held = new();
    private readonly Dictionary<RowAction, Action<TaskItem, TaskRow?>> _rowActions;
    private ListSnapshot _snapshot = new([], [], false);
    private TaskItem? _selected;
    private TagEditor? _tagEditor;
    private string? _tagEditorId;
    private RowPosition? _titleAt;
    private string? _detailsId;
    private bool _previewing;
    private bool _closing;
    private bool _closed;

    public TaskListWindow(ITasks tasks, ITagColors tagColors, ITagOrder tagOrder, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, IDescriptionWebView webView,
        IDefaultBrowser browser, ListSync sync, IUpdateStatus updates, SaveKeys saveKeys, IDragPointer pointer, string? message, Action<string> carry, ILogger logger)
    {
        _sync = sync;
        _contacts = contacts;
        _pointer = pointer;
        _tagOrder = tagOrder;
        _statusBar = new UpdateStatusBar(updates, new ListStatusStart(sync));
        _saveKeys = saveKeys;
        _tagColors = tagColors;
        _decision = new LeaveDecision(new CloseDialog(this));
        _tasks = tasks;
        _theme = theme;
        _time = time;
        _browser = browser;
        _carry = carry;
        _logger = logger;
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
        _description.OpenFailed += (_, failure) =>
        {
            logger.Error(failure, "A link couldn't be opened");
            Report(failure.Message);
        };
        _description.ImageResized += (_, resize) => ResizeImage(resize);
        _details = new DetailsPane(_description);
        _details.TabChosen += (_, preview) => SwitchTab(preview);

        _header = new ListHeader(_filter);
        _content = new ListContent(tasks, _filter);
        _links = new ListLinks(tasks, _filter, _content);
        _actions = new ListActions(tasks, Report, logger);
        _pending = new PendingUndo(time, post => Dispatcher.BeginInvoke(post), () => _header.ShowUndo(_pending!.IsPending ? _pending.Label : null));
        _deletion = new TaskDeletion(tasks, _content, _pending, Report, Refresh, logger);
        _cleanup = new EditCleanup(tasks, _pending, Report, logger);
        _tagChanges = new TagChanges(tasks, _pending, _cleanup, Report, logger);
        _editing = new ListEditing(tasks, contacts, palette, theme, time, clipboard, imageFiles, files, saveKeys, _cleanup, RestoreEdit, logger);
        _rows = new GroupedRows(tagColors, theme, tagOrder, CreateRow, task => LiveMentions.Of(contacts, task.Title, title: true), pointer);
        _groupActions = new GroupActions(tagOrder, Report, logger);
        _header.ShowMessage(message);
        _filter.Changed += (_, _) => Refresh();
        _header.UndoClicked += (_, _) => _pending.Undo();
        _header.EdgeRowRequested += (_, last) => SelectEdgeRow(last);
        Loaded += (_, _) => _header.FocusSearch();
        _rows.ResolvedClicked += (_, _) =>
        {
            _content.ShowResolved = !_content.ShowResolved;
            Refresh();
        };
        _rows.GroupToggled += (_, tag) => ToggleGroup(tag);
        _rows.TaskDropped += (_, drop) => DropIn(drop);
        _rows.GroupDropped += (_, move) => MoveGroup(move);

        Content = Layout();
        EscCloses.Attach(this);
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, e) => _held.Released(EventKey.Of(e));
        Deactivated += (_, _) => _held.Clear();
        tasks.Changed += OnChanged;
        tagOrder.Changed += OnChanged;
        contacts.Changed += OnChanged;
        Refresh();
    }

    /// <summary>
    /// Closes the window as its close button does: a changed edit asks first. False, with the window open, when the
    /// user cancels, the save fails, or the prompt already shows.
    /// </summary>
    public bool TryClose()
    {
        var mayClose = _decision.MayLeave(ClosingEdit());

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
        _tagOrder.Changed -= OnChanged;
        _contacts.Changed -= OnChanged;
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
        AutoScroll.Attach(left, _time, pointer => _rows.ContinueDrag(left.TranslatePoint(pointer, _rows)));
        var sides = new SplitPanes(left, 68, 300, _details, 32, 340);

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

        // The tag control stays live during an edit, so it follows the task the edit is of when a filter hides it.
        var task = ShownTask();
        if (task is null)
        {
            _tagEditor = null;
            _tagEditorId = null;
            _detailsId = null;
            _details.ShowNothing();
            return;
        }

        var tags = TagControlFor(task);
        if (describing is not null)
        {
            if (_previewing)
            {
                _details.ShowPreview(LiveMentions.Of(_contacts, describing.Draft, title: false), describing.Editor, tags);
            }
            else
            {
                _details.ShowEditor(describing.Editor, tags);
            }

            _detailsId = null;
        }
        else if (_detailsId != task.Id)
        {
            _detailsId = task.Id;
            _details.ShowTask(LiveMentions.Of(_contacts, task.Description, title: false), tags);
        }
        else
        {
            _details.UpdateDescription(LiveMentions.Of(_contacts, task.Description, title: false));
        }
    }

    // The task the right side shows: the selected one, or the one a description edit is of when a filter hides it.
    private TaskItem? ShownTask() => _selected ?? _editing.Describing?.Started;

    // A new control for each task, so selecting another drops what was typed in Custom. A change from Sync updates the pill and
    // leaves an open menu or Custom as it is. During an edit of the task the pill shows the edit's tag, and Sync doesn't change it.
    private TagEditor TagControlFor(TaskItem task)
    {
        var tag = PillTagOf(task);
        if (_tagEditor is null || _tagEditorId != task.Id)
        {
            _tagEditor = NewTagControl(task);
            _tagEditorId = task.Id;
        }
        else if (_tagEditor.TaskTag != tag)
        {
            _tagEditor.TaskTag = tag;
        }

        return _tagEditor;
    }

    private string? PillTagOf(TaskItem task) =>
        _editing.Current is { } edit && edit.Started.Id == task.Id ? edit.Tag : task.Tags.FirstOrDefault();

    private TagEditor NewTagControl(TaskItem task)
    {
        var control = new TagEditor(_tasks, _tagColors, _theme, _logger) { TaskTag = PillTagOf(task) };
        control.TagChanged += (_, tag) => ChangeTag(control, tag);
        return control;
    }

    // During an edit the choice only changes the pill and the edit keeps it, for the edit's one save. Otherwise the control already
    // shows the change, and when it can't be saved the pill goes back to the task as shown.
    private void ChangeTag(TagEditor control, string? tag)
    {
        _header.ShowMessage(null);
        if (_editing.Current is { } edit)
        {
            edit.Tag = tag;
            return;
        }

        if (ShownTask() is not { } shown)
        {
            return;
        }

        if ((tag is null ? _tagChanges.Clear(shown) : _tagChanges.Choose(shown, tag)) is null)
        {
            control.TaskTag = shown.Tags.FirstOrDefault();
        }
        else
        {
            Refresh();
        }
    }

    private TaskRow CreateRow(TaskItem task)
    {
        var row = new TaskRow(task, _theme, _browser, _pointer);
        row.SelectRequested += (_, _) => SelectTask(row.Task.Id);
        row.EditRequested += (_, _) => BeginEditOf(row.Task, row);
        row.EditTitleRequested += (_, _) => BeginTitleEditOf(row.Task, row);
        row.DeleteRequested += (_, _) => Delete(row.Task);
        row.SearchRequested += (_, _) => _header.FocusSearch();
        row.ResolveToggled += (_, on) => Resolve(row.Task, row, on);
        row.LinkClicked += (_, address) => FollowLink(address);
        row.OpenFailed += (_, failure) => Report(failure.Message);
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

    /// <summary>Empties the search and puts the cursor in it, as when the window's shortcut brings it forward.</summary>
    public void StartSearch()
    {
        _filter.ClearSearch();
        _header.FocusSearch();
    }

    // Down in the search box selects the top row, Up the bottom one, and the list scrolls to it.
    private void SelectEdgeRow(bool last)
    {
        if ((last ? _rows.LastRow : _rows.FirstRow) is not { } row)
        {
            return;
        }

        // Focus scrolls the row into view; this row, not the task's first one, since a task can show in two groups.
        Keyboard.Focus(row);
        SelectTask(row.Task.Id);
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
        else
        {
            DropTagText();
        }

        // A title edit saves the whole task, so the task's own tag-change bar is finished first; its Undo would save the old tag over the edit.
        if (title)
        {
            _tagChanges.FinishFor(task.Id);
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
        // Text left in Custom becomes the pill's tag first, as Save does in Add; the edit's one save then carries it.
        _tagEditor?.ChooseTypedText();
        if (!_editing.Save())
        {
            if (ShowEditInFull())
            {
                _editing.Current!.FocusEnd();
            }

            return false;
        }

        DropTagText();
        Refresh();
        FocusSelectedRow();
        return true;
    }

    // Whatever an edit ends by, text typed in Custom and never chosen goes with it, and Custom and the menu close. A tag control
    // closes one part at a time, innermost first.
    private void DropTagText()
    {
        while (_tagEditor?.CloseEdit() == true)
        {
        }
    }

    // Closing the editor without saving: a clean description throws its images away; a dirty one can be brought back with Undo.
    private void LeaveEdit()
    {
        DropTagText();
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
    private ILeavingEdit? ClosingEdit() => _editing.Current is { } edit ? new EditToClose(this, edit) : null;

    private sealed class EditToClose(TaskListWindow window, IFieldEdit edit) : ILeavingEdit
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

    private void DropIn((DraggedTask Dropped, GroupKey Target, IReadOnlyList<TaskItem> TargetRows, int Index) drop)
    {
        _header.ShowMessage(null);
        if (ListOrder.DropIn(drop.Dropped.Source, drop.Target, drop.TargetRows, _snapshot.Shown, drop.Dropped.Task.Id, drop.Index) is { } move)
        {
            _actions.Move(move);
        }
    }

    // The group moves at once, in Tasks' tag order. The list regroups when the order says it changed, so a move that can't be
    // saved leaves the group where it was.
    private void MoveGroup(GroupMove move)
    {
        _header.ShowMessage(null);
        _groupActions.Move(move);
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
        // Ctrl+F comes before TabKeys, so it searches even when the user made it a Save key.
        if (modifiers == ModifierKeys.Control && key == Key.F)
        {
            _header.FocusSearch();
            e.Handled = true;
        }
        else if (TabKeys(modifiers, key))
        {
            e.Handled = true;
        }
        else if (key == Key.Escape && modifiers == ModifierKeys.None && _tagEditor is { } tagControl && tagControl.CloseEdit())
        {
            // The tag control's menu or Custom closes before an edit does, wherever the cursor is.
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
    // editor has the focus to see the Save shortcut, and in a title edit the tag control, on the right side, doesn't either, so
    // the window saves on it.
    private bool TabKeys(ModifierKeys modifiers, Key key)
    {
        if (_editing.Current is null)
        {
            return false;
        }

        // Save comes first: the user may set Ctrl+Tab or Ctrl+Shift+Tab as a Save key, and Save wins over the other keys.
        if (_saveKeys.Matches(modifiers, key))
        {
            SaveEdit();
            return true;
        }

        if (_editing.Describing is not null && key == Key.Tab && modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
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
            else
            {
                _header.FocusSearch();
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

    // Esc that no list, menu, or edit took empties the search before it may close the window. This class handler runs
    // before the platform's Esc handler.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && EventKey.Of(e) == Key.Escape && _held.Modifiers == ModifierKeys.None && _filter.SearchText.Length > 0)
        {
            _filter.ClearSearch();
            e.Handled = true;
        }
    }

    private async void SyncOnKey() => await _sync.SyncAsync();
}
