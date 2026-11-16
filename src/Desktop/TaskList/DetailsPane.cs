using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The right side: what it says while no task is selected, or the selected task's formatted description with its tag control
/// under it, or the editor. It scrolls on its own, and shows one of the three at a time.
/// </summary>
internal sealed class DetailsPane : ScrollViewer
{
    private static readonly Thickness ContentMargin = new(16);

    private readonly FormattedDescription _description;
    private readonly TextBlock _prompt = Note("Select a task to see its description.");
    private readonly TextBlock _empty = Note("No description.");
    private readonly Border _tagSlot = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly EditorSlot _editorSlot = new();

    /// <param name="description">The one description view, which the window releases when it closes.</param>
    public DetailsPane(FormattedDescription description)
    {
        _description = description;
        Focusable = false; // So that Ctrl+Tab doesn't stop on the pane and frame it; the mouse wheel scrolls it without focus.
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        AutomationProperties.SetAutomationId(this, "Details");
        var content = new StackPanel { Margin = ContentMargin };
        content.Children.Add(_prompt);
        content.Children.Add(_empty);
        content.Children.Add(description);
        content.Children.Add(_tagSlot);
        content.Children.Add(_editorSlot);
        Content = content;
        SizeChanged += (_, _) => FillEditorSlot();
        ShowNothing();
    }

    /// <summary>Says to select a task.</summary>
    public void ShowNothing() => Show(prompt: true, description: null, tags: null, editor: null);

    /// <summary>Shows the description, or "No description." when it is empty, with the task's tag control under it.</summary>
    public void ShowTask(string description, UIElement tags) => Show(prompt: false, description, tags, editor: null);

    /// <summary>Shows a changed description of the task already shown, leaving its tag control as it is.</summary>
    public void UpdateDescription(string description) => SetDescription(description);

    /// <summary>Shows the editor in place of the description and the tag control.</summary>
    public void ShowEditor(UIElement editor) => Show(prompt: false, description: null, tags: null, editor);

    private void Show(bool prompt, string? description, UIElement? tags, UIElement? editor)
    {
        _prompt.Visibility = Visible(prompt);
        _tagSlot.Child = tags;
        _tagSlot.Visibility = Visible(tags is not null);
        _editorSlot.Child = editor;
        _editorSlot.Visibility = Visible(editor is not null);
        FillEditorSlot();
        SetDescription(description);
    }

    // The editor gets the height of the pane, so its Description stretches and the pane doesn't scroll.
    private void FillEditorSlot() => _editorSlot.FillHeight = Math.Max(0, ActualHeight - 2 * ContentMargin.Top);

    private void SetDescription(string? description)
    {
        if (description is not null && _description.Markdown != description)
        {
            _description.Markdown = description;
        }

        var has = !string.IsNullOrEmpty(description);

        // Hidden and flat, not collapsed: a collapsed web view never loads, and it must start with the window.
        _description.Visibility = has ? Visibility.Visible : Visibility.Hidden;
        _description.MaxHeight = has ? double.PositiveInfinity : 0;
        _empty.Visibility = Visible(description is not null && !has);
    }

    private static Visibility Visible(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

    // Measures its child at a set height, which an element in a scroll viewer is never given otherwise. When the child's MinHeight,
    // known once it has measured, is more than that, the slot gives it that height and takes it, so the pane scrolls instead of
    // clipping the child.
    private sealed class EditorSlot : Decorator
    {
        private double _fillHeight;

        public double FillHeight
        {
            set
            {
                _fillHeight = value;
                InvalidateMeasure();
            }
        }

        protected override Size MeasureOverride(Size available)
        {
            if (Child is not FrameworkElement child)
            {
                return default;
            }

            var height = _fillHeight;
            child.Measure(new Size(available.Width, height));
            if (child.MinHeight > height)
            {
                height = child.MinHeight;
                child.Measure(new Size(available.Width, height));
            }

            return new Size(child.DesiredSize.Width, height);
        }
    }
}
