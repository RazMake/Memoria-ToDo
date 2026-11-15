using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// One task in a group of the left side: a checkbox and the formatted title, and nothing else. A click on the row, except on its
/// checkbox or a link, selects the task, and so does a right-click, which also opens the row's menu. Reports what the user asks
/// for; the window decides what happens.
/// </summary>
internal sealed class TaskRow : Border
{
    private const double CheckBoxGap = 8;

    private readonly CheckBox _resolved = new()
    {
        Focusable = false,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 0, CheckBoxGap, 0),
        Padding = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
    };
    private readonly FormattedTitle _title;
    private readonly FirstLineAlignment _alignment;
    private Point? _pressedAt;
    private bool _updating;
    private bool _selected;

    public TaskRow(TaskItem task, ITheme theme, IDefaultBrowser browser)
    {
        Task = task;
        Group = [];
        Focusable = true;
        AllowDrop = true;
        Padding = new Thickness(8);
        BorderThickness = new Thickness(0, 0, 0, 1);
        SetResourceReference(BorderBrushProperty, "CardStrokeColorDefaultBrush");
        AutomationProperties.SetAutomationId(this, "Row");

        _title = new FormattedTitle(theme, browser) { TextWrapping = TextWrapping.Wrap, Cursor = Cursors.Hand };
        _alignment = new FirstLineAlignment(_title, _resolved, CheckBoxGap);
        // The row is named by the title's own name, which it sets as it renders.
        SetBinding(AutomationProperties.NameProperty,
            new System.Windows.Data.Binding { Source = _title, Path = new PropertyPath(AutomationProperties.NameProperty), Mode = System.Windows.Data.BindingMode.OneWay });
        _title.OtherLinkClicked += (_, address) => LinkClicked?.Invoke(this, address);
        _title.OpenFailed += (_, failure) => OpenFailed?.Invoke(this, failure);
        _resolved.Checked += (_, _) => Toggled(true);
        _resolved.Unchecked += (_, _) => Toggled(false);
        ContextMenu = MakeMenu();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(_title, 1);
        grid.Children.Add(_resolved);
        grid.Children.Add(_title);
        Child = grid;

        PreviewMouseDown += OnPreviewMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        DragOver += OnDragOver;
        Drop += OnDrop;
        Show(task, false, []);
    }

    /// <summary>The task as the row shows it now.</summary>
    public TaskItem Task { get; private set; }

    /// <summary>The tasks of the group the row is in, in the order the group shows them.</summary>
    public IReadOnlyList<TaskItem> Group { get; private set; }

    /// <summary>Raised when the user selects the task: a left click on the row, or a right-click.</summary>
    public event EventHandler? SelectRequested;

    public event EventHandler? EditRequested;

    public event EventHandler? DeleteRequested;

    /// <summary>Raised with true when the user ticks the checkbox, and false when they untick it.</summary>
    public event EventHandler<bool>? ResolveToggled;

    /// <summary>Raised with the address of a clicked title link that isn't a web link.</summary>
    public event EventHandler<string>? LinkClicked;

    public event EventHandler<Exception>? OpenFailed;

    /// <summary>Raised with what the drag carried and whether it was dropped on the row's lower half.</summary>
    public event EventHandler<(DraggedTask Dropped, bool LowerHalf)>? TaskDropped;

    /// <summary>Shows the task's current title and resolved state, whether it is the selected one, and the group it is in.</summary>
    public void Show(TaskItem task, bool selected, IReadOnlyList<TaskItem> group)
    {
        Task = task;
        Group = group;
        _selected = selected;
        if (_title.Markdown != task.Title)
        {
            _title.Markdown = task.Title;
            _alignment.Invalidate();
        }

        _updating = true;
        _resolved.IsChecked = task.Resolved is not null;
        _updating = false;
        AutomationProperties.SetItemStatus(this, selected ? "Selected" : "");
        if (selected)
        {
            SetResourceReference(BackgroundProperty, "SubtleFillColorSecondaryBrush");
        }
        else
        {
            // A background that is not null makes the whole row take the click.
            Background = Brushes.Transparent;
        }
    }

    /// <summary>Shows the checkbox as the task is, after a change of it could not be saved.</summary>
    public void Revert() => Show(Task, _selected, Group);

    private ContextMenu MakeMenu()
    {
        var edit = new MenuItem { Header = "Edit Description", InputGestureText = "F2" };
        edit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        var delete = new MenuItem { Header = "Delete", InputGestureText = "Del" };
        delete.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        return new ContextMenu { Items = { edit, delete } };
    }

    private void Toggled(bool resolved)
    {
        if (!_updating)
        {
            ResolveToggled?.Invoke(this, resolved);
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _pressedAt = e.GetPosition(this);
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            Focus();
            SelectRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // A click on the row itself or its title, not on the checkbox or a link inside it.
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source || !IsPlain(source))
        {
            return;
        }

        Focus();
        SelectRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool IsPlain(DependencyObject source)
    {
        for (var current = source; current is not null && current != this; current = ParentOf(current))
        {
            if (current is ButtonBase or Hyperlink)
            {
                return false;
            }
        }

        return true;
    }

    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressedAt is not { } from || e.OriginalSource is not DependencyObject source || !IsPlain(source))
        {
            return;
        }

        var now = e.GetPosition(this);
        if (Math.Abs(now.X - from.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(now.Y - from.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressedAt = null;
        DragDrop.DoDragDrop(this, new DraggedTask(Task, Group).ToData(), DragDropEffects.Move);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DraggedTask.IsIn(e.Data) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DraggedTask.From(e.Data) is not { } dropped)
        {
            return;
        }

        e.Handled = true;
        TaskDropped?.Invoke(this, (dropped, e.GetPosition(this).Y > ActualHeight / 2));
    }
}
