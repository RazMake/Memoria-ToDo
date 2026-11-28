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
        Cursor = Cursors.Hand,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 0, CheckBoxGap, 0),
        Padding = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
    };
    private readonly FormattedTitle _title;
    private readonly FirstLineAlignment _alignment;
    private readonly PressDrag _press = new();
    private readonly IDragPointer _pointer;
    private bool _updating;
    private bool _selected;
    private UIElement? _editor;

    public TaskRow(TaskItem task, ITheme theme, IDefaultBrowser browser, IDragPointer pointer, Serilog.ILogger logger)
    {
        _pointer = pointer;
        Task = task;
        Group = [];
        GroupKey = new GroupKey(null, false);
        Focusable = true;
        FocusVisualStyle = null;
        Padding = new Thickness(8);
        BorderThickness = new Thickness(0, 0, 0, 1);
        SetResourceReference(BorderBrushProperty, "CardStrokeColorDefaultBrush");
        AutomationProperties.SetAutomationId(this, "Row");

        _title = new FormattedTitle(theme, browser, pointer, logger) { TextWrapping = TextWrapping.Wrap };
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
        MouseLeave += (_, _) => _press.Leave();
        Show(task, task.Title, false, [], GroupKey);
    }

    /// <summary>The task as the row shows it now.</summary>
    public TaskItem Task { get; private set; }

    /// <summary>The tasks of the group the row is in, in the order the group shows them.</summary>
    public IReadOnlyList<TaskItem> Group { get; private set; }

    /// <summary>The key of the group the row is in.</summary>
    public GroupKey GroupKey { get; private set; }

    /// <summary>Makes the card a mention in the title shows while the pointer rests on it; null from it shows none. Unset, none shows.</summary>
    public Func<Mention, FrameworkElement?>? MentionCard
    {
        get => _title.MentionCard;
        set => _title.MentionCard = value;
    }

    /// <summary>Raised when the user selects the task: a left click on the row, or a right-click.</summary>
    public event EventHandler? SelectRequested;

    public event EventHandler? EditRequested;

    public event EventHandler? EditTitleRequested;

    public event EventHandler? DeleteRequested;

    /// <summary>Raised on Ctrl+F in the row's menu, which closes first, since the menu keeps its keys from the window.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Raised with true when the user ticks the checkbox, and false when they untick it.</summary>
    public event EventHandler<bool>? ResolveToggled;

    /// <summary>Raised with the address of a clicked title link that isn't a web link.</summary>
    public event EventHandler<string>? LinkClicked;

    public event EventHandler<Exception>? OpenFailed;

    /// <summary>Shows the task's title as <paramref name="title"/> (its mentions' live text), its resolved state, whether it is the selected one, and the group it is in.</summary>
    public void Show(TaskItem task, string title, bool selected, IReadOnlyList<TaskItem> group, GroupKey groupKey)
    {
        Task = task;
        Group = group;
        GroupKey = groupKey;
        _selected = selected;
        if (_title.Markdown != title)
        {
            _title.Markdown = title;
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
    public void Revert() => Show(Task, _title.Markdown, _selected, Group, GroupKey);

    private ContextMenu MakeMenu()
    {
        var editTitle = new MenuItem { Header = "Edit Title", InputGestureText = ShortcutText.Of(ModifierKeys.Shift, Key.F2) };
        editTitle.Click += (_, _) => EditTitleRequested?.Invoke(this, EventArgs.Empty);
        var edit = new MenuItem { Header = "Edit Description", InputGestureText = "F2" };
        edit.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        var delete = new MenuItem { Header = "Delete", InputGestureText = "Del" };
        delete.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        var menu = new ContextMenu { Items = { editTitle, edit, delete } };
        // The menu is a popup, so its keys never reach the window, which tracks the held modifiers for its own keys.
        var held = new HeldModifierKeys();
        menu.PreviewKeyDown += (_, e) =>
        {
            var key = EventKey.Of(e);
            if (ModifierKey.Of(key) != ModifierKeys.None)
            {
                held.Pressed(key);
                return;
            }

            held.OtherKeyReached();
            if (held.Modifiers == ModifierKeys.Control && key == Key.F)
            {
                e.Handled = true;
                menu.IsOpen = false;
                SearchRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        menu.PreviewKeyUp += (_, e) => held.Released(EventKey.Of(e));
        menu.Closed += (_, _) => held.Clear();
        return menu;
    }

    /// <summary>
    /// Puts the Title editor in place of the title, or puts the title back when <paramref name="editor"/> is null. While it is
    /// there, the checkbox is off and a press or drag on the row does nothing, so the editor keeps the keyboard focus.
    /// </summary>
    public void ShowEditor(UIElement? editor)
    {
        if (_editor == editor)
        {
            return;
        }

        var grid = (Grid)Child;
        if (_editor is not null)
        {
            grid.Children.Remove(_editor);
        }

        _editor = editor;
        if (editor is not null)
        {
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
        }

        // The title stays in the grid, so the row keeps the title as its name.
        _title.Visibility = editor is null ? Visibility.Visible : Visibility.Collapsed;
        _resolved.IsEnabled = editor is null;
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
        if (_editor is not null)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            _press.Press(_pointer.PositionIn(this));
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
        if (_editor is not null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (!_press.Release() || e.OriginalSource is not DependencyObject source || !IsPlain(source))
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
        if (_editor is null && e.OriginalSource is DependencyObject source && IsPlain(source)
            && _press.Move(_pointer.PositionIn(this), _pointer.IsLeftButtonDown))
        {
            _pointer.StartDrag(this, new DraggedTask(Task, GroupKey).ToData());
        }
    }
}
