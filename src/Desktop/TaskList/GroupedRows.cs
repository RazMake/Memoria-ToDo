using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The left side's content: a heading and the rows of each tag group, then the "N resolved" button and, when chosen, the
/// resolved tasks in one list. A task in two groups has a row in each. Rows and headings are kept between shows, so the
/// keyboard focus stays on a row while the list changes around it.
/// </summary>
internal sealed class GroupedRows : StackPanel
{
    private readonly ITagColors _tagColors;
    private readonly ITheme _theme;
    private readonly Func<TaskItem, TaskRow> _createRow;
    private readonly Button _resolvedButton = new() { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 8) };
    private readonly Dictionary<RowKey, TaskRow> _rows = [];
    private readonly Dictionary<string, GroupHeading> _headings = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TaskRow> _shown = [];
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

    /// <summary>Shows what the snapshot holds: the open tasks in groups, and the resolved ones when chosen.</summary>
    /// <param name="onlyTag">The tag the filter chose, which leaves only its group, or null.</param>
    /// <param name="isSelected">Whether a task is the selected one, which every row of it shows.</param>
    /// <param name="editedId">The task being edited, which keeps its rows when the tag filter leaves it out, or null.</param>
    public void Show(ListSnapshot snapshot, string? onlyTag, string? editedId, Func<string, bool> isSelected)
    {
        var order = new List<UIElement>();
        var shown = new List<TaskRow>();
        var used = new HashSet<RowKey>();

        foreach (var group in TagGroups.Of(snapshot.Open, onlyTag, editedId))
        {
            order.Add(HeadingOf(group.Tag));
            AddRows(group.Tasks, group.Tag, resolved: false);
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
            _rows.Remove(key);
        }

        _shown.Clear();
        _shown.AddRange(shown);
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
                    _rows[key] = row;
                }

                row.Show(task, isSelected(task.Id), tasks);
                used.Add(key);
                shown.Add(row);
                order.Add(row);
            }
        }
    }

    /// <summary>The row above (<paramref name="step"/> -1) or below (1) the row, across groups, or null at an end.</summary>
    public TaskRow? Neighbor(TaskRow row, int step)
    {
        var at = _shown.IndexOf(row) + step;
        return at >= 0 && at < _shown.Count ? _shown[at] : null;
    }

    /// <summary>The first row that shows the task, or null when none does.</summary>
    public TaskRow? FirstRowOf(string id) => _shown.FirstOrDefault(row => row.Task.Id == id);

    private GroupHeading HeadingOf(string? tag)
    {
        if (tag is null)
        {
            return _noTagHeading ??= new GroupHeading(null, _tagColors, _theme);
        }

        if (!_headings.TryGetValue(tag, out var heading))
        {
            heading = new GroupHeading(tag, _tagColors, _theme);
            _headings[tag] = heading;
        }

        return heading;
    }
}
