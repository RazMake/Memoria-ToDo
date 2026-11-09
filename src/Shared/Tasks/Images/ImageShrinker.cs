namespace Memoria.ToDo.Shared.Tasks;

/// <summary>An image ready to keep: its bytes and the extension its file gets.</summary>
internal sealed record EncodedImage(byte[] Bytes, string Extension);

/// <summary>Brings an image down to 1 MB, keeping PNG and JPEG as they are when they already fit.</summary>
internal sealed class ImageShrinker(IImageEncoder encoder)
{
    private const int MaxBytes = 1024 * 1024;
    private const int FirstSide = 1920;
    private const int SmallestSide = 256;
    private static readonly int[] Qualities = [85, 75, 65, 55];

    /// <summary>Throws <see cref="InvalidDataException"/> when it isn't an image, and <see cref="ImageTooLargeException"/> when nothing fits.</summary>
    public EncodedImage Shrink(byte[] image)
    {
        var info = encoder.Read(image);
        var isPng = info.Format != ImageFormat.Jpeg;
        if (info.Format == ImageFormat.Other)
            image = encoder.SavePng(image, Math.Max(info.Width, info.Height));
        if (image.Length <= MaxBytes)
            return Encoded(image, isPng);

        if (isPng && encoder.SavePng(image, FirstSide) is { Length: <= MaxBytes } png)
            return Encoded(png, true);

        foreach (var quality in Qualities)
            if (encoder.SaveJpeg(image, FirstSide, quality) is { Length: <= MaxBytes } jpeg)
                return Encoded(jpeg, false);

        var side = FirstSide;
        while (side > SmallestSide)
        {
            side = Math.Max(SmallestSide, side * 3 / 4);
            if (encoder.SaveJpeg(image, side, Qualities[^1]) is { Length: <= MaxBytes } jpeg)
                return Encoded(jpeg, false);
        }

        throw new ImageTooLargeException();
    }

    private static EncodedImage Encoded(byte[] bytes, bool isPng) => new(bytes, isPng ? "png" : "jpg");
}
