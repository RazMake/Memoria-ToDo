using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's tags as the <see cref="TagsModel"/> holds them: the tags added so far, each with a remove button, a line to
/// type the next one with suggestions from the tags in use, and the most-used tags as chips.
/// </summary>
internal sealed class TagsLine : UserControl
{
    private readonly TagsModel _model;
    private readonly ItemsControl _tagList;
    private readonly TextBox _input = new();
    private readonly ListBox _suggestions = new() { Focusable = false };
    private readonly KeyMap _keys = new();

    public TagsLine(TagsModel model, ITasks tasks)
    {
        _model = model;
        _tagList = ClickableItems.Create(TagVisual(), model.Remove);
        AutomationProperties.SetAutomationId(_tagList, "TagList");
        AutomationProperties.SetAutomationId(_input, "Tags");
        AutomationProperties.SetAutomationId(_suggestions, "Suggestions");
        _input.Margin = new Thickness(0, 0, 0, 4);
        var chips = new TagChips(tasks) { Margin = new Thickness(0, 0, 0, 12) };
        chips.TagChosen += (_, tag) => model.Add(tag);

        var panel = new StackPanel();
        panel.Children.Add(_tagList);
        panel.Children.Add(_input);
        panel.Children.Add(_suggestions);
        panel.Children.Add(chips);
        Content = panel;

        _suggestions.ShowChoices(model.Suggestions);
        model.Changed += ShowModel;
        _keys
            .On(ModifierKeys.None, Key.Enter, model.AddChosenOrTyped)
            .On(ModifierKeys.None, Key.Down, model.MoveDown)
            .Try(ModifierKeys.None, Key.Back, model.Backspace);

        _input.PreviewKeyDown += OnInputKeyDown;
        _input.TextChanged += (_, _) =>
        {
            if (model.TypedText != _input.Text)
            {
                model.TypedText = _input.Text;
            }
        };
        DataObject.AddPastingHandler(_input, TextPaste.JoinLines);
        _suggestions.PreviewMouseLeftButtonUp += OnSuggestionClicked;
        WhileLoaded.Run(this, () =>
        {
            tasks.Changed += OnTasksChanged;
            model.Refresh();
        }, () => tasks.Changed -= OnTasksChanged);
    }

    /// <summary>Puts the cursor in the typing line.</summary>
    public void FocusInput() => KeyboardFocus.MoveTo(_input);

    private void OnTasksChanged(object? sender, EventArgs e) => OnElementThread.Run(this, _model.Refresh);

    private void ShowModel()
    {
        if (!ReferenceEquals(_tagList.ItemsSource, _model.Tags))
        {
            _tagList.ItemsSource = _model.Tags;
        }

        if (_input.Text != _model.TypedText)
        {
            _input.Text = _model.TypedText;
        }
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

    private void OnInputKeyDown(object sender, KeyEventArgs e) => e.Handled = _keys.TryRun(Keyboard.Modifiers, e.Key);

    private void OnSuggestionClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_suggestions, source) is ListBoxItem item)
        {
            _model.AddSuggestion(_suggestions.ItemContainerGenerator.IndexFromContainer(item));
            FocusInput();
        }
    }
}
