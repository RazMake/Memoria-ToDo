using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's tags: the tags added so far, each with a remove button, a line to type the next one with suggestions
/// from the tags in use, and the most-used tags as chips.
/// </summary>
internal sealed class TagsLine : UserControl
{
    private readonly ITasks _tasks;
    private readonly ItemsControl _tagList;
    private readonly TextBox _input = new();
    private readonly ListBox _suggestions = new() { Focusable = false, Visibility = Visibility.Collapsed };
    private readonly KeyMap _keys = new();
    private readonly KeyMap _keysOnEmptyLine = new();
    private IReadOnlyList<string> _tags = [];

    public TagsLine(ITasks tasks)
    {
        _tasks = tasks;
        _tagList = ClickableItems.Create(TagVisual(), RemoveTag);
        AutomationProperties.SetAutomationId(_tagList, "TagList");
        AutomationProperties.SetAutomationId(_input, "Tags");
        AutomationProperties.SetAutomationId(_suggestions, "Suggestions");
        _input.Margin = new Thickness(0, 0, 0, 4);
        var chips = new TagChips(tasks) { Margin = new Thickness(0, 0, 0, 12) };
        chips.TagChosen += (_, tag) => AddTag(tag);

        var panel = new StackPanel();
        panel.Children.Add(_tagList);
        panel.Children.Add(_input);
        panel.Children.Add(_suggestions);
        panel.Children.Add(chips);
        Content = panel;

        _keys
            .On(ModifierKeys.None, Key.Enter, AddChosenOrTyped)
            .On(ModifierKeys.None, Key.Down, _suggestions.ChooseNext);
        _keysOnEmptyLine.On(ModifierKeys.None, Key.Back, RemoveLastTag);

        _input.PreviewKeyDown += OnInputKeyDown;
        _input.TextChanged += (_, _) => ShowSuggestions();
        DataObject.AddPastingHandler(_input, TextPaste.JoinLines);
        _suggestions.PreviewMouseLeftButtonUp += OnSuggestionClicked;
        tasks.Changed += (_, _) => ShowSuggestions();
    }

    /// <summary>The tags in the order they were added. Setting them clears the typed text.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _tags = value;
            _tagList.ItemsSource = value;
            _input.Clear();
            ShowSuggestions();
        }
    }

    /// <summary>The text typed in the line and not yet added as a tag.</summary>
    public string TypedText
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    /// <summary>Puts the cursor in the typing line.</summary>
    public void FocusInput() => KeyboardFocus.MoveTo(_input);

    /// <summary>Adds the typed text as a tag, if there is any, so saving loses none of it.</summary>
    public void AddTypedText()
    {
        if (!string.IsNullOrWhiteSpace(_input.Text))
        {
            AddTag(_input.Text);
        }
    }

    /// <summary>Closes the suggestions. Returns false, doing nothing, when none were open.</summary>
    public bool CloseSuggestions()
    {
        if (_suggestions.Visibility != Visibility.Visible)
        {
            return false;
        }

        _suggestions.ItemsSource = null;
        _suggestions.Visibility = Visibility.Collapsed;
        return true;
    }

    private static FrameworkElementFactory TagVisual()
    {
        var remove = new FrameworkElementFactory(typeof(Button));
        remove.SetValue(ContentControl.ContentProperty, "×");
        remove.SetValue(Control.PaddingProperty, new Thickness(6, 0, 6, 0));
        remove.SetBinding(AutomationProperties.AutomationIdProperty, new Binding(".") { StringFormat = "Remove {0}" });

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding("."));
        name.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));

        var tag = new FrameworkElementFactory(typeof(StackPanel));
        tag.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        tag.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 4));
        tag.AppendChild(name);
        tag.AppendChild(remove);
        return tag;
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e) =>
        e.Handled = _keys.TryRun(Keyboard.Modifiers, e.Key)
            || (_input.Text.Length == 0 && _keysOnEmptyLine.TryRun(Keyboard.Modifiers, e.Key));

    private void OnSuggestionClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_suggestions, source) is ListBoxItem { Content: string tag })
        {
            AddTag(tag);
            FocusInput();
        }
    }

    private void AddChosenOrTyped()
    {
        if (_suggestions.SelectedItem is string chosen)
        {
            AddTag(chosen);
        }
        else
        {
            AddTypedText();
        }
    }

    // Setting Tags also clears the typed text.
    private void AddTag(string tag) => Tags = _tasks.WithTag(_tags, tag);

    private void RemoveTag(string tag) => Tags = [.. _tags.Where(existing => existing != tag)];

    private void RemoveLastTag()
    {
        if (_tags.Count > 0)
        {
            Tags = [.. _tags.Take(_tags.Count - 1)];
        }
    }

    private void ShowSuggestions()
    {
        var suggestions = _tasks.SuggestTags(_input.Text, _tags);
        _suggestions.ItemsSource = suggestions;
        _suggestions.Visibility = suggestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
