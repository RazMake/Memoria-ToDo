using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// The left side: each group's heading and rows, or the text that says why there are none. Rows and headings are kept while the list
/// stays the same, so a click, and the keyboard focus, aren't lost to a selection change. Every action goes to the model, then calls
/// <c>changed</c> so the window shows what the model now holds.
/// </summary>
internal sealed class ContactListView : ScrollViewer
{
    private readonly ContactsModel _model;
    private readonly IDragPointer _pointer;
    private readonly Action _changed;
    private readonly StackPanel _panel = new() { Margin = new Thickness(8, 0, 8, 8), AllowDrop = true, Background = Brushes.Transparent };
    private readonly TextBlock _text = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly DropLine _dropLine;
    private readonly List<(string Group, StackPanel Panel)> _groups = [];
    private readonly List<StackPanel> _rowLists = [];
    private readonly List<ContactRow> _rows = [];
    private readonly Action _pastEdgeRow;
    private IReadOnlyList<object> _shown = [];
    private IDataObject? _dragged;

    public ContactListView(ContactsModel model, TimeProvider time, IDragPointer pointer, Action changed, Action pastEdgeRow)
    {
        _model = model;
        _pointer = pointer;
        _changed = changed;
        _pastEdgeRow = pastEdgeRow;
        _dropLine = new DropLine(_panel);
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Content = _panel;
        PreviewKeyDown += OnKeyDown;
        _panel.DragOver += OnDragOver;
        _panel.DragLeave += (_, _) => EndDrag();
        _panel.Drop += OnDrop;
        AutoScroll.Attach(this, time, at =>
        {
            if (_dragged is { } data)
            {
                ShowLine(LandingAt(data, TranslatePoint(at, _panel).Y));
            }
        });
    }

    /// <summary>Shows what the model holds: the groups, or the text, and which row is selected.</summary>
    public void Show()
    {
        var structure = Structure();
        if (!structure.SequenceEqual(_shown))
        {
            _shown = structure;
            Rebuild();
        }

        foreach (var row in _rows)
        {
            row.MarkSelected(IsSelected(row));
        }

        // Tab stops once in each group's rows: at its selected row, or its first.
        foreach (var rows in _rowLists)
        {
            var listed = rows.Children.OfType<ContactRow>().ToList();
            var landing = listed.FirstOrDefault(IsSelected) ?? listed.FirstOrDefault();
            foreach (var row in listed)
            {
                KeyboardNavigation.SetIsTabStop(row, ReferenceEquals(row, landing));
            }
        }
    }

    private bool IsSelected(ContactRow row) => _model.Selected is { } selected && Same(selected, row.Listed);

    /// <summary>Selects the first row, or the last, and puts the focus on it. Does nothing when no row shows.</summary>
    public void SelectEdgeRow(bool last)
    {
        if (_rows.Count > 0)
        {
            SelectAndFocus(last ? _rows[^1] : _rows[0]);
        }
    }

    // What the list's look depends on, besides the selection: compared value by value.
    private List<object> Structure() =>
        [
            _model.ListText ?? "",
            .. _model.Groups.SelectMany(group => new object[] { (group.Name, group.Heading, group.CanAdd) }.Concat(group.Contacts)),
        ];

    private void Rebuild()
    {
        var focused = Keyboard.FocusedElement as ContactRow;
        _panel.Children.Clear();
        _groups.Clear();
        _rowLists.Clear();
        _rows.Clear();
        _text.Text = _model.ListText ?? "";
        if (_model.ListText is not null)
        {
            _panel.Children.Add(_text);
            return;
        }

        foreach (var group in _model.Groups)
        {
            var panel = new StackPanel();
            panel.Children.Add(new ContactGroupHeading(group, _pointer, () => _model.CanReorder, AddTo));
            var rows = new StackPanel();
            foreach (var listed in group.Contacts)
            {
                var row = NewRow(listed);
                _rows.Add(row);
                rows.Children.Add(row);
            }

            panel.Children.Add(rows);
            _rowLists.Add(rows);
            _groups.Add((group.Name, panel));
            _panel.Children.Add(panel);
        }

        // Only a focused row that is still selected keeps the focus: giving it back to one the selection left would select it again.
        if (focused is not null && IsSelected(focused) && ShownRowOf(focused.Listed) is { } again)
        {
            Keyboard.Focus(again);
        }
    }

    private static bool Same(ListedContact a, ListedContact b) => a.GroupName == b.GroupName && a.Contact.Id == b.Contact.Id;

