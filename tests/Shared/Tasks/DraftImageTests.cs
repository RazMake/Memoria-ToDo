using System.Text.RegularExpressions;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Pictures pasted into a draft: shrunk to at most 1 MB and kept beside the draft.</summary>
[Unit]
public sealed class DraftImageTests
{
    private const int OneMegabyte = 1_048_576;
    private static readonly Regex Markdown = new(@"^!\[(?<alt>.*)\]\(images/(?<task>[0-9a-f-]{36})/(?<image>[0-9a-f-]{36})\.(?<ext>png|jpg)\)$");

    private readonly TasksFixture f = new();
    private readonly string taskId = TasksFixture.Id(500);

    private string Save(byte[] image, string alt = "pic") => f.Tasks.SaveImage(taskId, image, alt);

    private byte[] SavedBytes(string markdown)
    {
        var m = Markdown.Match(markdown);
        Assert.True(m.Success, markdown);
        return f.Files.ReadFile(Path.Combine(f.DraftImages(taskId), m.Groups["image"].Value + "." + m.Groups["ext"].Value));
    }

    private static string Ext(string markdown) => Markdown.Match(markdown).Groups["ext"].Value;

    [Fact]
    public void Returns_markdown_pointing_at_images_task_id_image_id_and_extension()
    {
        var markdown = Save(FakeImageEncoder.Image(ImageFormat.Png, 100, 50, 500), "A cat");

        var m = Markdown.Match(markdown);
        Assert.True(m.Success, markdown);
        Assert.Equal("A cat", m.Groups["alt"].Value);
        Assert.Equal(taskId, m.Groups["task"].Value);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", m.Groups["image"].Value);
    }

    [Fact]
    public void Keeps_the_image_in_the_device_folder_under_the_task_id()
    {
        var markdown = Save(FakeImageEncoder.Image(ImageFormat.Png, 100, 50, 500));

        var m = Markdown.Match(markdown);
        Assert.True(f.Files.FileExists(Path.Combine(f.Device, "images", taskId, m.Groups["image"].Value + ".png")));
    }

    [Fact]
    public void Gives_each_image_its_own_id()
    {
        var first = Save(FakeImageEncoder.Image(ImageFormat.Png, 100, 50, 500));
        var second = Save(FakeImageEncoder.Image(ImageFormat.Png, 100, 50, 500));

        Assert.NotEqual(first, second);
        Assert.Equal(2, f.Files.ListFiles(f.DraftImages(taskId)).Count);
    }

    [Fact]
    public void Writes_nothing_inside_the_clone()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Save(FakeImageEncoder.Image(ImageFormat.Png, 100, 50, 500));

