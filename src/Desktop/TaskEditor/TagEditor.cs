using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A task's tags as colored badges, each with a remove button, and a + button that opens a menu of the most-used tags and
/// Custom. Custom is a line to type a tag with suggestions from the tags in use, and a grid of colors to keep for it.
/// </summary>
public sealed class TagEditor : UserControl
{
    private readonly TagsModel _model;
    private readonly BadgeList _badges;
    private readonly Button _plus = new() { Content = "+", Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 0, 6, 6) };
    private readonly TagMenu _menu;
    private readonly StackPanel _custom = new() { Visibility = Visibility.Collapsed };
    private readonly TextBox _input = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly ListBox _suggestions = new() { Focusable = false };
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
        _badges = new BadgeList(look, TagBadgeVisuals.Removable(look), model.Remove, _plus);
        _menu = new TagMenu(look);
        _colors = new ColorGrid(look);
        AutomationProperties.SetAutomationId(_badges, "TagList");
        AutomationProperties.SetAutomationId(_plus, "AddTag");
        AutomationProperties.SetAutomationId(_input, "Tags");
        AutomationProperties.SetAutomationId(_suggestions, "Suggestions");

        _custom.Children.Add(_input);
        _custom.Children.Add(_suggestions);
        _custom.Children.Add(_colors);
        _custom.Children.Add(_message);
        var panel = new StackPanel();
        panel.Children.Add(_badges);
        panel.Children.Add(_menu);
        panel.Children.Add(_custom);
        Content = panel;

        _suggestions.ShowChoices(model.Suggestions);
        model.TagAdded += tag => TagAdded?.Invoke(this, tag);
        model.TagRemoved += tag => TagRemoved?.Invoke(this, tag);
        model.Changed += ShowModel;

        _inputKeys
            .On(ModifierKeys.None, Key.Enter, model.AddChosenOrTyped)
            .On(ModifierKeys.None, Key.Down, model.MoveDown)
            .Try(ModifierKeys.None, Key.Back, model.Backspace);
        _plusKeys
            .Try(ModifierKeys.None, Key.Enter, ChooseOrOpenMenu)
            .Try(ModifierKeys.None, Key.Space, ChooseOrOpenMenu)
            .Try(ModifierKeys.None, Key.Down, MenuDown)
            .Try(ModifierKeys.None, Key.Up, MenuUp);

        _plus.Click += (_, _) =>
        {
            KeyboardFocus.MoveTo(_plus);
            ToggleMenu();
        };
        _plus.PreviewKeyDown += (_, e) => e.Handled = _plusKeys.TryRun(Keyboard.Modifiers, e.Key);
        _menu.TagChosen += model.Add;
        _menu.CustomChosen += model.OpenCustom;
        _colors.Picked += color => model.PickedColor = color;
        _input.PreviewKeyDown += (_, e) => e.Handled = _inputKeys.TryRun(Keyboard.Modifiers, e.Key);
        _input.TextChanged += (_, _) =>
        {
            if (model.TypedText != _input.Text)
            {
                model.TypedText = _input.Text;
            }
        };
        DataObject.AddPastingHandler(_input, TextPaste.JoinLines);
        _suggestions.PreviewMouseLeftButtonUp += OnSuggestionClicked;
        // Esc closes an open part wherever the cursor is in the window, before the window's own Esc. A key an editor
        // around this control handled first arrives handled and is left alone.
        WhileLoaded.Run(this, () =>
        {
            tasks.Changed += OnTasksChanged;
            _window = Window.GetWindow(this);
            if (_window is not null)
            {
                _window.PreviewKeyDown += OnWindowPreviewKeyDown;
            }

            model.Refresh();
        }, () =>
        {
            tasks.Changed -= OnTasksChanged;
            if (_window is not null)
            {
                _window.PreviewKeyDown -= OnWindowPreviewKeyDown;
                _window = null;
            }
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
    /// Closes the innermost open part, in this order: the suggestions, the menu, then Custom, wherever the cursor is.
    /// Returns false, doing nothing, when none is open.
    /// </summary>
    internal bool CloseOpen() => _model.CloseSuggestions() || _menu.Close() || _model.CloseCustom();

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && CloseOpen())
        {
            e.Handled = true;
        }
    }

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

    private void ToggleMenu()
    {
        if (!_menu.Close())
        {
            _menu.Open(_model.MenuTags);
        }
    }

    private bool ChooseOrOpenMenu()
    {
        if (!_menu.IsOpen)
        {
            _menu.Open(_model.MenuTags);
        }
        else
        {
            _menu.ChooseHighlighted();
        }

        return true;
    }

    private bool MenuDown()
    {
        if (!_menu.IsOpen)
        {
            _menu.Open(_model.MenuTags);
        }
        else
        {
            _menu.HighlightNext();
        }

        return true;
    }

    private bool MenuUp()
    {
        if (!_menu.IsOpen)
        {
            return false;
        }

        _menu.HighlightPrevious();
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

        _colors.Show(_model.PickedColor);
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

    private void OnSuggestionClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_suggestions, source) is ListBoxItem item)
        {
            _model.AddSuggestion(_suggestions.ItemContainerGenerator.IndexFromContainer(item));
            // Custom is hidden once the tag is added, so the cursor goes to +; it stays in the tag box when the color couldn't be kept.
            KeyboardFocus.MoveTo(_model.IsCustomOpen ? _input : _plus);
        }
    }
}
