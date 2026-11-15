using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Shows a <see cref="TagEditor"/> holding the tags in a window over a real Tasks (with a repository) and the fake tag
/// colors, records what it raises, and drives it as a user does. Dispose it to close the window. Shared test setup.
/// </summary>
internal sealed class TagEditorWindow : IDisposable
{
    private readonly Window window;

    public TagEditorWindow(params string[] tags)
    {
        Services = new EditorServices();
        Services.UseRepository();
        Editor = Services.CreateTagEditor();
        Editor.Tags = tags;
        Editor.TagAdded += (_, tag) => Added.Add((tag, Editor.Tags, [.. Services.TagColors.Kept]));
        Editor.TagRemoved += (_, tag) => Removed.Add((tag, Editor.Tags));
        window = new Window { Content = Editor, Width = 480, Height = 400 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagEditor Editor { get; }

    /// <summary>Each TagAdded: the tag, the tags the editor held and the colors kept when it was raised.</summary>
    public List<(string Tag, IReadOnlyList<string> TagsThen, IReadOnlyList<(string Tag, string Color)> KeptThen)> Added { get; } = [];

    /// <summary>Each TagRemoved: the tag, and the tags the editor held when it was raised.</summary>
    public List<(string Tag, IReadOnlyList<string> TagsThen)> Removed { get; } = [];

    public void Remove() => LoadSync.Remove(window, Editor);

    public void Put() => LoadSync.Put(window, Editor);

    /// <summary>Clicks +.</summary>
    public void ClickPlus() => OpenWindows.Click(TagControls.Plus(Editor));

    /// <summary>Clicks + and then Custom.</summary>
    public void OpenCustom()
    {
        ClickPlus();
        OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    /// <summary>Sets what the user has typed in the tag box.</summary>
    public void Type(string text) => EditorFields.Type(Editor, EditorFields.Tags, text);

    /// <summary>The text in the tag box.</summary>
    public string Typed => EditorFields.TextOf(Editor, EditorFields.Tags);

    /// <summary>Gives the keyboard focus to the element with the automation id.</summary>
    public void Focus(string id)
    {
        var element = EditorFields.Field(Editor, id);
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Gives the keyboard focus to the element.</summary>
    public static void Focus(UIElement element)
    {
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Presses the keys on the element that has the keyboard focus.</summary>
    public void Press(params Key[] keys) => EditorFields.PressOnFocused(keys);

    /// <summary>The texts the user can see beside the controls, such as the message under the colors.</summary>
    public IReadOnlyList<string> Texts => EditorFields.VisibleTexts(Editor);

    public void Dispose() => window.Close();
}
