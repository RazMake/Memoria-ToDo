using System.IO;
using System.Windows.Media.Imaging;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Turns a WPF image into the bytes of an image file.</summary>
internal static class ImageBytes
{
    public static byte[] Encode(BitmapEncoder encoder, BitmapSource image)
    {
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
