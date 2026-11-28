using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The left side's content: a heading and the rows of each tag group, unless the group is collapsed, then the "N resolved"
/// button and, when chosen, the resolved tasks in one list. A task shows in one group only. Rows and headings are
/// kept between shows, so the keyboard focus stays on a row while the list changes around it.
/// </summary>
internal sealed class GroupedRows : StackPanel
{
    private static readonly Thickness RowIndent = new(16, 0, 0, 0);

    private readonly ITagColors _tagColors;
    private readonly ITheme _theme;
    private readonly ITagOrder _tagOrder;
    private readonly Func<TaskItem, TaskRow> _createRow;
    private readonly Func<TaskItem, string> _titleOf;
    private readonly IDragPointer _pointer;
    private readonly Button _resolvedButton = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 8) };
    private readonly Dictionary<RowKey, TaskRow> _rows = [];
    private readonly Dictionary<string, GroupHeading> _headings = new(StringComparer.Ordinal);
    private readonly List<(TaskRow Row, RowKey Key)> _shown = [];
    private readonly DropLine _dropLine;
    private IReadOnlyList<PlannedGroup> _plan = [];
    private IReadOnlyList<TaskItem> _resolvedTasks = [];
    private bool _resolvedShown;
    private bool _hasResolved;
    private IDataObject? _dragged;
    private GroupHeading? _noTagHeading;

    private readonly record struct RowKey(string? Tag, bool Resolved, string Id);

    /// <summary>What a drop finds under the pointer: where its line goes, and what taking the drop does.</summary>
    private readonly record struct Landing(double LineX, double LineY, Action Take);

    /// <param name="tagOrder">Puts the groups in the user's order.</param>
    /// <param name="createRow">Makes the row of a task, with the window's handlers on it.</param>
    /// <param name="titleOf">The title text a task's row shows, with its mentions' live text.</param>
    /// <param name="pointer">The mouse the group headings drag with.</param>
    public GroupedRows(ITagColors tagColors, ITheme theme, ITagOrder tagOrder, Func<TaskItem, TaskRow> createRow, Func<TaskItem, string> titleOf, IDragPointer pointer)
    {
        _tagColors = tagColors;
        _theme = theme;
        _tagOrder = tagOrder;
        _createRow = createRow;
        _titleOf = titleOf;
        _pointer = pointer;
        Margin = new Thickness(8, 0, 8, 8);
        // A background that is not null makes the gaps between the items take the drag too.
        Background = Brushes.Transparent;
        AllowDrop = true;
        _dropLine = new DropLine(this);
        _resolvedButton.Click += (_, _) => ResolvedClicked?.Invoke(this, EventArgs.Empty);
        DragOver += OnDragOver;
        DragLeave += (_, _) =>
        {
            _dragged = null;
            _dropLine.Hide();
        };
        Drop += OnDrop;
    }

    /// <summary>Raised when the user clicks the "N resolved" button.</summary>
    public event EventHandler? ResolvedClicked;

    /// <summary>Raised with the tag of a group whose heading the user clicked, or null for the "No tag" group.</summary>
    public event EventHandler<string?>? GroupToggled;

    /// <summary>
    /// Raised when the user drops a task in a gap among the rows of <c>TargetRows</c>, the rows of the group <c>Target</c>, which it was dragged from: before the row
    /// at <c>Index</c>, or at the end when the index is the row count.
    /// </summary>
    public event EventHandler<(DraggedTask Dropped, GroupKey Target, IReadOnlyList<TaskItem> TargetRows, int Index)>? TaskDropped;

    /// <summary>Raised when the user drops a group in a gap among the groups.</summary>
    public event EventHandler<GroupMove>? GroupDropped;

    /// <summary>
    /// Follows the pointer of a drag that is over the list after the list scrolled under it, so the drop line stays where a drop would land.
    /// </summary>
    /// <param name="pointer">The pointer's place in the list.</param>
    public void ContinueDrag(Point pointer)
    {
        if (_dragged is { } data)
        {
            ShowLine(LandingAt(data, pointer.Y));
        }
    }

    /// <summary>Shows what the snapshot holds: the open tasks in groups, and the resolved ones when chosen.</summary>
    /// <param name="collapse">Which groups show no rows.</param>
    /// <param name="editedAt">The row a title editor goes in, or null. When that row isn't showing, no row has the editor.</param>
    /// <param name="rowEditor">The title editor, or null when no row has one.</param>
    /// <param name="isSelected">Whether a task is the selected one, which every row of it shows.</param>
    public void Show(ListSnapshot snapshot, GroupCollapse collapse, RowPosition? editedAt, UIElement? rowEditor, Func<string, bool> isSelected)
    {
        var order = new List<UIElement>();
        var shown = new List<(TaskRow Row, RowKey Key)>();
        var used = new HashSet<RowKey>();

        _plan = GroupPlan.Of(TagGroups.Of(snapshot.Open, _tagOrder), collapse);
        _resolvedTasks = snapshot.Resolved;
        _resolvedShown = snapshot.ShowResolved && snapshot.ResolvedCount > 0;
        _hasResolved = snapshot.ResolvedCount > 0;
        foreach (var group in _plan)
        {
            var heading = HeadingOf(group.Tag);
            heading.Show(group.CountText);
            order.Add(heading);
            AddRows(group.Rows, group.Tag, resolved: false);
        }

        _resolvedButton.Content = $"{snapshot.ResolvedCount} resolved";
        AutomationProperties.SetName(_resolvedButton, $"{snapshot.ResolvedCount} resolved");
        if (snapshot.ResolvedCount > 0)
        {
            order.Add(_resolvedButton);
            if (snapshot.ShowResolved)
            {
                AddRows(snapshot.Resolved, null, resolved: true);
            }
        }

        foreach (var key in _rows.Keys.Where(key => !used.Contains(key)).ToList())
        {
            _rows[key].ShowEditor(null);
            _rows.Remove(key);
        }

        _shown.Clear();
        _shown.AddRange(shown);
        ShowEditor(editedAt, rowEditor);
        if (!Children.Cast<UIElement>().SequenceEqual(order))
        {
            var focused = Keyboard.FocusedElement;
            Children.Clear();
            foreach (var element in order)
            {
                Children.Add(element);
            }

            if (focused is not null)
            {
                Keyboard.Focus(focused);
            }
        }

        void AddRows(IReadOnlyList<TaskItem> tasks, string? tag, bool resolved)
        {
            foreach (var task in tasks)
            {
                var key = new RowKey(tag?.ToUpperInvariant(), resolved, task.Id);
                if (!_rows.TryGetValue(key, out var row))
                {
                    row = _createRow(task);
                    row.Margin = RowIndent;
                    _rows[key] = row;
                }

                row.Show(task, _titleOf(task), isSelected(task.Id), tasks, new GroupKey(tag, resolved));
                used.Add(key);
                shown.Add((row, key));
                order.Add(row);
            }
        }
    }

    /// <summary>The row above (<paramref name="step"/> -1) or below (1) the row, across groups, or null at an end.</summary>
    /// <remarks>The rows of a collapsed group are skipped, and the resolved rows follow the groups.</remarks>
    public TaskRow? Neighbor(TaskRow row, int step)
    {
        var key = _shown.FirstOrDefault(shown => shown.Row == row).Key;
        if (key == default)
        {
            return null;
        }

        var resolved = _shown.Where(shown => shown.Key.Resolved).Select(shown => shown.Row).ToList();
        if (key.Resolved)
        {
            var at = resolved.IndexOf(row) + step;
            return at >= 0 && at < resolved.Count ? resolved[at] : step < 0 ? LastOpenRow() : null;
        }

        return GroupPlan.Neighbor(_plan, new RowPosition(key.Tag, key.Id), step) is { } next
            ? _rows[new RowKey(next.Tag?.ToUpperInvariant(), false, next.TaskId)]
            : step > 0 ? resolved.FirstOrDefault() : null;
    }

    /// <summary>The place of an open row in the list, or null for a resolved row or one not showing.</summary>
    public RowPosition? PositionOf(TaskRow row)
    {
        var key = _shown.FirstOrDefault(shown => shown.Row == row).Key;
        return key == default || key.Resolved ? null : new RowPosition(key.Tag, key.Id);
    }

    /// <summary>The top row of the list, or null when no row shows.</summary>
    public TaskRow? FirstRow => _shown.Count == 0 ? null : _shown[0].Row;

    /// <summary>The bottom row of the list, or null when no row shows.</summary>
    public TaskRow? LastRow => _shown.Count == 0 ? null : _shown[^1].Row;

    /// <summary>The first row that shows the task, or null when none does.</summary>
    public TaskRow? FirstRowOf(string id) => _shown.Select(shown => shown.Row).FirstOrDefault(row => row.Task.Id == id);

    /// <summary>Whether the row the title editor was opened in is showing, which a collapsed group or a removed row denies.</summary>
    public bool IsShown(RowPosition at) => RowAt(at) is not null;

    private TaskRow? LastOpenRow() =>
        _shown.LastOrDefault(shown => !shown.Key.Resolved).Row;

    // The open row of the task in the group, or else its resolved row, which has no group.
    private TaskRow? RowAt(RowPosition at) =>
        _shown.Where(shown => shown.Key.Id == at.TaskId
            && (shown.Key.Resolved || string.Equals(shown.Key.Tag, at.Tag, StringComparison.Ordinal)))
            .Select(shown => shown.Row).FirstOrDefault();

    // Puts the title editor in the row the user opened it in. When that row isn't showing, no row has the editor.
    private void ShowEditor(RowPosition? editedAt, UIElement? editor)
    {
        var target = editedAt is { } at && editor is not null ? RowAt(at) : null;
        // The editor leaves its old row before it enters the new one, as an element has one parent.
        foreach (var (row, _) in _shown.Where(shown => shown.Row != target))
        {
            row.ShowEditor(null);
        }

        target?.ShowEditor(editor);
    }

    private GroupHeading HeadingOf(string? tag)
    {
        if (tag is null)
        {
            return _noTagHeading ??= NewHeading(null);
        }

        if (!_headings.TryGetValue(tag, out var heading))
        {
            heading = NewHeading(tag);
            _headings[tag] = heading;
        }

        return heading;
    }

    private GroupHeading NewHeading(string? tag)
    {
        var heading = new GroupHeading(tag, _tagColors, _theme, _pointer);
        heading.Clicked += (_, _) => GroupToggled?.Invoke(this, tag);
        return heading;
    }

    // ---- Drag and drop ----

    private void OnDragOver(object sender, DragEventArgs e)
    {
        _dragged = e.Data;
        var landing = LandingAt(e.Data, e.GetPosition(this).Y);
        ShowLine(landing);
        e.Effects = landing is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var landing = LandingAt(e.Data, e.GetPosition(this).Y);
        _dragged = null;
        _dropLine.Hide();
        if (landing is null && !DraggedTask.IsIn(e.Data) && !DraggedGroup.IsIn(e.Data))
        {
            return;
        }

        e.Handled = true;
        landing?.Take();
    }

    private void ShowLine(Landing? landing)
    {
        if (landing is { } at)
        {
            _dropLine.Show(at.LineX, at.LineY, ActualWidth - at.LineX);
        }
        else
        {
            _dropLine.Hide();
        }
    }

    // The one rule for the line and the drop: the gap under the pointer, and what dropping in it does.
    private Landing? LandingAt(IDataObject data, double y)
    {
        if (DraggedGroup.From(data) is { } group)
        {
            var layout = Layout();
            return DropTargets.ForGroup(layout, group.Tag, y) is { } gap && GroupMoves.Of([.. _plan.Select(planned => planned.Tag)], group.Tag, gap) is { } move
                ? new Landing(0, DropTargets.LineY(layout, gap, GroupHeading.Gap), () => GroupDropped?.Invoke(this, move))
                : null;
        }

        if (DraggedTask.From(data) is { } task && IsShowing(task.Source))
        {
            var layout = Layout();
            return DropTargets.ForTask(layout, task.Source.Tag, task.Task.Id, task.Source.Resolved, y) is { } gap && TargetRows(gap) is { } rows
                ? new Landing(RowIndent.Left, DropTargets.LineY(layout, gap), () => TaskDropped?.Invoke(this, (task, new GroupKey(gap.Tag, gap.Resolved), rows, gap.Index)))
                : null;
        }

        return null;
    }

    // Whether the group a dragged task came from still shows, found by its key so a drag survives a refresh of the list.
    private bool IsShowing(GroupKey key) =>
        key.Resolved ? _resolvedShown : _plan.Any(group => new GroupKey(group.Tag, false) == key);

    private IReadOnlyList<TaskItem>? TargetRows(TaskGap gap) =>
        gap.Resolved ? _resolvedTasks : _plan.FirstOrDefault(group => string.Equals(group.Tag, gap.Tag, StringComparison.Ordinal))?.Rows;

    // Where the items sit now, in this panel's coordinates; the groups' touching spans include the space above each heading.
    private ListLayout Layout()
    {
        var groups = new List<GroupSpan>(_plan.Count);
        foreach (var group in _plan)
        {
            var heading = HeadingOf(group.Tag);
            var top = Top(heading) - heading.Margin.Top;
            var rows = group.Rows.Select(task => SpanOf(_rows[new RowKey(group.Tag?.ToUpperInvariant(), false, task.Id)], task.Id)).ToList();
            var bottom = rows.Count > 0 ? rows[^1].Bottom : Top(heading) + heading.ActualHeight + heading.Margin.Bottom;
            groups.Add(new GroupSpan(group.Tag, top, bottom, rows));
        }

        var resolvedRows = _resolvedShown
            ? _resolvedTasks.Select(task => SpanOf(_rows[new RowKey(null, true, task.Id)], task.Id)).ToList()
            : [];
        return new ListLayout(groups, _hasResolved ? Top(_resolvedButton) - _resolvedButton.Margin.Top : null, resolvedRows);
    }

    private double Top(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), this).Y;

    private RowSpan SpanOf(TaskRow row, string id) => new(id, Top(row), Top(row) + row.ActualHeight);
}
