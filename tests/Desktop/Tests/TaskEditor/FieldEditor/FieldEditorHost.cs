using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.FieldEditor;

/// <summary>Makes the hosts of the field editors: <see cref="FieldEditorHost{TEditor}"/> for a Title editor or a Description editor.</summary>
internal static class FieldEditorHost
{
    /// <summary>Shows a Title editor holding the title, over a real Tasks with a repository.</summary>
    public static FieldEditorHost<TitleEditor> OpenTitle(string title = "")
    {
        var services = new EditorServices();
        services.UseRepository();
        var editor = services.CreateTitleEditor();
        editor.Value = title;
        var host = new FieldEditorHost<TitleEditor>(services, editor, EditorFields.Title);
        editor.SaveRequested += (_, _) => host.Saves++;
        editor.CancelRequested += (_, _) => host.Cancels++;
        return host;
    }

    /// <summary>Shows a Description editor holding the description, for a task id Tasks hasn't saved, over a real Tasks with a repository, in a window of the height.</summary>
    public static FieldEditorHost<DescriptionEditor> OpenDescription(string description = "", double height = 300)
    {
        var services = new EditorServices();
        services.UseRepository();
        var editor = services.CreateDescriptionEditor();
        editor.TaskId = services.Tasks.StartDraft().TaskId;
        editor.Value = description;
        var host = new FieldEditorHost<DescriptionEditor>(services, editor, EditorFields.Description, height);
        editor.SaveRequested += (_, _) => host.Saves++;
        editor.CancelRequested += (_, _) => host.Cancels++;
        return host;
    }
}

/// <summary>
/// A <see cref="TitleEditor"/> or a <see cref="DescriptionEditor"/> in a showing window, counting the requests it raises, and
/// driving its one field as a user does. Shared test setup. Dispose it to close the window.
/// </summary>
internal sealed class FieldEditorHost<TEditor> : IDisposable
    where TEditor : FrameworkElement
{
    private readonly Window window;
    private readonly string fieldName;

    internal FieldEditorHost(EditorServices services, TEditor editor, string fieldName, double height = 300)
    {
        Services = services;
        Editor = editor;
        this.fieldName = fieldName;
        window = new Window { Content = editor, Width = 480, Height = height };
        window.Show();
        Focus();
    }

    public EditorServices Services { get; }

    public TEditor Editor { get; }

    /// <summary>How many times the editor asked to save.</summary>
    public int Saves { get; internal set; }

    /// <summary>How many times the editor asked to cancel.</summary>
    public int Cancels { get; internal set; }

    /// <summary>Gives the field the keyboard focus.</summary>
    public void Focus()
    {
        var element = EditorFields.Field(Editor, fieldName);
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Puts the text in the field, selects <paramref name="length"/> characters from <paramref name="start"/>, and gives the field the focus.</summary>
    public void Edit(string text, int start = 0, int length = 0)
    {
        EditorFields.Type(Editor, fieldName, text);
        Focus();
        EditorFields.Select(Editor, fieldName, start, length);
    }

    /// <summary>What the field's text holds now.</summary>
    public string Text => EditorFields.TextOf(Editor, fieldName);

    /// <summary>Where the field's selection starts and how long it is; length 0 is the cursor.</summary>
    public (int Start, int Length) Selection => EditorFields.SelectionOf(Editor, fieldName);

    /// <summary>Whether the field holds the keyboard focus.</summary>
    public bool FieldHasFocus => EditorFields.FocusedFieldName() == fieldName;

    /// <summary>Presses the keys on the element that has the keyboard focus, in order, and releases them in reverse.</summary>
    public void Press(params Key[] keys) => EditorFields.PressOnFocused(keys);

    /// <summary>Types the text at the cursor of the focused field, one character at a time.</summary>
    public void Type(string text) => EditorFields.TypeOnFocused(text);

    /// <summary>The texts the user can see in the editor beside its field, such as its message line.</summary>
    public IReadOnlyList<string> Texts => EditorFields.VisibleTexts(Editor);

    /// <summary>Whether a displayed text contains the words, ignoring case.</summary>
    public bool ShowsText(string words) => Texts.Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the field carries the status text that marks it as empty.</summary>
    public bool FieldIsMarked => !string.IsNullOrEmpty(AutomationProperties.GetItemStatus(EditorFields.Field(Editor, fieldName)));

    /// <summary>The editor's Save button.</summary>
    public Button SaveButton => OpenWindows.Descendants(Editor).OfType<Button>().Single(b => b.Content as string == "Save");

    /// <summary>Gives the window the theme the platform gives the app's windows, so the theme's resources resolve.</summary>
    public void UseTheme(bool dark) => OpenWindows.UseTheme(window, dark);

    /// <summary>Lays the window out, as on screen.</summary>
    public void Layout() => window.UpdateLayout();

    /// <summary>Where the element is in the editor after layout, and how tall the editor is.</summary>
    public (Rect Place, double EditorHeight) PlaceOf(FrameworkElement element)
    {
        window.UpdateLayout();
        var place = new Rect(element.TranslatePoint(new Point(0, 0), Editor), new Size(element.ActualWidth, element.ActualHeight));
        return (place, Editor.ActualHeight);
    }

    /// <summary>The visible text block with the text, such as the message line.</summary>
    public FrameworkElement TextBlockShowing(string text) =>
        OpenWindows.Descendants(Editor).OfType<TextBlock>().Single(block => block.IsVisible && block.Text == text);

    public void Dispose() => window.Close();
}
