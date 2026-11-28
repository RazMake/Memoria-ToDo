using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A task's tags as pills in order, each in its color with an x that removes it, then an add pill: "No tag" while there are none,
/// "+" after, and off at 15 tags. A click on a pill opens a menu under it with the eight most-used tags the task doesn't have,
/// Search, which lists the tags that match what is typed in a submenu beside it, and Custom…; the choice replaces that tag in place.
/// The add pill opens the same menu, and its choice goes at the end. Custom opens below the pills: a New tag box and 30 colors, which
/// show while the box holds text. A click on a color keeps it for the tag and chooses the tag; Enter chooses the tag with no new color.
/// A pill dragged past Windows' drag distance moves with the pointer and the others make room; Alt+Left and Alt+Right move the pill
/// that has the cursor one place. The first tag groups the task, so its pill is a little larger.
/// </summary>
public sealed class TagEditor : UserControl
{
    /// <summary>The words the add pill shows when the task has no tag, and the Task list's "No tag" group heading.</summary>
    public const string NoTagText = "No tag";

    private readonly TagsModel _model;
    private readonly TagMenuModel _menuModel;
    private readonly TagPills _pills;
    private readonly PillDrag _drag;
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
    private readonly KeyMap _moveKeys = new();
    private Window? _window;
    private int _keyedPill = -1;

    public TagEditor(ITasks tasks, ITagColors tagColors, ITheme theme, IPointerPosition pointer, ILogger logger)
        : this(tasks, new TagsModel(tasks, tagColors, Argument.Required(logger)), new TagLook(tagColors, theme), Argument.Required(pointer), logger)
    {
    }

