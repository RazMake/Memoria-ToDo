namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The system clipboard.</summary>
public interface IClipboard
{
    /// <summary>The clipboard's text, or null when it holds none.</summary>
    string? Text();

    /// <summary>The clipboard's image as PNG, or null when it holds none.</summary>
    byte[]? ImagePng();
}
