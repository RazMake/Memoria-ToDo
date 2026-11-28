using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Memoria.ToDo.Desktop.TaskEditor;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The WPF clipboard. Thin: the Task editor's tests use a fake, so no test touches the user's clipboard.</summary>
internal sealed class WpfClipboard : IClipboard
{
    public string? Text() => Clipboard.ContainsText() ? Clipboard.GetText() : null;

    /// <summary>The clipboard's image as PNG, or null when it has none. An image that disappears or can't be read between the check and the read is an <see cref="InvalidDataException"/>.</summary>
    public byte[]? ImagePng()
    {
        if (!Clipboard.ContainsImage())
        {
            return null;
        }

        try
        {
            return ImageBytes.Encode(new PngBitmapEncoder(), Clipboard.GetImage() ?? throw new InvalidDataException("The clipboard's image can't be read."));
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidDataException("The clipboard's image can't be read.", e);
        }
    }
}