    internal TagEditor(ITasks tasks, TagsModel model, TagLook look, IPointerPosition pointer, ILogger logger)
    {
        _model = model;
        _pills = new TagPills(look);
        _menuModel = new TagMenuModel(tasks, model);
        _menu = new TagMenu(_menuModel, look, _pills.AddPill);
        _colors = new ColorGrid(look) { Visibility = Visibility.Collapsed };
        _drag = new PillDrag(this, pointer, _pills, ShowModel, model.Move, ShowModel, logger);
        AutomationProperties.SetAutomationId(_input, "Tags");

        var label = new TextBlock { Text = "New tag:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var box = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(label, Dock.Left);
        box.Children.Add(label);
        box.Children.Add(_input);
        _custom.Children.Add(box);
        _custom.Children.Add(_colors);
        var panel = new StackPanel();
        panel.Children.Add(_pills);
        panel.Children.Add(_menu);
        panel.Children.Add(_custom);
        // Outside Custom, so a message left by a color kept before a notice failed shows after Custom closes.
        panel.Children.Add(_message);
        Content = panel;

        model.TagsChanged += tags => TagsChanged?.Invoke(this, tags);
        model.Chosen += index => KeyboardFocus.MoveTo(index < _pills.Count ? _pills.PillAt(index) : _pills.AddPill);
        model.Changed += ShowModel;
        look.Follow(this, () =>
        {
            _pills.Repaint();
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
        _moveKeys
            .Try(ModifierKeys.Alt, Key.Left, () => MoveFocusedPill(-1))
            .Try(ModifierKeys.Alt, Key.Right, () => MoveFocusedPill(1));
        new KeyRouter().Listen(this, _moveKeys);

        _pills.TagClicked += index => ToggleMenuFrom(index);
        _pills.AddClicked += () => ToggleMenuFrom(_pills.Count);
        _pills.TagPressed += index => _drag.Press(index, model.Tags);
        _pills.TagRemoved += index =>
        {
            model.RemoveAt(index);
            // The x disappears with its tag, so the cursor goes to the pill that took its place, or to the add pill.
            KeyboardFocus.MoveTo(index < _pills.Count ? _pills.PillAt(index) : _pills.AddPill);
        };
        _pills.PreviewKeyDown += (_, e) => e.Handled = RunPillKey(e);
        _pills.PreviewTextInput += (_, e) => e.Handled = e.Text.Length == 1 && _menuModel.TypeOutsideSearch(e.Text[0]);
        _colors.Chosen += color =>
        {
            model.AddTypedTextInColor(color);
            // Custom is hidden once the tag is chosen, which puts the cursor on the tag; it stays in Custom when the color couldn't be kept.
            if (model.IsCustomOpen)
            {
                KeyboardFocus.MoveTo(_input);
            }
        };
        _input.PreviewKeyDown += (_, e) => e.Handled = _inputKeys.TryRun(Keyboard.Modifiers, e.Key);
        _input.TextChanged += (_, _) => OnInputChanged();
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

            _drag.Cancel();
            _menuModel.Close();
        });
        ShowModel();
    }

    /// <summary>
    /// Raised once for each change the user makes to the tags, once <see cref="TaskTags"/> holds them, with the whole list: a tag
    /// chosen, added, replaced, or removed, or the order changed by a drop or Alt+Left and Alt+Right. Never while a pill is
    /// still being dragged, and never for a drop where it started or a choice that changes nothing.
    /// </summary>
    public event EventHandler<IReadOnlyList<string>>? TagsChanged;

    /// <summary>The tags the pills show, in order. Setting them changes the pills, raises nothing, cancels a drag in progress since its places no longer fit, and leaves an open menu or Custom as it is.</summary>
    public IReadOnlyList<string> TaskTags
    {
        get => _model.Tags;
        set
        {
            _drag.Abandon();
            _model.ReplaceTags(value);
        }
    }

    /// <summary>Puts the cursor in the first pill, or in the add pill when there are none.</summary>
    internal void FocusFirst() => _pills.FocusFirst();

    /// <summary>
    /// Closes the innermost open part, in this order: a pill's drag, which puts the pill back, then the menu, then Custom, wherever
    /// the cursor is. Returns false, doing nothing, when none is open.
    /// </summary>
    public bool CloseEdit() => _drag.Cancel() || _menuModel.Escape() || _model.CloseCustom();

    /// <summary>
    /// Chooses the text typed in Custom as the tag, as Enter there does, raising <see cref="TagsChanged"/>. Returns true when Custom holds
    /// no text or the tag was chosen; false when the task has 15 tags, which leaves Custom open with the text and says why.
    /// </summary>
    public bool ChooseTypedText() => _model.AddTypedText();

    // The box shows the text as TagText says; the typed text is what the model gets, once the box shows it.
    private void OnInputChanged()
    {
        var text = _input.Text;
        var rewritten = TagText.Of(text);
        if (rewritten != text)
        {
            var caret = TagText.Of(text[.._input.CaretIndex]).Length;
            _input.Text = rewritten;
            _input.CaretIndex = Math.Min(caret, rewritten.Length);
            return;
        }

        if (_model.TypedText != text)
        {
            _model.TypedText = text;
        }
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

    // A click on a pill opens the menu under it, or closes it when it is open under that pill.
    private void ToggleMenuFrom(int index)
    {
        var anchor = AnchorOf(index);
        KeyboardFocus.MoveTo(anchor);
        if (_menuModel.IsOpen && ReferenceEquals(_menu.Anchor, anchor))
        {
            _menuModel.Close();
            return;
        }

        OpenMenuFrom(index);
    }

    // The menu's choice replaces the tag at the place, or goes at the end for the add pill.
    private void OpenMenuFrom(int index)
    {
        _menuModel.Close();
        _model.Target = index < _pills.Count ? index : null;
        _menu.Anchor = AnchorOf(index);
        _menuModel.Open();
    }

    private UIElement AnchorOf(int index) => index < _pills.Count ? _pills.PillAt(index) : _pills.AddPill;

    // The keys of a pill's own button; the x keeps its button keys.
    private bool RunPillKey(KeyEventArgs e)
    {
        var index = _pills.IndexOfPill(e.OriginalSource);
        if (index < 0 || !ReferenceEquals(e.OriginalSource, AnchorOf(index)))
        {
            return false;
        }

        _keyedPill = index;
        return _pillKeys.TryRun(Keyboard.Modifiers, e.Key);
    }

    // Alt+Left and Alt+Right: the pill with the cursor moves one place and keeps it. At either end nothing moves, and the key is still taken.
    private bool MoveFocusedPill(int step)
    {
        var index = _pills.IndexOfPill(Keyboard.FocusedElement);
        if (index < 0 || index >= _pills.Count)
        {
            return false;
        }

        var to = index + step;
        if (to >= 0 && to < _pills.Count)
        {
            _model.Move(index, to);
        }

        return true;
    }

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
            OpenMenuFrom(_keyedPill);
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
            OpenMenuFrom(_keyedPill);
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
        _pills.Show(_drag.IsDragging ? _drag.Order : _model.Tags, !_model.IsFull, _drag.Held);
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
            var anchor = _menu.Anchor;
            KeyboardFocus.MoveTo(anchor.IsDescendantOf(_pills) ? anchor : _pills.AddPill);
        }
    }
}
