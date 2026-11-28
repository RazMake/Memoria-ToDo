using System.Text;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>One call a test made to <see cref="FakeImageEncoder"/>'s save methods. <paramref name="Quality"/> is null for PNG.</summary>
public sealed record EncoderCall(ImageFormat Format, int LongestSide, int? Quality);

/// <summary>
/// The shared fake image tools. Its "images" are bytes that carry their own format and size, made by <see cref="Image"/>,
/// so <see cref="Read"/> can read them. A save returns the image scaled down (never up) to the asked longest side,
/// as many bytes as <see cref="OutputSize"/> says. It records every save in <see cref="Calls"/>.
/// </summary>
public sealed class FakeImageEncoder : IImageEncoder
{
    private const string Prefix = "fake-image:";
    private readonly List<EncoderCall> calls = [];

    /// <summary>The bytes a save returns, from the saved format, width, height, and quality (0 for PNG). Starts at 1,000.</summary>
    public Func<ImageFormat, int, int, int, int> OutputSize { get; set; } = (_, _, _, _) => 1_000;

    /// <summary>Every save so far, in order.</summary>
    public IReadOnlyList<EncoderCall> Calls => calls;

    /// <summary>An "image" of the format and size, in at least <paramref name="size"/> bytes.</summary>
    public static byte[] Image(ImageFormat format, int width, int height, int size = 0)
    {
        var header = Encoding.ASCII.GetBytes($"{Prefix}{format}:{width}x{height}:");
        var bytes = new byte[Math.Max(size, header.Length)];
        Array.Fill(bytes, (byte)'x');
        header.CopyTo(bytes, 0);
        return bytes;
    }

    /// <inheritdoc />
    public ImageInfo Read(byte[] image)
    {
        var text = Encoding.ASCII.GetString(image, 0, Math.Min(image.Length, 64));
        if (text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var parts = text[Prefix.Length..].Split(':');
            var size = parts[1].Split('x');
            if (Enum.TryParse<ImageFormat>(parts[0], out var format) && size.Length == 2
                && int.TryParse(size[0], out var width) && int.TryParse(size[1], out var height))
                return new ImageInfo(format, width, height);
        }

        throw new InvalidDataException("Not an image.");
    }

    /// <inheritdoc />
    public byte[] SavePng(byte[] image, int longestSide)
    {
        calls.Add(new EncoderCall(ImageFormat.Png, longestSide, null));
        return Save(image, ImageFormat.Png, longestSide, 0);
    }

    /// <inheritdoc />
    public byte[] SaveJpeg(byte[] image, int longestSide, int quality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);
        calls.Add(new EncoderCall(ImageFormat.Jpeg, longestSide, quality));
        return Save(image, ImageFormat.Jpeg, longestSide, quality);
    }

    private byte[] Save(byte[] image, ImageFormat format, int longestSide, int quality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longestSide, 1);
        var info = Read(image);
        var longest = Math.Max(info.Width, info.Height);
        var scale = longest <= longestSide ? 1.0 : (double)longestSide / longest;
        var width = Math.Max(1, (int)Math.Round(info.Width * scale));
        var height = Math.Max(1, (int)Math.Round(info.Height * scale));
        return Image(format, width, height, OutputSize(format, width, height, quality));
    }
}
