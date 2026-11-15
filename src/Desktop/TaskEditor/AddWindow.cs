using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Creates the Add window.</summary>
public static class AddWindow
{
    /// <summary>Creates the Add window. It opens only on <see cref="IAddWindow.Show"/>.</summary>
    public static IAddWindow Create(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
        IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files,
        CloseWindowKeys closeWindowKeys, IUpdateStatus updates, SaveKeys saveKeys)
    {
        ArgumentNullException.ThrowIfNull(tagColors);
        ArgumentNullException.ThrowIfNull(closeWindowKeys);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(saveKeys);
        var window = new AddWindowView(tasks, new TaskEditorControl(tasks, tagColors, palette, theme, time, clipboard, imageFiles, files, saveKeys), updates, saveKeys);
        closeWindowKeys.Attach(window);
        return window;
    }
}

/// <summary>The Add window, as Shell uses it.</summary>
public interface IAddWindow
{
    /// <summary>Shows the Add window, with the draft if there is one.</summary>
    void Show();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }

    /// <summary>Hides an open Add window and keeps its draft, as Esc does. Does nothing when it isn't open.</summary>
    void Close();
}
