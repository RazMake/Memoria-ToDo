using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A task's tags as colored badges, each with a remove button, followed by a + drop-down button. The + opens a menu under
/// it with the eight most-used tags, Search, which lists the tags that match what is typed in a submenu beside it, and
/// Custom…. Custom opens below the badges: a New tag box and 30 colors, which show while the box holds text. A click on a
/// color keeps it for the tag and adds the tag; Enter adds the tag with no new color.
/// </summary>
public sealed class TagEditor : UserControl
{
    private readonly TagsModel _model;
    private readonly TagMenuModel _menuModel;
    private readonly BadgeList _badges;
    private readonly Button _plus = new() { Content = "+", Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 6) };
    private readonly TagMenu _menu;
    private readonly StackPanel _custom = new() { Visibility = Visibility.Collapsed };
    private readonly TextBox _input = new();
    private readonly ColorGrid _colors;
    private readonly TextBlock _message = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Firebrick,
        Visibility = Visibility.Collapsed,
    };
    private readonly KeyMap _inputKeys = new();
    private readonly KeyMap _plusKeys = new();
    private Window? _window;

    public TagEditor(ITasks tasks, ITagColors tagColors, ITheme theme)
        : this(tasks, new TagsModel(tasks, tagColors), new TagLook(tagColors, theme))
    {
    }

    internal TagEditor(ITasks tasks, TagsModel model, TagLook look)
    {
        _model = model;
        _menuModel = new TagMenuModel(tasks, model);
        _badges = new BadgeList(look, TagBadgeVisuals.Removable(look), model.Remove, _plus);
        _menu = new TagMenu(_menuModel, look, _plus);
        _colors = new ColorGrid(look) { Visibility = Visibility.Collapsed };
        AutomationProperties.SetAutomationId(_badges, "TagList");
        AutomationProperties.SetAutomationId(_plus, "AddTag");
        AutomationProperties.SetAutomationId(_input, "Tags");

        var label = new TextBlock { Text = "New tag:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var box = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(label, Dock.Left);
        box.Children.Add(label);
        box.Children.Add(_input);
        _custom.Children.Add(box);
        _custom.Children.Add(_colors);
        _custom.Children.Add(_message);
        var panel = new StackPanel();
        panel.Children.Add(_badges);
        panel.Children.Add(_menu);
        panel.Children.Add(_custom);
        Content = panel;

        model.TagAdded += tag => TagAdded?.Invoke(this, tag);
        model.TagRemoved += tag => TagRemoved?.Invoke(this, tag);
        model.Changed += ShowModel;
        look.Follow(this, () => _colors.Show(model.MarkedColor));

        _inputKeys
            .On(ModifierKeys.None, Key.Enter, () => model.AddTypedText())
            .Try(ModifierKeys.None, Key.Back, model.Backspace)
            .Try(ModifierKeys.None, Key.Tab, FocusColors);
        _plusKeys
            .Try(ModifierKeys.None, Key.Enter, OpenOrEnter)
            .Try(ModifierKeys.None, Key.Space, OpenOrEnter)
            .Try(ModifierKeys.None, Key.Down, OpenOrDown)
            .Try(ModifierKeys.None, Key.Up, MenuUp);

        _plus.Click += (_, _) =>
        {
            KeyboardFocus.MoveTo(_plus);
            _menuModel.Toggle();
        };
        _plus.PreviewKeyDown += (_, e) => e.Handled = _plusKeys.TryRun(Keyboard.Modifiers, e.Key);
        _plus.PreviewTextInput += (_, e) => e.Handled = e.Text.Length == 1 && _menuModel.TypeOutsideSearch(e.Text[0]);
        _colors.Chosen += color =>
        {
            model.AddTypedTextInColor(color);
            // Custom is hidden once the tag is added, so the cursor goes to +; it stays in Custom when the color couldn't be kept.
            KeyboardFocus.MoveTo(model.IsCustomOpen ? _input : _plus);
        };
        _input.PreviewKeyDown += (_, e) => e.Handled = _inputKeys.TryRun(Keyboard.Modifiers, e.Key);
        _input.TextChanged += (_, _) =>
        {
            if (model.TypedText != _input.Text)
            {
                model.TypedText = _input.Text;
            }
        };
        DataObject.AddPastingHandler(_input, TagText.OnPaste);
        _input.PreviewTextInput += OnInputText;
        // Esc closes an open part wherever the cursor is in the window, before the window's own Esc. A key an editor
        // around this control handled first arrives handled and is left alone. A click outside the menu closes it, and
        // so does hiding the window or taking this control out of it, since a popup would stay on screen.
        WhileLoaded.Run(this, () =>
        {
            tasks.Changed += OnTasksChanged;
            _window = Window.GetWindow(this);
            if (_window is not null)
            {
                _window.PreviewKeyDown += OnWindowPreviewKeyDown;
                _window.PreviewMouseDown += OnWindowPreviewMouseDown;
                _window.IsVisibleChanged += OnWindowVisibleChanged;
                _window.Deactivated += OnWindowDeactivated;
            }

            model.Refresh();
        }, () =>
        {
            tasks.Changed -= OnTasksChanged;
            if (_window is not null)
            {
                _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
                _window.PreviewMouseDown -= OnWindowPreviewMouseDown;
                _window.IsVisibleChanged -= OnWindowVisibleChanged;
                _window.Deactivated -= OnWindowDeactivated;
                _window = null;
            }

            _menuModel.Close();
        });
        ShowModel();
    }

    /// <summary>Raised after the user adds a tag, with the tag.</summary>
    public event EventHandler<string>? TagAdded;

    /// <summary>Raised after the user removes a tag, with the tag.</summary>
    public event EventHandler<string>? TagRemoved;

    /// <summary>The tags, in the order they were added. Setting them raises neither event.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _model.Tags;
        set => _model.ReplaceTags(value);
    }

    /// <summary>Puts the cursor in the first tag's remove button, or in + when there are no tags.</summary>
    internal void FocusFirst() => KeyboardFocus.MoveTo(FirstRemoveButton() ?? _plus);

    /// <summary>
    /// Closes the innermost open part, in this order: the + menu, then Custom, wherever the cursor is. Returns false,
    /// doing nothing, when none is open.
    /// </summary>
    internal bool CloseOpen() => _menuModel.Escape() || _model.CloseCustom();

    // Typed letters become lowercase and typed whitespace does nothing, even over a selection, as pasted text does through the same rule.
    private void OnInputText(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text))
        {
            e.Handled = true;
            return;
        }

        var text = TagText.Of(e.Text);
        if (text == e.Text)
        {
            return;
        }

        e.Handled = true;
        var start = _input.SelectionStart;
        _input.SelectedText = text;
        _input.CaretIndex = start + text.Length;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && CloseOpen())
        {
            e.Handled = true;
        }
    }

    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_menuModel.IsOpen && !_menu.Covers(e.OriginalSource as DependencyObject))
        {
            _menuModel.Close();
        }
    }

    private void OnWindowVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_window!.IsVisible)
        {
            _menuModel.Close();
        }
    }

    // Another application took over, so the popups would stay on screen over it. Popups never activate (WPF creates
    // them WS_EX_NOACTIVATE), so the cursor going into the Search box doesn't deactivate the window.
    private void OnWindowDeactivated(object? sender, EventArgs e) => _menuModel.Close();

    private Button? FirstRemoveButton() =>
        _badges.ItemContainerGenerator.ContainerFromIndex(0) is DependencyObject first ? FindButton(first) : null;

    private static Button? FindButton(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var found = child as Button ?? FindButton(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnTasksChanged(object? sender, EventArgs e) => OnElementThread.Run(this, _model.Refresh);

    // Tab from the box goes to the colors while they show; with none showing it keeps its usual meaning.
    private bool FocusColors()
    {
        if (!_model.ColorsShown)
        {
            return false;
        }

        _colors.FocusFirst();
        return true;
    }

    private bool OpenOrEnter()
    {
        if (_menuModel.IsOpen)
        {
            _menuModel.Enter();
        }
        else
        {
            _menuModel.Open();
        }

        return true;
    }

    private bool OpenOrDown()
    {
        if (_menuModel.IsOpen)
        {
            _menuModel.MoveDown();
        }
        else
        {
            _menuModel.Open();
        }

        return true;
    }

    private bool MenuUp()
    {
        if (!_menuModel.IsOpen)
        {
            return false;
        }

        _menuModel.MoveUp();
        return true;
    }

    private void ShowModel()
    {
        if (!ReferenceEquals(_badges.Tags, _model.Tags))
        {
            _badges.Tags = _model.Tags;
        }

        if (_input.Text != _model.TypedText)
        {
            _input.Text = _model.TypedText;
        }

        _colors.Visibility = _model.ColorsShown ? Visibility.Visible : Visibility.Collapsed;
        _colors.Show(_model.MarkedColor);
        _message.Text = _model.ColorMessage;
        _message.Visibility = _model.ColorMessage.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ShowCustom(_model.IsCustomOpen);
    }

    private void ShowCustom(bool open)
    {
        var wasOpen = _custom.Visibility == Visibility.Visible;
        if (open == wasOpen)
        {
            return;
        }

        var hadFocus = _custom.IsKeyboardFocusWithin;
        _custom.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open)
        {
            KeyboardFocus.MoveTo(_input);
        }
        else if (hadFocus)
        {
            KeyboardFocus.MoveTo(_plus);
        }
    }
}
