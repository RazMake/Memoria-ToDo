using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The right side: what it says while no task is selected, or the selected task's formatted description with its tag control
/// under it, or the editor under the Edit tab, or its draft under the Preview tab. It scrolls on its own.
/// </summary>
internal sealed class DetailsPane : ScrollViewer
{
    private readonly FormattedDescription _description;
    private readonly EditTabs _tabs = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _prompt = Note("Select a task to see its description.");
    private readonly TextBlock _empty = Note("No description.");
    private readonly Border _tagSlot = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly Border _editorSlot = new();

    /// <param name="description">The one description view, which the window releases when it closes.</param>
    public DetailsPane(FormattedDescription description)
    {
        _description = description;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        // The pane takes the keyboard focus for the preview, so its arrow keys scroll it, but Tab never stops on it.
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, false);
        AutomationProperties.SetAutomationId(this, "Details");
        var content = new StackPanel { Margin = new Thickness(16) };
        content.Children.Add(_tabs);
        content.Children.Add(_prompt);
        content.Children.Add(_empty);
        content.Children.Add(description);
        content.Children.Add(_tagSlot);
        content.Children.Add(_editorSlot);
        Content = content;
        _tabs.Chosen += (_, preview) => TabChosen?.Invoke(this, preview);
        ShowNothing();
    }

    /// <summary>Raised when the user clicks the tab header Preview (true) or Edit (false).</summary>
    public event EventHandler<bool>? TabChosen;

    /// <summary>Says to select a task.</summary>
    public void ShowNothing() => Show(prompt: true, description: null, tags: null, editor: null, preview: false);

    /// <summary>Shows the description, or "No description." when it is empty, with the task's tag control under it.</summary>
    public void ShowTask(string description, UIElement tags) => Show(prompt: false, description, tags, editor: null, preview: false);

    /// <summary>Shows a changed description of the task already shown, leaving its tag control as it is.</summary>
    public void UpdateDescription(string description) => SetDescription(description, preview: false);

    /// <summary>Shows the editor in place of the description, under the Edit tab, with the task's tags as they are.</summary>
    public void ShowEditor(UIElement editor, UIElement tags) => Show(prompt: false, description: null, tags, editor, preview: false);

    /// <summary>
    /// Shows the draft formatted, with resize corners, under the Preview tab. The editor stays in the pane, hidden, with its text
    /// and cursor, for the Edit tab.
    /// </summary>
    public void ShowPreview(string draft, UIElement editor, UIElement tags) => Show(prompt: false, draft, tags, editor, preview: true);

    /// <summary>Gives the pane the keyboard focus, so its arrow keys and Page Up and Page Down scroll it.</summary>
    public void FocusContent() => Focus();

    private void Show(bool prompt, string? description, UIElement? tags, UIElement? editor, bool preview)
    {
        _prompt.Visibility = Visible(prompt);
        _tagSlot.Child = tags;
        _tagSlot.Visibility = Visible(tags is not null);
        _editorSlot.Child = editor;
        _editorSlot.Visibility = Visible(editor is not null && !preview);
        _tabs.Visibility = Visible(editor is not null);
        _tabs.Show(preview);
        SetDescription(description, preview);
    }

    // The page is shown once: the Markdown goes in first when a preview starts, and the preview flag goes first when it ends.
    private void SetDescription(string? description, bool preview)
    {
        if (!preview)
        {
            _description.IsPreview = false;
        }

        if (description is not null && _description.Markdown != description)
        {
            _description.Markdown = description;
        }

        if (preview)
        {
            _description.IsPreview = true;
        }

        var has = !string.IsNullOrEmpty(description);

        // Hidden and flat, not collapsed: a collapsed web view never loads, and it must start with the window.
        _description.Visibility = has ? Visibility.Visible : Visibility.Hidden;
        _description.MaxHeight = has ? double.PositiveInfinity : 0;
        _empty.Visibility = Visible(description is not null && !has);
    }

    private static Visibility Visible(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
