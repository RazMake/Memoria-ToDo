using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A task's one tag as a pill in its color, with an x that clears it, or a "No tag" pill. A click elsewhere on the pill opens a
/// menu under it with the eight most-used tags other than the current one, Search, which lists the tags that match what is typed
/// in a submenu beside it, and Custom…. Custom opens below the pill: a New tag box and 30 colors, which show while the box holds
/// text. A click on a color keeps it for the tag and chooses the tag; Enter chooses the tag with no new color.
/// </summary>
public sealed class TagEditor : UserControl
{
    /// <summary>The words the pill shows when the task has no tag, and the Task list's "No tag" group heading.</summary>
    public const string NoTagText = "No tag";

    private static readonly CornerRadius Rounded = new(10);

    private readonly TagsModel _model;
    private readonly TagMenuModel _menuModel;
    private readonly TagLook _look;
    private readonly Button _pill = new() { Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand };
    private readonly Button _clear = new()
    {
        Content = "×",
        Padding = new Thickness(2, 2, 8, 2),
        Cursor = Cursors.Hand,
        Template = TagBadgeVisuals.FlatButtonWith(new CornerRadius(0, 10, 10, 0)),
    };
    private readonly ControlTemplate _pillWithClear = TagBadgeVisuals.FlatButtonWith(new CornerRadius(10, 0, 0, 10));
    private readonly ControlTemplate _pillAlone = TagBadgeVisuals.FlatButtonWith(Rounded);
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
    private readonly KeyMap _pillKeys = new();
    private Window? _window;

    public TagEditor(ITasks tasks, ITagColors tagColors, ITheme theme)
        : this(tasks, new TagsModel(tasks, tagColors), new TagLook(tagColors, theme))
    {
    }

    internal TagEditor(ITasks tasks, TagsModel model, TagLook look)
    {
        _model = model;
        _look = look;
        _menuModel = new TagMenuModel(tasks, model);
        _menu = new TagMenu(_menuModel, look, _pill);
        _colors = new ColorGrid(look) { Visibility = Visibility.Collapsed };
        AutomationProperties.SetAutomationId(_pill, "TagPill");
        AutomationProperties.SetAutomationId(_clear, "ClearTag");
        AutomationProperties.SetName(_clear, "Clear tag");
        AutomationProperties.SetAutomationId(_input, "Tags");

        var label = new TextBlock { Text = "New tag:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var box = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(label, Dock.Left);
        box.Children.Add(label);
        box.Children.Add(_input);
        _custom.Children.Add(box);
        _custom.Children.Add(_colors);
        _custom.Children.Add(_message);
        var pill = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        pill.Children.Add(_pill);
        pill.Children.Add(_clear);
        var panel = new StackPanel();
        panel.Children.Add(pill);
        panel.Children.Add(_menu);
        panel.Children.Add(_custom);
        Content = panel;

        model.TagAdded += tag => TagChanged?.Invoke(this, tag);
        model.TagRemoved += _ => TagChanged?.Invoke(this, null);
        model.Changed += ShowModel;
        look.Follow(this, () =>
        {
            ShowPill();
            _colors.Show(model.MarkedColor);
        });

        _inputKeys
            .On(ModifierKeys.None, Key.Enter, () => model.AddTypedText())
            .Try(ModifierKeys.None, Key.Tab, FocusColors);
        _pillKeys
            .Try(ModifierKeys.None, Key.Enter, OpenOrEnter)
            .Try(ModifierKeys.None, Key.Space, OpenOrEnter)
            .Try(ModifierKeys.None, Key.Down, OpenOrDown)
            .Try(ModifierKeys.None, Key.Up, MenuUp);

        _pill.Click += (_, _) =>
        {
            KeyboardFocus.MoveTo(_pill);
            _menuModel.Toggle();
        };
        _pill.PreviewKeyDown += (_, e) => e.Handled = _pillKeys.TryRun(Keyboard.Modifiers, e.Key);
        _pill.PreviewTextInput += (_, e) => e.Handled = e.Text.Length == 1 && _menuModel.TypeOutsideSearch(e.Text[0]);
        // The x disappears with the tag, so the cursor goes to the pill.
        _clear.Click += (_, _) =>
        {
            model.Clear();
            KeyboardFocus.MoveTo(_pill);
        };
        _colors.Chosen += color =>
        {
            model.AddTypedTextInColor(color);
            // Custom is hidden once the tag is chosen, so the cursor goes to the pill; it stays in Custom when the color couldn't be kept.
            KeyboardFocus.MoveTo(model.IsCustomOpen ? _input : _pill);
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

    /// <summary>Raised after the user chooses or clears the tag, once <see cref="TaskTag"/> holds it: the chosen tag, spelled as Tasks' <c>WithTag</c> spells it, or null when cleared. Choosing the current tag, ignoring case, raises nothing.</summary>
    public event EventHandler<string?>? TagChanged;

    /// <summary>The tag the pill shows, or null for "No tag". Setting it changes the pill, raises nothing, and leaves an open menu or Custom as it is.</summary>
    public string? TaskTag
    {
        get => _model.Tags.Count > 0 ? _model.Tags[0] : null;
        set => _model.ReplaceTags(value is null ? [] : [value]);
    }

    /// <summary>Puts the cursor in the pill.</summary>
    internal void FocusFirst() => KeyboardFocus.MoveTo(_pill);

    /// <summary>
    /// Closes the innermost open part, in this order: the menu, then Custom, wherever the cursor is. Returns false,
    /// doing nothing, when none is open.
    /// </summary>
    public bool CloseEdit() => _menuModel.Escape() || _model.CloseCustom();

    /// <summary>Chooses the text typed in Custom as the tag, as Enter there does, raising <see cref="TagChanged"/>. Does nothing when Custom holds no text.</summary>
    public void ChooseTypedText() => _model.AddTypedText();

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
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && CloseEdit())
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
        ShowPill();
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

    // The tag in its color with an x, or "No tag" in the theme's plain look with none.
    private void ShowPill()
    {
        var tag = TaskTag;
        _pill.Content = tag ?? NoTagText;
        _clear.Visibility = tag is null ? Visibility.Collapsed : Visibility.Visible;
        if (tag is null)
        {
            _pill.Template = _pillAlone;
            _pill.Background = Brushes.Transparent;
            _pill.BorderThickness = new Thickness(1);
            _pill.SetResourceReference(Control.BorderBrushProperty, "SurfaceStrokeColorDefaultBrush");
            _pill.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
            return;
        }

        var fill = _look.FillOf(tag);
        var background = TagLook.Paint(fill.Background);
        var text = TagLook.Paint(fill.Text);
        _pill.Template = _pillWithClear;
        _pill.BorderThickness = new Thickness(0);
        _pill.Background = background;
        _pill.Foreground = text;
        _clear.Background = background;
        _clear.Foreground = text;
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
            KeyboardFocus.MoveTo(_pill);
        }
    }
}
