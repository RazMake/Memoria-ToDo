using System.Windows;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>Shows a <see cref="TaskEditorControl"/> in a window, which a test can take it out of and put it back in. Shared test setup.</summary>
internal sealed class EditorWindow : IDisposable
{
    public EditorWindow()
    {
        Services = new EditorServices();
        Services.UseRepository();
        Editor = Services.CreateEditor();
        Window = new Window { Content = Editor, Width = 480, Height = 300 };
        Window.Show();
    }

    public EditorServices Services { get; }

    public TaskEditorControl Editor { get; }

    public Window Window { get; }

    public void Remove() => LoadSync.Remove(Window, Editor);

    public void Put() => LoadSync.Put(Window, Editor);

    /// <summary>Clicks the pill and then Custom, which opens the tag box.</summary>
    public void OpenCustom()
    {
        OpenWindows.Click(TagControls.PillButton(Editor));
        OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    public void Dispose() => Window.Close();
}
