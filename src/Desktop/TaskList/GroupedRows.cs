using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The left side's content: a heading and the rows of each tag group, unless the group is collapsed, then the "N resolved"
/// button and, when chosen, the resolved tasks in one list. A task in two groups has a row in each. Rows and headings are
/// kept between shows, so the keyboard focus stays on a row while the list changes around it.
/// </summary>
internal sealed class GroupedRows : StackPanel
{
    private static readonly Thickness RowIndent = new(16, 0, 0, 0);

    private readonly ITagColors _tagColors;
    private readonly ITheme _theme;
    private readonly Func<TaskItem, TaskRow> _createRow;
    private readonly Button _resolvedButton = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 8) };
    private readonly Dictionary<RowKey, TaskRow> _rows = [];
    private readonly Dictionary<string, GroupHeading> _headings = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(TaskRow Row, RowKey Key)> _shown = [];
    private IReadOnlyList<PlannedGroup> _plan = [];
    private GroupHeading? _noTagHeading;

    private readonly record struct RowKey(string? Tag, bool Resolved, string Id);

    /// <param name="createRow">Makes the row of a task, with the window's handlers on it.</param>
    public GroupedRows(ITagColors tagColors, ITheme theme, Func<TaskItem, TaskRow> createRow)
    {
        _tagColors = tagColors;
        _theme = theme;
        _createRow = createRow;
        Margin = new Thickness(8, 0, 8, 8);
        _resolvedButton.Click += (_, _) => ResolvedClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user clicks the "N resolved" button.</summary>
    public event EventHandler? ResolvedClicked;

    /// <summary>Raised with the tag of a group whose heading the user clicked, or null for the "No tag" group.</summary>
    public event EventHandler<string?>? GroupToggled;

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

        _plan = GroupPlan.Of(TagGroups.Of(snapshot.Open), collapse);
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

                row.Show(task, isSelected(task.Id), tasks);
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

    /// <summary>The first row that shows the task, or null when none does.</summary>
    public TaskRow? FirstRowOf(string id) => _shown.Select(shown => shown.Row).FirstOrDefault(row => row.Task.Id == id);

    /// <summary>Whether the row the title editor was opened in is showing, which a collapsed group or a removed row denies.</summary>
    public bool IsShown(RowPosition at) => RowAt(at) is not null;

    private TaskRow? LastOpenRow() =>
        _shown.LastOrDefault(shown => !shown.Key.Resolved).Row;

    // The open row of the task in the group, or else its resolved row, which has no group.
    private TaskRow? RowAt(RowPosition at) =>
        _shown.Where(shown => shown.Key.Id == at.TaskId
            && (shown.Key.Resolved || string.Equals(shown.Key.Tag, at.Tag, StringComparison.OrdinalIgnoreCase)))
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
        var heading = new GroupHeading(tag, _tagColors, _theme);
        heading.Clicked += (_, _) => GroupToggled?.Invoke(this, tag);
        return heading;
    }
}
