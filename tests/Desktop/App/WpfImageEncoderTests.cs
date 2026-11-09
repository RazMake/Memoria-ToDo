using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The WPF image tools, on images made in memory by WPF's own encoders and read back by WPF's own decoders,
/// so each test checks the adapter against an independent reading.
/// </summary>
[Unit]
public sealed class WpfImageEncoderTests
{
    private readonly IImageEncoder encoder = new WpfImageEncoder();

    [Fact]
    public void Reads_a_PNG_with_its_size()
    {
        Assert.Equal(new ImageInfo(ImageFormat.Png, 40, 30), encoder.Read(Image(new PngBitmapEncoder(), 40, 30)));
    }

    [Fact]
    public void Reads_a_JPEG_with_its_size()
    {
        Assert.Equal(new ImageInfo(ImageFormat.Jpeg, 40, 30), encoder.Read(Image(new JpegBitmapEncoder(), 40, 30)));
    }

    [Fact]
    public void Reads_any_other_image_format_as_Other_with_its_size()
    {
        Assert.Equal(new ImageInfo(ImageFormat.Other, 40, 30), encoder.Read(Image(new BmpBitmapEncoder(), 40, 30)));
    }

    [Fact]
    public void Refuses_to_read_bytes_that_are_not_an_image()
    {
        Assert.Throws<InvalidDataException>(() => encoder.Read([1, 2, 3, 4]));
    }

    [Fact]
    public void Refuses_to_read_no_bytes()
    {
        Assert.Throws<InvalidDataException>(() => encoder.Read([]));
    }

    [Fact]
    public void Saves_a_wide_PNG_scaled_down_keeping_its_proportions()
    {
        var saved = encoder.SavePng(Image(new PngBitmapEncoder(), 400, 200), 100);

        Assert.Equal((100, 50), SizeOf(saved));
    }

    [Fact]
    public void Saves_a_tall_PNG_scaled_down_keeping_its_proportions()
    {
        var saved = encoder.SavePng(Image(new PngBitmapEncoder(), 200, 400), 100);

        Assert.Equal((50, 100), SizeOf(saved));
    }

    [Fact]
    public void Saves_a_PNG_as_PNG_whatever_the_source_format()
    {
        var saved = encoder.SavePng(Image(new JpegBitmapEncoder(), 40, 30), 100);

        Assert.IsType<PngBitmapDecoder>(BitmapDecoder.Create(new MemoryStream(saved), BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
    }

    [Fact]
    public void Never_scales_a_PNG_up()
    {
        var saved = encoder.SavePng(Image(new PngBitmapEncoder(), 40, 30), 1000);

        Assert.Equal((40, 30), SizeOf(saved));
    }

    [Fact]
    public void Saves_a_wide_JPEG_scaled_down_keeping_its_proportions()
    {
        var saved = encoder.SaveJpeg(Image(new PngBitmapEncoder(), 400, 200), 100, 80);

        Assert.Equal((100, 50), SizeOf(saved));
    }

    [Fact]
    public void Saves_a_JPEG_as_JPEG_whatever_the_source_format()
    {
        var saved = encoder.SaveJpeg(Image(new PngBitmapEncoder(), 40, 30), 100, 80);

        Assert.IsType<JpegBitmapDecoder>(BitmapDecoder.Create(new MemoryStream(saved), BitmapCreateOptions.None, BitmapCacheOption.OnLoad));
    }

    [Fact]
    public void Never_scales_a_JPEG_up()
    {
        var saved = encoder.SaveJpeg(Image(new PngBitmapEncoder(), 40, 30), 1000, 80);

        Assert.Equal((40, 30), SizeOf(saved));
    }

    [Fact]
    public void A_lower_JPEG_quality_gives_fewer_bytes()
    {
        var source = Image(new PngBitmapEncoder(), 200, 200, noisy: true);

        var low = encoder.SaveJpeg(source, 200, 10);
        var high = encoder.SaveJpeg(source, 200, 95);

        Assert.True(low.Length < high.Length, $"Quality 10 gave {low.Length} bytes, quality 95 gave {high.Length}.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Refuses_a_JPEG_quality_outside_1_to_100(int quality)
    {
        var source = Image(new PngBitmapEncoder(), 40, 30);

        Assert.Throws<ArgumentOutOfRangeException>(() => encoder.SaveJpeg(source, 100, quality));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Accepts_the_JPEG_quality_limits(int quality)
    {
        var saved = encoder.SaveJpeg(Image(new PngBitmapEncoder(), 40, 30), 100, quality);

        Assert.Equal((40, 30), SizeOf(saved));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Refuses_a_longest_side_below_1(int longestSide)
    {
        var source = Image(new PngBitmapEncoder(), 40, 30);

        Assert.Throws<ArgumentOutOfRangeException>(() => encoder.SavePng(source, longestSide));
        Assert.Throws<ArgumentOutOfRangeException>(() => encoder.SaveJpeg(source, longestSide, 80));
    }

    [Fact]
    public void Refuses_to_save_bytes_that_are_not_an_image()
    {
        Assert.Throws<InvalidDataException>(() => encoder.SavePng([1, 2, 3, 4], 100));
        Assert.Throws<InvalidDataException>(() => encoder.SaveJpeg([1, 2, 3, 4], 100, 80));
    }

    // An image made with the WPF encoder, so the adapter is checked against WPF itself rather than against its own output.
    private static byte[] Image(BitmapEncoder wpfEncoder, int width, int height, bool noisy = false)
    {
        var pixels = new byte[width * height * 4];
        if (noisy)
        {
            new Random(1).NextBytes(pixels);
        }
        else
        {
            Array.Fill(pixels, (byte)200);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        wpfEncoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        wpfEncoder.Save(stream);
        return stream.ToArray();
    }

    private static (int Width, int Height) SizeOf(byte[] image)
    {
        var frame = BitmapDecoder.Create(new MemoryStream(image), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }
}
