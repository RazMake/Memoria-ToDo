using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Creates the Add window.</summary>
public static class AddWindow
{
    /// <summary>Creates the Add window. It opens only on <see cref="IAddWindow.Show"/>.</summary>
    public static IAddWindow Create(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files) =>
        new AddWindowView(tasks, new TaskEditorControl(tasks, palette, theme, time, clipboard, imageFiles, files));
}

/// <summary>The Add window, as Shell uses it.</summary>
public interface IAddWindow
{
    /// <summary>Shows the Add window, with the draft if there is one.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }
}
