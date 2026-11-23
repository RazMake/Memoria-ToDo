using System.Windows;
using System.Windows.Input;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>Hosts one editor in a showing window and counts the requests it raises. Shared test setup.</summary>
internal sealed class EditorHost : IDisposable
{
    private readonly Window window;
    private int save;
    private int saveAndClear;
    private int cancel;

    /// <param name="height">The height the window gives the editor, as a container with a set height does.</param>
    public EditorHost(double height = 300)
    {
        Services = new EditorServices();
        Editor = Services.CreateEditor();
        Editor.SaveRequested += (_, _) => save++;
        Editor.SaveAndClearRequested += (_, _) => saveAndClear++;
        Editor.CancelRequested += (_, _) => cancel++;
        window = new Window { Content = Editor, Width = 480 };
        if (double.IsNaN(height))
        {
            window.SizeToContent = SizeToContent.Height; // As the Add window, which grows with its content.
        }
        else
        {
            window.Height = height;
        }

        window.Show();
        EditorFields.Field(Editor, EditorFields.Title).Focus();
    }

    public EditorServices Services { get; }

    /// <summary>Gives the window the theme the platform gives the app's windows, so the theme's resources resolve.</summary>
    public void UseTheme(bool dark) => Memoria.ToDo.Desktop.Fakes.OpenWindows.UseTheme(window, dark);

    /// <summary>Lays the window out, as on screen, where the tags are laid out before a key reaches them.</summary>
    public void Layout() => window.UpdateLayout();

    /// <summary>Where the element is in the editor after layout, and how tall the editor is.</summary>
    public (Rect Place, double EditorHeight) PlaceOf(FrameworkElement element)
    {
        window.UpdateLayout();
        var place = new Rect(element.TranslatePoint(new Point(0, 0), Editor), new Size(element.ActualWidth, element.ActualHeight));
        return (place, Editor.ActualHeight);
    }

    /// <summary>Shows the text on the editor's message line.</summary>
    public void ShowMessage(string text) => ((IDraftEditor)Editor).ShowMessage(text);

    /// <summary>The visible text block with the text, such as the message line.</summary>
    public FrameworkElement TextBlockShowing(string text) =>
        Memoria.ToDo.Desktop.Fakes.OpenWindows.Descendants(Editor).OfType<System.Windows.Controls.TextBlock>()
            .Single(block => block.IsVisible && block.Text == text);

    public TaskEditorControl Editor { get; }

    public (int Save, int SaveAndClear, int Cancel) Counts => (save, saveAndClear, cancel);

    /// <summary>The texts the editor displays beside its controls, such as its message line.</summary>
    public IReadOnlyList<string> Texts => EditorFields.VisibleTexts(window);

    /// <summary>Whether a displayed text contains the words, ignoring case.</summary>
    public bool ShowsText(string words) => Texts.Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the Title field carries the status text that marks it as empty.</summary>
    public bool TitleIsMarked =>
        !string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetItemStatus(EditorFields.Field(Editor, EditorFields.Title)));

    /// <summary>Gives the keyboard focus to the field.</summary>
    public void Focus(string field)
    {
        var element = EditorFields.Field(Editor, field);
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Clicks the pill and then Custom, which opens the tag box.</summary>
    public void OpenCustom()
    {
        Memoria.ToDo.Desktop.Fakes.OpenWindows.Click(TagControls.PillButton(Editor));
        Memoria.ToDo.Desktop.Fakes.OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    /// <summary>The name of the field that holds the keyboard focus.</summary>
    public string? FocusedField => EditorFields.FocusedFieldName();

    /// <summary>Puts the text in the field, as typing does.</summary>
    public void Type(string field, string text) => EditorFields.Type(Editor, field, text);

    public void Press(params Key[] keys) => Memoria.ToDo.Desktop.Fakes.SimulatedKey.Chord(
        Keyboard.FocusedElement as UIElement ?? Editor, keys);

    public void Dispose() => window.Close();
}
