namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The dialog that chooses an image file.</summary>
public interface IImageFileChooser
{
    /// <summary>The chosen file's full path, or null when the user cancels.</summary>
    string? Choose();
}