        Assert.Empty(f.Files.ListFiles(f.CloneImages(taskId)));
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.Jpeg)]
    public void Keeps_a_png_or_jpeg_of_at_most_1_MB_byte_for_byte(ImageFormat format)
    {
        var image = FakeImageEncoder.Image(format, 800, 600, 500_000);

        var markdown = Save(image);

        Assert.Equal(image, SavedBytes(markdown));
        Assert.Equal(format == ImageFormat.Png ? "png" : "jpg", Ext(markdown));
        Assert.Empty(f.Encoder.Calls);
    }

    [Fact]
    public void Keeps_a_png_of_exactly_1_MB_as_it_is()
    {
        var image = FakeImageEncoder.Image(ImageFormat.Png, 800, 600, OneMegabyte);

        Assert.Equal(image, SavedBytes(Save(image)));
    }

    [Fact]
    public void Keeps_a_small_image_as_it_is_even_when_it_is_wider_than_1920_pixels()
    {
        var image = FakeImageEncoder.Image(ImageFormat.Png, 6000, 3000, 900_000);

        var markdown = Save(image);

        Assert.Equal(image, SavedBytes(markdown));
        Assert.Empty(f.Encoder.Calls);
    }

    [Fact]
    public void Shrinks_a_png_that_is_a_byte_over_1_MB()
    {
        var image = FakeImageEncoder.Image(ImageFormat.Png, 800, 600, OneMegabyte + 1);

        var markdown = Save(image);

        Assert.NotEmpty(f.Encoder.Calls);
        Assert.True(SavedBytes(markdown).Length <= OneMegabyte);
    }

    [Fact]
    public void Scales_a_big_png_down_to_1920_pixels_on_its_longest_side_and_keeps_it_as_png()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 600_000;
        var image = FakeImageEncoder.Image(ImageFormat.Png, 4000, 2000, 5_000_000);

        var markdown = Save(image);

        Assert.Equal(new EncoderCall(ImageFormat.Png, 1920, null), Assert.Single(f.Encoder.Calls));
        Assert.Equal("png", Ext(markdown));
        Assert.Equal(f.Encoder.Read(SavedBytes(markdown)), new ImageInfo(ImageFormat.Png, 1920, 960));
    }

    [Fact]
    public void Saves_a_png_as_jpeg_when_it_is_still_over_1_MB_at_1920_pixels()
    {
        f.Encoder.OutputSize = (format, _, _, _) => format == ImageFormat.Png ? 3_000_000 : 400_000;
        var image = FakeImageEncoder.Image(ImageFormat.Png, 4000, 2000, 5_000_000);

        var markdown = Save(image);

        Assert.Equal("jpg", Ext(markdown));
        Assert.Equal(ImageFormat.Jpeg, f.Encoder.Read(SavedBytes(markdown)).Format);
        Assert.True(SavedBytes(markdown).Length <= OneMegabyte);
    }

    [Fact]
    public void Shrinks_a_jpeg_over_1_MB_to_1920_pixels_at_quality_85_first()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 700_000;
        var image = FakeImageEncoder.Image(ImageFormat.Jpeg, 4000, 3000, 5_000_000);

        var markdown = Save(image);

        Assert.Equal(new EncoderCall(ImageFormat.Jpeg, 1920, 85), Assert.Single(f.Encoder.Calls));
        Assert.Equal("jpg", Ext(markdown));
    }

    [Fact]
    public void Lowers_the_jpeg_quality_until_it_fits_and_stops_at_the_first_fit()
    {
        f.Encoder.OutputSize = (_, _, _, quality) => quality >= 75 ? 2_000_000 : 900_000;
        var image = FakeImageEncoder.Image(ImageFormat.Jpeg, 4000, 3000, 5_000_000);

        var markdown = Save(image);

        Assert.Equal([85, 75, 65], f.Encoder.Calls.Select(c => c.Quality));
        Assert.All(f.Encoder.Calls, c => Assert.Equal(1920, c.LongestSide));
        Assert.Equal(900_000, SavedBytes(markdown).Length);
    }

    [Fact]
    public void Makes_the_jpeg_smaller_in_steps_when_the_lowest_quality_still_does_not_fit()
    {
        f.Encoder.OutputSize = (_, w, h, _) => w * h < 1_300_000 ? 900_000 : 2_000_000;
        var image = FakeImageEncoder.Image(ImageFormat.Jpeg, 4000, 3000, 5_000_000);

        var markdown = Save(image);

        var calls = f.Encoder.Calls;
        Assert.Equal(new EncoderCall(ImageFormat.Jpeg, 1920, 85), calls[0]);
        Assert.Equal([85, 75, 65, 55], calls.Take(4).Select(c => c.Quality));
        Assert.All(calls.Skip(3), c => Assert.Equal(55, c.Quality));
        Assert.Equal(1440, calls[4].LongestSide);
        Assert.True(calls.Skip(3).Select(c => c.LongestSide).SequenceEqual(calls.Skip(3).Select(c => c.LongestSide).OrderDescending()));
        Assert.True(SavedBytes(markdown).Length <= OneMegabyte);
    }

    [Fact]
    public void Keeps_the_first_size_that_fits_when_shrinking_in_steps()
    {
        f.Encoder.OutputSize = (_, w, h, _) => Math.Max(w, h) <= 1080 ? 900_000 : 2_000_000;
        var image = FakeImageEncoder.Image(ImageFormat.Jpeg, 4000, 3000, 5_000_000);

        var info = f.Encoder.Read(SavedBytes(Save(image)));

        Assert.Equal(new ImageInfo(ImageFormat.Jpeg, 1080, 810), info);
        Assert.Equal(1080, f.Encoder.Calls[^1].LongestSide);
    }

    [Fact]
    public void Tries_as_small_as_256_pixels_before_giving_up()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 2_000_000;
        var image = FakeImageEncoder.Image(ImageFormat.Jpeg, 4000, 3000, 5_000_000);

        Assert.Throws<ImageTooLargeException>(() => Save(image));

        Assert.Equal(new EncoderCall(ImageFormat.Jpeg, 256, 55), f.Encoder.Calls[^1]);
    }

    [Fact]
    public void Saves_nothing_when_the_image_cannot_be_shrunk_to_1_MB()
    {
        f.Encoder.OutputSize = (_, _, _, _) => 2_000_000;
        var image = FakeImageEncoder.Image(ImageFormat.Png, 4000, 3000, 5_000_000);

        Assert.Throws<ImageTooLargeException>(() => Save(image));

        Assert.Empty(f.Files.ListFiles(f.DraftImages(taskId)));
    }

    [Fact]
    public void Converts_other_image_formats_to_png()
    {
        var image = FakeImageEncoder.Image(ImageFormat.Other, 300, 200, 20_000);

        var markdown = Save(image);

        Assert.Equal("png", Ext(markdown));
        Assert.Equal(ImageFormat.Png, f.Encoder.Read(SavedBytes(markdown)).Format);
    }

    [Fact]
    public void Refuses_bytes_that_are_not_an_image()
    {
        Assert.Throws<InvalidDataException>(() => Save([1, 2, 3, 4]));
    }

    [Fact]
    public void Saves_nothing_for_bytes_that_are_not_an_image()
    {
        Assert.Throws<InvalidDataException>(() => Save([1, 2, 3, 4]));

        Assert.Empty(f.Files.ListFiles(f.DraftImages(taskId)));
        Assert.Empty(f.Encoder.Calls);
    }

    [Theory]
    [InlineData("A [bracketed] name", @"A \[bracketed\] name")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("plain", "plain")]
    public void Escapes_brackets_and_backslashes_in_the_alt_text(string alt, string escaped)
    {
        var markdown = Save(FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), alt);

        Assert.StartsWith($"![{escaped}](images/{taskId}/", markdown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../evil")]
    [InlineData("short")]
    [InlineData("01900000-0000-7000-8000-00000000000G")]
    public void Refuses_a_task_id_that_is_not_a_valid_id_and_saves_nothing(string badId)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            f.Tasks.SaveImage(badId, FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), "pic"));

        Assert.Empty(f.Files.ListFiles(Path.Combine(f.Device, "images")));
        Assert.Empty(f.Files.ListFiles(f.Device));
    }

    [Fact]
    public void Keeps_each_task_ids_images_apart()
    {
        f.Tasks.SaveImage(TasksFixture.Id(1), FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), "a");
        f.Tasks.SaveImage(TasksFixture.Id(2), FakeImageEncoder.Image(ImageFormat.Png, 10, 10, 200), "b");

        Assert.Single(f.Files.ListFiles(f.DraftImages(TasksFixture.Id(1))));
        Assert.Single(f.Files.ListFiles(f.DraftImages(TasksFixture.Id(2))));
    }
}
