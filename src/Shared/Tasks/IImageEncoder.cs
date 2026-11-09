namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Each app's image tools. Tasks runs the shrinking steps through it.</summary>
public interface IImageEncoder
{
    /// <summary>The image's format and size. Throws <see cref="InvalidDataException"/> when it isn't an image.</summary>
    ImageInfo Read(byte[] image);

    /// <summary>The image as PNG, scaled so its longest side is at most <paramref name="longestSide"/> pixels.</summary>
    byte[] SavePng(byte[] image, int longestSide);

    /// <summary>The image as JPEG at quality 1–100, scaled so its longest side is at most <paramref name="longestSide"/> pixels.</summary>
    byte[] SaveJpeg(byte[] image, int longestSide, int quality);
}

/// <summary>An image file's format; anything but PNG and JPEG is <see cref="Other"/>.</summary>
public enum ImageFormat { Png, Jpeg, Other }

/// <summary>An image's format and size in pixels.</summary>
public sealed record ImageInfo(ImageFormat Format, int Width, int Height);
