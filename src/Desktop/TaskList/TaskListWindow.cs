using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The View Tasks window: the tasks in rank order, one of them open at a time, with the search, the tag filter, and
/// the resolved tasks. It works on the tasks through <see cref="ITasks"/> and shows what they say.
/// </summary>
internal sealed class TaskListWindow : Window
{
    private const string GoneText = "This task no longer exists.";

    private readonly ITasks _tasks;
    private readonly IReadOnlyList<PaletteColor> _palette;
    private readonly ITheme _theme;
    private readonly TimeProvider _time;
    private readonly IClipboard _clipboard;
    private readonly IImageFileChooser _imageFiles;
    private readonly IFileSystem _files;
    private readonly IDefaultBrowser _browser;
    private readonly Action<string> _carry;
    private readonly FormattedDescription _description;
    private readonly Border _park = new() { Height = 0 };
    private readonly ListFilter _filter = new();
    private readonly ListHeader _header;
    private readonly ListSync _sync;
    private readonly PendingUndo _pending;
    private readonly ListContent _content;
    private readonly ListLinks _links;
    private readonly ListActions _actions;
    private readonly TaskDeletion _deletion;
    private readonly EditCleanup _cleanup;
    private readonly StackPanel _panel = new() { Margin = new Thickness(8, 0, 8, 8) };
    private readonly Button _resolvedButton = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Dictionary<string, TaskRow> _rows = [];
    private readonly HeldModifierKeys _held = new();
    private readonly Dictionary<RowAction, Action<TaskRow>> _rowActions;
    private ListSnapshot _snapshot = new([], [], false);
    private TaskRow? _descriptionRow;
    private string? _expandedId;
    private Edit? _edit;
    private bool _closing;

