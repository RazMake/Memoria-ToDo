using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// One task in the list: a checkbox, the formatted title and tags, and below them the slot that holds the description
/// or the editor, and the buttons for the task. Reports what the user asks for; the window decides what happens.
/// </summary>
internal sealed class TaskRow : Border
{
    private readonly CheckBox _resolved = new() { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 8, 0) };
    private readonly FormattedTitle _title;
    private readonly TextBlock _tags = new() { Opacity = 0.7, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _slot = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button _edit = Labeled("Edit");
    private readonly Button _delete = Labeled("Delete");
    private readonly Button _reopen = Labeled("Reopen");
    private bool _openAndNotEditing;
    private Point? _pressedAt;
    private bool _updating;

    public TaskRow(TaskItem task, IReadOnlyList<PaletteColor> palette, ITheme theme, IDefaultBrowser browser)
    {
        Task = task;
        Focusable = true;
        AllowDrop = true;
        Padding = new Thickness(8);
        BorderThickness = new Thickness(0, 0, 0, 1);
        SetResourceReference(BorderBrushProperty, "CardStrokeColorDefaultBrush");
        AutomationProperties.SetAutomationId(this, "Row");

        _title = new FormattedTitle(palette, theme, browser) { TextWrapping = TextWrapping.Wrap };
        // The row is named by the title's own name, which it sets as it renders.
        SetBinding(AutomationProperties.NameProperty,
            new System.Windows.Data.Binding { Source = _title, Path = new PropertyPath(AutomationProperties.NameProperty), Mode = System.Windows.Data.BindingMode.OneWay });
        _title.OtherLinkClicked += (_, address) => LinkClicked?.Invoke(this, address);
        _title.OpenFailed += (_, failure) => OpenFailed?.Invoke(this, failure);
        _edit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        _delete.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        _reopen.Click += (_, _) => ResolveToggled?.Invoke(this, false);
        _buttons.Children.Add(_edit);
        _buttons.Children.Add(_delete);
        _buttons.Children.Add(_reopen);
        _resolved.Checked += (_, _) => Toggled(true);
        _resolved.Unchecked += (_, _) => Toggled(false);

        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_tags, Dock.Right);
        header.Children.Add(_tags);
        header.Children.Add(_title);
        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(_slot);
        content.Children.Add(_buttons);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(content, 1);
        grid.Children.Add(_resolved);
        grid.Children.Add(content);
        Child = grid;

        MouseUp += OnMouseUp;
        PreviewMouseLeftButtonDown += (_, e) => _pressedAt = e.GetPosition(this);
        MouseMove += OnMouseMove;
        DragOver += OnDragOver;
        Drop += OnDrop;
        Update(task);
    }

    /// <summary>The task as the row shows it now.</summary>
    public TaskItem Task { get; private set; }

    /// <summary>The place the description or the editor shows in.</summary>
    public Border Slot => _slot;

    public event EventHandler? ClickRequested;

    public event EventHandler? EditRequested;

    public event EventHandler? DeleteRequested;

    /// <summary>Raised with true when the user ticks the checkbox, and false when they untick it or press Reopen.</summary>
    public event EventHandler<bool>? ResolveToggled;

    /// <summary>Raised with the address of a clicked title link that isn't a web link.</summary>
    public event EventHandler<string>? LinkClicked;

    public event EventHandler<Exception>? OpenFailed;

    /// <summary>Raised with the dragged task and whether it was dropped on the row's lower half.</summary>
    public event EventHandler<(TaskItem Dropped, bool LowerHalf)>? TaskDropped;

    /// <summary>Shows the task's current title, tags, and resolved state.</summary>
    public void Update(TaskItem task)
    {
        Task = task;
        _title.Markdown = task.Title;
        _tags.Text = string.Join(", ", task.Tags);
        _tags.Visibility = task.Tags.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _updating = true;
        _resolved.IsChecked = task.Resolved is not null;
        _updating = false;
        _reopen.Visibility = task.Resolved is null ? Visibility.Collapsed : Visibility.Visible;
        ShowActions(_openAndNotEditing);
    }

    /// <summary>Shows Edit and Delete while the row is the open one and not being edited. Reopen shows whenever the task is resolved.</summary>
    public void ShowActions(bool openAndNotEditing)
    {
        var visibility = openAndNotEditing ? Visibility.Visible : Visibility.Collapsed;
        _edit.Visibility = visibility;
        _delete.Visibility = visibility;
        _buttons.Visibility = openAndNotEditing || Task.Resolved is not null ? Visibility.Visible : Visibility.Collapsed;
        _openAndNotEditing = openAndNotEditing;
    }

    /// <summary>Puts the content in the slot, or empties it.</summary>
    public void SetContent(UIElement? content)
    {
        _slot.Child = content;
        _slot.Visibility = content is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Toggled(bool resolved)
    {
        if (!_updating)
        {
            ResolveToggled?.Invoke(this, resolved);
        }
    }

    private static Button Labeled(string label)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 2, 10, 2) };
        AutomationProperties.SetName(button, label);
        return button;
    }

    // A click on the row itself or its title, not on a control or link inside it or on the content in the slot.
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source || !IsPlain(source))
        {
            return;
        }

        Focus();
        ClickRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool IsPlain(DependencyObject source)
    {
        for (var current = source; current is not null && current != this; current = ParentOf(current))
        {
            if (current is ButtonBase or Hyperlink || current == _slot)
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
        DragDrop.DoDragDrop(this, new DataObject(Task), DragDropEffects.Move);
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TaskItem)) is not TaskItem dropped)
        {
            return;
        }

        e.Handled = true;
        TaskDropped?.Invoke(this, (dropped, e.GetPosition(this).Y > ActualHeight / 2));
    }
}
