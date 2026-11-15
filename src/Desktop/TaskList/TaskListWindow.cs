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
/// and above them the search, the tag filter, and the resolved tasks. It works on the tasks through <see cref="ITasks"/> and
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
    private readonly ListSelection _selection = new();
    private readonly ListEditing _editing;
    private readonly CloseDecision _decision;
    private readonly HeldModifierKeys _held = new();
    private readonly Dictionary<RowAction, Action<TaskRow>> _rowActions;
    private ListSnapshot _snapshot = new([], [], false);
    private TaskItem? _selected;
    private TagEditor? _tagEditor;
    private string? _detailsId;
    private bool _closing;
    private bool _closed;

    public TaskListWindow(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, IDescriptionWebView webView,
        IDefaultBrowser browser, ListSync sync, IUpdateStatus updates, string? message, Action<string> carry)
    {
        _sync = sync;
        _statusBar = new UpdateStatusBar(updates, sync);
        _tagColors = tagColors;
        _decision = new CloseDecision(new CloseDialog(this));
        _tasks = tasks;
        _theme = theme;
        _browser = browser;
        _carry = carry;
        _rowActions = new()
        {
            [RowAction.Toggle] = row => SelectTask(row.Task.Id),
            [RowAction.Edit] = BeginEditOf,
            [RowAction.Resolve] = row => Resolve(row, row.Task.Resolved is null),
            [RowAction.Delete] = Delete,
            [RowAction.MoveUp] = row => Nudge(row, -1),
            [RowAction.MoveDown] = row => Nudge(row, 1),
        };

        Title = "Task View";
        Width = 1100;
        Height = 720;
        MinWidth = 680;

        _description = new FormattedDescription(webView, theme, browser);
        _description.OtherLinkClicked += (_, address) => FollowLink(address);
        _description.OpenFailed += (_, failure) => Report(failure.Message);
        _details = new DetailsPane(_description);

        _header = new ListHeader(tasks, tagColors, theme, _filter);
        _content = new ListContent(tasks, _filter);
        _links = new ListLinks(tasks, _filter, _content);
        _actions = new ListActions(tasks, Report);
        _pending = new PendingUndo(time, post => Dispatcher.BeginInvoke(post), () => _header.ShowUndo(_pending!.IsPending ? _pending.Label : null));
        _deletion = new TaskDeletion(tasks, _content, _pending, Report, Refresh);
        _cleanup = new EditCleanup(tasks, _pending, Report);
        _tagChanges = new TagChanges(tasks, _pending, _cleanup, Report);
        _editing = new ListEditing(tasks, tagColors, palette, theme, time, clipboard, imageFiles, files, _cleanup, RestoreEdit);
        _rows = new GroupedRows(tagColors, theme, CreateRow);
        _header.ShowMessage(message);
        _filter.Changed += (_, _) => Refresh();
        _header.UndoClicked += (_, _) => _pending.Undo();
        _rows.ResolvedClicked += (_, _) =>
        {
            _content.ShowResolved = !_content.ShowResolved;
            Refresh();
        };

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
            if (_editing.Forget() is { } started)
            {
                _cleanup.Close(started.Id);
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
    private DockPanel Layout()
    {
        var left = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _rows };
        var splitter = new GridSplitter
        {
            Width = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
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

        var layout = new DockPanel();
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        DockPanel.SetDock(_header, Dock.Top);
        layout.Children.Add(_statusBar);
        layout.Children.Add(_header);
        layout.Children.Add(sides);
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
        _rows.Show(_snapshot, _filter.TagFilter, _editing.Started?.Id, _selection.IsSelected);
        ShowDetails();
        _header.ShowTags([.. _tasks.TagsInUse().Select(use => use.Tag)]);
    }

    private void ShowDetails()
    {
        if (_editing.Editor is { } editor)
        {
            _details.ShowEditor(editor);
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
        row.EditRequested += (_, _) => BeginEditOf(row);
        row.DeleteRequested += (_, _) => Delete(row);
        row.ResolveToggled += (_, on) => Resolve(row, on);
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

    private void BeginEditOf(TaskRow row)
    {
        _header.ShowMessage(null);
        var task = row.Task;
        if (_editing.Started?.Id == task.Id)
        {
            _pending.CommitIfFor(task.Id);
            return;
        }

        SelectTask(task.Id);

        // A fresh edit starts from a clean stage. When that can't be made the edit doesn't open, and nothing else changes.
        if (_cleanup.BeginFresh(task.Id))
        {
            var editor = _editing.Begin(task, () => SaveEdit(), LeaveEdit);
            Refresh();

            // The editor joins the window's visual tree on layout, and only an element in it takes the focus.
            UpdateLayout();
            editor.FocusDescription();
        }
    }

    private bool SaveEdit()
    {
        if (!_editing.Save())
        {
            return false;
        }

        Refresh();
        FocusSelectedRow();
        return true;
    }

    // Closing the editor without saving: a clean one throws its images away; a dirty one can be brought back with Undo.
    private void LeaveEdit()
    {
        var hadFocus = _editing.Editor!.IsKeyboardFocusWithin;
        _editing.Leave();
        Refresh();
        if (hadFocus)
        {
            FocusSelectedRow();
        }
    }

    // Undo brings the edit back even when a filter now hides the task, so nothing typed is lost.
    private void RestoreEdit(string id)
    {
        _selection.Select(id);
        Refresh();
        _editing.Editor!.FocusTitle();
    }

    // The edit as closing sees it, or null when none is open.
    private IClosingEdit? ClosingEdit() => _editing.Editor is { } editor ? new EditToClose(this, editor, _editing.Started!.Id) : null;

    private sealed class EditToClose(TaskListWindow window, TaskEditorControl editor, string taskId) : IClosingEdit
    {
        public bool HasChanges => editor.HasChanges;

        public bool Save() => window.SaveEdit();

        // The window is going, so a failure to remove the images is carried to the next one, as when it closes.
        public void Discard()
        {
            window._closing = true;
            window._editing.Forget();
            window._cleanup.Close(taskId);
        }

        // Nothing to return to when the application's exit closed the window under the prompt.
        public void ReturnToEditor()
        {
            if (!window._closed)
            {
                editor.FocusTitle();
            }
        }
    }

    // ---- Delete, resolve, reopen ----

    private void Delete(TaskRow row)
    {
        _header.ShowMessage(null);
        var id = row.Task.Id;
        if (_editing.Started?.Id == id)
        {
            _editing.Forget();
        }

        _deletion.Start(id);
    }

    private void Resolve(TaskRow row, bool resolved)
    {
        _header.ShowMessage(null);
        var id = row.Task.Id;

        // Checking the task being edited saves the edit first, as Ctrl+Enter does. When that fails the editor stays open
        // and says why, and the task isn't resolved.
        if (_editing.Started?.Id == id && !SaveEdit())
        {
            row.Revert();
            return;
        }

        if (!_actions.Resolve(id, resolved))
        {
            row.Revert();
        }
    }

    // ---- Order ----

    private void Nudge(TaskRow row, int step)
    {
        _header.ShowMessage(null);
        if (ListOrder.Nudge(row.Group, row.Task.Id, step) is { } move)
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
        // The open All tags list is the innermost step, so it closes first and alone. This handler is added before the tag
        // control's window handler, so it runs first, and a tag part stays open under it.
        if (key == Key.Escape && modifiers == ModifierKeys.None && _header.CloseAllTagsList())
        {
            e.Handled = true;
        }
        else if (key == Key.Escape && modifiers == ModifierKeys.None && _editing.Editor is not null && !_editing.Contains(e.OriginalSource))
        {
            e.Handled = _editing.SendEsc(e);
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
    }

    // Up and Down select the row above or below; the other keys are the row's actions.
    private bool ActOnRow(TaskRow row, ModifierKeys modifiers, Key key)
    {
        if (RowKeys.ActionFor(modifiers, key) is { } action)
        {
            _rowActions[action](row);
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

    private async void SyncOnKey() => await _sync.SyncAsync();
}