    public TaskListWindow(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, IDescriptionWebView webView,
        IDefaultBrowser browser, ListSync sync, string? message, Action<string> carry)
    {
        _sync = sync;
        _tasks = tasks;
        _palette = palette;
        _theme = theme;
        _time = time;
        _clipboard = clipboard;
        _imageFiles = imageFiles;
        _files = files;
        _browser = browser;
        _carry = carry;
        _rowActions = new()
        {
            [RowAction.Toggle] = ToggleExpanded,
            [RowAction.Edit] = BeginEditOf,
            [RowAction.Resolve] = row => Resolve(row, row.Task.Resolved is null),
            [RowAction.Delete] = Delete,
            [RowAction.MoveUp] = row => Nudge(row, -1),
            [RowAction.MoveDown] = row => Nudge(row, 1),
        };

        Title = "View Tasks";
        Width = 680;
        Height = 720;
        MinWidth = 400;

        _description = new FormattedDescription(webView, theme, browser);
        _description.OtherLinkClicked += (_, address) => FollowLink(address);
        _description.OpenFailed += (_, failure) => Report(failure.Message);
        _park.Child = _description;

        _header = new ListHeader(tasks, _filter);
        _content = new ListContent(tasks, _filter);
        _links = new ListLinks(tasks, _filter, _content);
        _actions = new ListActions(tasks, Report);
        _pending = new PendingUndo(time, post => Dispatcher.BeginInvoke(post), () => _header.ShowUndo(_pending!.IsPending ? _pending.Label : null));
        _deletion = new TaskDeletion(tasks, _content, _pending, Report, Refresh);
        _cleanup = new EditCleanup(tasks, _pending, Report);
        _header.ShowMessage(message);
        _filter.Changed += (_, _) => Refresh();
        _header.UndoClicked += (_, _) => _pending.Undo();
        _resolvedButton.Click += (_, _) =>
        {
            _content.ShowResolved = !_content.ShowResolved;
            Refresh();
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _panel };
        var layout = new DockPanel();
        DockPanel.SetDock(_sync, Dock.Top);
        DockPanel.SetDock(_header, Dock.Top);
        DockPanel.SetDock(_park, Dock.Bottom);
        layout.Children.Add(_sync);
        layout.Children.Add(_header);
        layout.Children.Add(_park);
        layout.Children.Add(scroll);
        Content = layout;

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, e) => _held.Released(EventKey.Of(e));
        Deactivated += (_, _) => _held.Clear();
        tasks.Changed += OnChanged;
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closing = true;
        _tasks.Changed -= OnChanged;
        try
        {
            _pending.Commit();
            if (_edit is { } edit)
            {
                _edit = null;
                _cleanup.Close(edit.Started.Id);
            }
        }
        finally
        {
            _sync.Dispose();
            _pending.Dispose();
            _description.Dispose();
        }
    }

    private sealed record Edit(TaskRow Row, TaskEditorControl Editor, TaskItem Started);

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

        var snapshot = _content.Build(_edit?.Row.Task, _snapshot);
        _snapshot = snapshot;
        var shown = snapshot.Shown;
        if (_expandedId is { } expanded && _edit is null && shown.All(task => task.Id != expanded))
        {
            ClearExpanded();
        }

        var wanted = shown.Select(task => task.Id).ToHashSet();
        foreach (var id in _rows.Keys.Where(id => !wanted.Contains(id)).ToList())
        {
            _rows.Remove(id);
        }

        var order = new List<UIElement>();
        if (_edit is { } editing)
        {
            _rows[editing.Started.Id] = editing.Row;
        }
        foreach (var task in snapshot.Open)
        {
            order.Add(RowFor(task));
        }

        _resolvedButton.Content = $"{snapshot.ResolvedCount} resolved";
        System.Windows.Automation.AutomationProperties.SetName(_resolvedButton, $"{snapshot.ResolvedCount} resolved");
        if (snapshot.ResolvedCount > 0)
        {
            order.Add(_resolvedButton);
            if (snapshot.ShowResolved)
            {
                order.AddRange(snapshot.Resolved.Select(RowFor));
            }
        }

        if (!_panel.Children.Cast<UIElement>().SequenceEqual(order))
        {
            var focused = Keyboard.FocusedElement;
            _panel.Children.Clear();
            foreach (var element in order)
            {
                _panel.Children.Add(element);
            }

            if (focused is not null)
            {
                Keyboard.Focus(focused);
            }
        }

        if (_expandedId is not null && _edit is null && _descriptionRow is { } row && _description.Markdown != row.Task.Description)
        {
            _description.Markdown = row.Task.Description;
        }

        _header.ShowTags([.. _tasks.TagsInUse().Select(use => use.Tag)]);
    }

    private TaskRow RowFor(TaskItem task)
    {
        if (_rows.TryGetValue(task.Id, out var row))
        {
            if (_edit?.Row != row)
            {
                row.Update(task);
            }

            return row;
        }

        row = new TaskRow(task, _palette, _theme, _browser);
        row.ClickRequested += (_, _) => ToggleExpanded(row);
        row.EditRequested += (_, _) => BeginEditOf(row);
        row.DeleteRequested += (_, _) => Delete(row);
        row.ResolveToggled += (_, on) => Resolve(row, on);
        row.LinkClicked += (_, address) => FollowLink(address);
        row.OpenFailed += (_, failure) => Report(failure.Message);
        row.TaskDropped += (_, drop) => DropOn(row, drop.Dropped, drop.LowerHalf);
        _rows[task.Id] = row;
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

    // ---- Open and close a task ----

    private void ToggleExpanded(TaskRow row)
    {
        _header.ShowMessage(null);
        if (_expandedId == row.Task.Id)
        {
            Collapse();
        }
        else
        {
            Expand(row);
        }
    }

    private void Expand(TaskRow row)
    {
        Collapse();
        _expandedId = row.Task.Id;
        ShowDescription(row);
    }

    private void Collapse()
    {
        if (_edit is not null)
        {
            LeaveEdit();
        }

        ClearExpanded();
        Refresh();
    }

    private void ClearExpanded()
    {
        if (_expandedId is { } id && _rows.TryGetValue(id, out var row))
        {
            row.ShowActions(false);
        }

        _expandedId = null;
        ParkDescription();
    }

    private void ShowDescription(TaskRow row)
    {
        ParkDescription();
        _park.Child = null;
        _description.Markdown = row.Task.Description;
        row.SetContent(_description);
        row.ShowActions(true);
        _descriptionRow = row;
    }

    private void ParkDescription()
    {
        _descriptionRow?.SetContent(null);
        _descriptionRow = null;
        _park.Child = _description;
    }

    // ---- Edit ----

    private void BeginEditOf(TaskRow row)
    {
        _header.ShowMessage(null);
        if (_edit?.Row == row)
        {
            _pending.CommitIfFor(row.Task.Id);
            return;
        }

        // A fresh edit starts from a clean stage. When that can't be made the edit doesn't open, and nothing else changes.
        if (_cleanup.BeginFresh(row.Task.Id))
        {
            BeginEdit(row, row.Task, DraftOf(row.Task));
        }
    }

    private static TaskDraft DraftOf(TaskItem task) => new(task.Id, task.Title, task.Description, task.Tags);

    private void BeginEdit(TaskRow row, TaskItem started, TaskDraft draft)
    {
        var editor = new TaskEditorControl(_tasks, _palette, _theme, _time, _clipboard, _imageFiles, _files) { Value = draft };
        editor.SaveRequested += (_, _) => SaveEdit();
        editor.SaveAndClearRequested += (_, _) => SaveEdit();
        editor.CancelRequested += (_, _) =>
        {
            var editing = _edit!.Row;
            LeaveEdit();
            ShowDescription(editing);
            Refresh();
        };
        Attach(new Edit(row, editor, started));
    }

    // Shows the editor in the row. Undo uses it again with the same editor, so text typed and not yet added stays.
    private void Attach(Edit edit)
    {
        if (_expandedId != edit.Started.Id)
        {
            Collapse();
            _expandedId = edit.Started.Id;
        }

        ParkDescription();
        _edit = edit;
        edit.Row.ShowActions(false);
        edit.Row.SetContent(edit.Editor);
        edit.Editor.FocusTitle();
    }

    private void SaveEdit()
    {
        var edit = _edit!;
        TaskItem? saved = null;
        if (!edit.Editor.Save(draft => saved = _tasks.SaveEdit(edit.Started, draft)))
        {
            return;
        }

        _edit = null;
        if (saved is not null)
        {
            edit.Row.Update(saved);
        }

        ShowDescription(edit.Row);
        Refresh();
    }

    // Closing the editor without saving: a clean one throws its images away; a dirty one can be brought back with Undo.
    private void LeaveEdit()
    {
        var edit = _edit!;
        _edit = null;
        edit.Row.SetContent(null);
        _cleanup.Leave(edit.Started.Id, edit.Editor.HasChanges, () => RestoreEdit(edit));
    }

    // Undo brings the edit back in a row even when a filter now hides the task, so nothing typed is lost.
    private void RestoreEdit(Edit edit)
    {
        if (!_rows.TryGetValue(edit.Started.Id, out var row))
        {
            row = RowFor(edit.Started);
        }

        Attach(edit with { Row = row });
        Refresh();
    }

    // ---- Delete, resolve, reopen ----

    private void Delete(TaskRow row)
    {
        _header.ShowMessage(null);
        var id = row.Task.Id;
        if (_edit?.Started.Id == id)
        {
            _edit = null;
        }

        if (_expandedId == id)
        {
            ClearExpanded();
        }

        _deletion.Start(id);
    }

    private void Resolve(TaskRow row, bool resolved)
    {
        _header.ShowMessage(null);
        if (!_actions.Resolve(row.Task.Id, resolved))
        {
            row.Update(row.Task);
        }
    }

    // ---- Order ----

    private void Nudge(TaskRow row, int step)
    {
        _header.ShowMessage(null);
        if (ListOrder.Nudge(_snapshot.Open, _snapshot.Resolved, row.Task.Id, step) is { } move)
        {
            _actions.Move(move);
        }
    }

    private void DropOn(TaskRow target, TaskItem dropped, bool lowerHalf)
    {
        _header.ShowMessage(null);
        if (ListOrder.Drop(_snapshot.Open, _snapshot.Resolved, target.Task.Id, dropped.Id, lowerHalf) is { } move)
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

        Refresh();
        if (_rows.TryGetValue(id, out var row) && _expandedId != id)
        {
            Expand(row);
        }
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
        if (modifiers == ModifierKeys.Control && key == Key.F && !InEditor(e.OriginalSource))
        {
            _header.FocusSearch();
            e.Handled = true;
        }
        else if (e.OriginalSource is TaskRow row && RowKeys.ActionFor(modifiers, key) is { } action)
        {
            _rowActions[action](row);
            e.Handled = true;
        }
    }

    private async void SyncOnKey() => await _sync.SyncAsync();

    private bool InEditor(object source) =>
        _edit is { } edit && source is DependencyObject element && (edit.Editor == element || edit.Editor.IsAncestorOf(element));
}