    private ContactRow NewRow(ListedContact listed)
    {
        var add = new MenuItem { Header = "Add" };
        var delete = new MenuItem { Header = "Delete" };
        var menu = new ContextMenu();
        menu.Items.Add(add);
        menu.Items.Add(delete);
        var row = new ContactRow(listed, _model.CanAddTo(listed.GroupName) || _model.CanDeleteFrom(listed.GroupName) ? menu : null);
        add.Click += (_, _) => AddTo(row.Listed.GroupName);
        delete.Click += (_, _) => Run(() => _model.Delete());
        menu.Opened += (_, _) =>
        {
            var canAdd = _model.CanAddTo(row.Listed.GroupName);
            var canDelete = _model.Selected is { } selected && Same(selected, row.Listed) && _model.CanDelete;
            add.Visibility = canAdd ? Visibility.Visible : Visibility.Collapsed;
            delete.Visibility = canDelete ? Visibility.Visible : Visibility.Collapsed;
            if (!canAdd && !canDelete)
            {
                menu.IsOpen = false;
            }
        };
        row.MouseLeftButtonDown += (_, e) =>
        {
            SelectAndFocus(row);
            // Handled, so the list, a ScrollViewer, doesn't take the focus from the row and scroll on the arrow keys.
            e.Handled = true;
            if (e.ClickCount == 2)
            {
                Run(() => _model.BeginEdit());
            }
        };
        row.MouseRightButtonDown += (_, _) => SelectAndFocus(row);
        // Tab landing on a row the selection isn't on selects it, after the focus settles, so the focus always shows.
        // When the user keeps an open form instead, the focus goes to the row still selected, so it doesn't ask again.
        row.GotKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (row.IsKeyboardFocused && !IsSelected(row))
            {
                var moved = _model.Select(row.Listed.GroupName, row.Listed.Contact.Id);
                _changed();
                if (!moved && _model.Selected is { } kept && ShownRowOf(kept) is { } keptRow)
                {
                    Keyboard.Focus(keptRow);
                }
            }
        });
        return row;
    }

    private void AddTo(string groupName) => Run(() => _model.BeginAdd(groupName));

    private void Run(Func<bool> action)
    {
        action();
        _changed();
    }

    // Focuses the row only when the selection moved to it: a cancelled leave-form prompt keeps the focus where it was.
    private void SelectAndFocus(ContactRow row)
    {
        var moved = _model.Select(row.Listed.GroupName, row.Listed.Contact.Id);
        _changed();
        if (moved && ShownRowOf(row.Listed) is { } shown)
        {
            Keyboard.Focus(shown);
        }
    }

    private ContactRow? ShownRowOf(ListedContact listed) => _rows.FirstOrDefault(candidate => Same(candidate.Listed, listed));

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ContactRow row || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        switch (EventKey.Of(e))
        {
            case Key.F2:
                e.Handled = true;
                Run(() => _model.BeginEdit());
                break;
            case Key.Delete:
                e.Handled = true;
                Run(() => _model.Delete());
                break;
            case Key.Up or Key.Down:
                e.Handled = true;
                var next = _rows.IndexOf(row) + (EventKey.Of(e) == Key.Up ? -1 : 1);
                if (next >= 0 && next < _rows.Count)
                {
                    SelectAndFocus(_rows[next]);
                }
                else
                {
                    _pastEdgeRow();
                }

                break;
        }
    }

    // ---- Dragging a group's heading ----

    private void OnDragOver(object sender, DragEventArgs e)
    {
        _dragged = e.Data;
        var landing = LandingAt(e.Data, e.GetPosition(_panel).Y);
        ShowLine(landing);
        e.Effects = landing is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var landing = LandingAt(e.Data, e.GetPosition(_panel).Y);
        EndDrag();
        if (DraggedContactGroup.From(e.Data) is not { } dragged)
        {
            return;
        }

        e.Handled = true;
        if (landing is { } at)
        {
            Run(() => _model.MoveGroup(dragged.GroupName, at.Before));
        }
    }

    private void EndDrag()
    {
        _dragged = null;
        _dropLine.Hide();
    }

    private void ShowLine((double LineY, string? Before)? landing)
    {
        if (landing is { } at)
        {
            _dropLine.Show(0, at.LineY, _panel.ActualWidth);
        }
        else
        {
            _dropLine.Hide();
        }
    }

    // A group, heading and rows, is one item: the pointer in its upper half drops before it, in its lower half before the next group.
    private (double LineY, string? Before)? LandingAt(IDataObject data, double y)
    {
        if (DraggedContactGroup.From(data) is null || !_model.CanReorder || _groups.Count == 0)
        {
            return null;
        }

        var at = _groups.FindIndex(group => y < Bottom(group.Panel));
        var index = at < 0 ? _groups.Count - 1 : at;
        var panel = _groups[index].Panel;
        var top = panel.TranslatePoint(new Point(0, 0), _panel).Y;
        if (y < top + panel.ActualHeight / 2)
        {
            return (top, _groups[index].Group);
        }

        return (top + panel.ActualHeight, index + 1 < _groups.Count ? _groups[index + 1].Group : null);
    }

    private double Bottom(StackPanel panel) => panel.TranslatePoint(new Point(0, 0), _panel).Y + panel.ActualHeight;
}
