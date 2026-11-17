using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Shows a <see cref="TagEditor"/> holding the tag in a window over a real Tasks (with a repository) and the fake tag
/// colors, records what it raises, and drives it as a user does. Dispose it to close the window. Shared test setup.
/// </summary>
internal sealed class TagEditorWindow : IDisposable
{
    private readonly Window window;
    private Window? other;

    public TagEditorWindow(string? tag = null)
    {
        Services = new EditorServices();
        Services.UseRepository();
        Editor = Services.CreateTagEditor();
        Editor.TagChanged += (_, changed) => Changes.Add((changed, Editor.TaskTag, [.. Services.TagColors.Kept]));
        Editor.TaskTag = tag;
        window = new Window { Content = Editor, Width = 480, Height = 400 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagEditor Editor { get; }

    /// <summary>Each TagChanged: the tag it carried, the tag the editor held and the colors kept when it was raised.</summary>
    public List<(string? Tag, string? TagThen, IReadOnlyList<(string Tag, string Color)> KeptThen)> Changes { get; } = [];

    public void Remove() => LoadSync.Remove(window, Editor);

    public void Put() => LoadSync.Put(window, Editor);

    /// <summary>The words the pill shows now.</summary>
    public string PillText => TagControls.PillText(Editor);

    /// <summary>Clicks the pill, away from its x.</summary>
    public void ClickPill() => OpenWindows.Click(TagControls.PillButton(Editor));

    /// <summary>Clicks the x of the pill.</summary>
    public void ClickClear() => OpenWindows.Click(TagControls.ClearButton(Editor));

    /// <summary>Clicks the pill and then Custom.</summary>
    public void OpenCustom()
    {
        ClickPill();
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

    /// <summary>Makes another window the active one, as clicking another application does, so the editor's window deactivates.</summary>
    public void Deactivate()
    {
        other = new Window { Width = 100, Height = 100 };
        other.Show();
        other.Activate();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        if (window.IsActive)
        {
            throw new InvalidOperationException("The editor's window is still active.");
        }
    }

    public void Dispose()
    {
        other?.Close();
        window.Close();
    }
}
