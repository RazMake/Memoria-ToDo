using System.IO;
using System.Windows.Media.Imaging;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Image tools built on WPF's imaging.</summary>
internal sealed class WpfImageEncoder : IImageEncoder
{
    public ImageInfo Read(byte[] image)
    {
        var decoder = Decode(image);
        var frame = decoder.Frames[0];
        var format = decoder switch
        {
            PngBitmapDecoder => ImageFormat.Png,
            JpegBitmapDecoder => ImageFormat.Jpeg,
            _ => ImageFormat.Other,
        };
        return new ImageInfo(format, frame.PixelWidth, frame.PixelHeight);
    }

    public byte[] SavePng(byte[] image, int longestSide)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longestSide, 1);
        return ImageBytes.Encode(new PngBitmapEncoder(), Scaled(image, longestSide));
    }

    public byte[] SaveJpeg(byte[] image, int longestSide, int quality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longestSide, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);
        return ImageBytes.Encode(new JpegBitmapEncoder { QualityLevel = quality }, Scaled(image, longestSide));
    }

    // Never scales up, and keeps the proportions.
    private static BitmapSource Scaled(byte[] image, int longestSide)
    {
        var frame = Decode(image).Frames[0];
        var longest = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (longest <= longestSide)
        {
            return frame;
        }

        var scale = (double)longestSide / longest;
        return new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
    }

    private static BitmapDecoder Decode(byte[] image)
    {
        try
        {
            return BitmapDecoder.Create(new MemoryStream(image), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch (Exception error) when (error is NotSupportedException or FileFormatException or ArgumentException)
        {
            throw new InvalidDataException("The bytes aren't an image.", error);
        }
    }
}
