using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// A saved window placement: one window's size, place, and maximized state in one small versioned JSON file on this
/// device. Reads and writes the file only; choosing where a window opens is <see cref="SavedPlaceChoiceTests"/>.
/// </summary>
[Unit]
public class SavedWindowPlacementTests
{
    private const string Folder = @"C:\Memoria\Device";
    private const string Path = Folder + @"\task-view-place.json";
    private const string Name = "Task View's place";

    private readonly FakeFileSystem _files = new();
    private readonly InMemoryLogSink _log = new();

    private SavedWindowPlacement Create() => new(_files, Path, Name, _log.CreateLogger());

    private void Save(string text) => _files.WriteFile(Path, Encoding.UTF8.GetBytes(text));

    private string Saved() => Encoding.UTF8.GetString(_files.ReadFile(Path));

    private IReadOnlyList<LogEvent> Warnings() => _log.Events.Where(e => e.Level == LogEventLevel.Warning).ToList();

    private const string Valid = """{"version":1,"left":120,"top":80,"width":1100,"height":720,"maximized":false}""";

    [Fact]
    public void Gives_no_place_for_a_missing_file_and_logs_no_Warning()
    {
        Assert.Null(Create().Read());
        Assert.Empty(Warnings());
    }

    [Fact]
    public void Reads_the_place_the_file_holds()
    {
        Save(Valid);

        Assert.Equal(new SavedPlace(120, 80, 1100, 720, false), Create().Read());
        Assert.Empty(Warnings());
    }

    [Fact]
    public void Reads_a_maximized_place()
    {
        Save("""{"version":1,"left":120,"top":80,"width":1100,"height":720,"maximized":true}""");

        Assert.Equal(new SavedPlace(120, 80, 1100, 720, true), Create().Read());
    }

    [Fact]
    public void Reads_a_negative_left_and_top_of_a_screen_left_of_or_above_the_main_one()
    {
        Save("""{"version":1,"left":-1800,"top":-40,"width":900,"height":600,"maximized":false}""");

        Assert.Equal(new SavedPlace(-1800, -40, 900, 600, false), Create().Read());
    }

    [Fact]
    public void Counts_a_file_from_a_newer_version_as_missing_and_logs_a_Warning_naming_the_file()
    {
        Save("""{"version":2,"left":120,"top":80,"width":1100,"height":720,"maximized":false,"dock":"left"}""");

        Assert.Null(Create().Read());
        Assert.Contains(Path, Assert.Single(Warnings()).RenderMessage());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"left":120,"top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":0,"left":120,"top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":"1","left":120,"top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":1100,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":1100,"height":720}""")]
    [InlineData("""{"version":1,"left":"120","top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120.5,"top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":1100,"height":720,"maximized":"no"}""")]
    public void Counts_a_file_that_cannot_be_read_as_missing_and_logs_a_Warning_naming_the_file(string text)
    {
        Save(text);

        Assert.Null(Create().Read());
        Assert.Contains(Path, Assert.Single(Warnings()).RenderMessage());
    }

    [Theory]
    [InlineData("""{"version":1,"left":120,"top":80,"width":0,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":1100,"height":0,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":-300,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":1100,"height":-1,"maximized":false}""")]
    public void Counts_a_place_with_no_size_as_missing_and_logs_a_Warning_naming_the_file(string text)
    {
        Save(text);

        Assert.Null(Create().Read());
        Assert.Contains(Path, Assert.Single(Warnings()).RenderMessage());
    }

    [Theory]
    [InlineData("""{"version":1,"left":3000000000,"top":80,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":-3000000000,"width":1100,"height":720,"maximized":false}""")]
    [InlineData("""{"version":1,"left":120,"top":80,"width":3000000000,"height":720,"maximized":false}""")]
    public void Counts_a_place_beyond_the_range_of_pixels_as_missing_and_logs_a_Warning_naming_the_file(string text)
    {
        Save(text);

        Assert.Null(Create().Read());
        Assert.Contains(Path, Assert.Single(Warnings()).RenderMessage());
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Counts_a_file_that_cannot_be_opened_as_missing_and_logs_a_Warning_with_the_exception(Type failureType)
    {
        Save(Valid);
        _files.FailReadsUnder(Folder, (Exception)Activator.CreateInstance(failureType, "No access.")!);

        Assert.Null(Create().Read());

        var warning = Assert.Single(Warnings());
        Assert.IsType(failureType, warning.Exception);
        Assert.Contains(Path, warning.RenderMessage());
    }

    [Fact]
    public void Writes_the_place_as_versioned_JSON_in_the_platforms_file_style()
    {
        Create().Write(new SavedPlace(120, 80, 1100, 720, false));

        Assert.Equal(
            "{\n  \"version\": 1,\n  \"left\": 120,\n  \"top\": 80,\n  \"width\": 1100,\n  \"height\": 720,\n  \"maximized\": false\n}",
            Saved());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reads_back_the_place_it_wrote(bool maximized)
    {
        var placement = Create();
        var place = new SavedPlace(-20, 15, 640, 480, maximized);

        placement.Write(place);

        Assert.Equal(place, placement.Read());
        Assert.Empty(Warnings());
    }

    [Fact]
    public void Writing_replaces_what_the_file_held()
    {
        Save(Valid);
        var placement = Create();

        placement.Write(new SavedPlace(1, 2, 300, 400, true));
        placement.Write(new SavedPlace(5, 6, 700, 800, false));

        Assert.Equal(new SavedPlace(5, 6, 700, 800, false), placement.Read());
    }

    [Fact]
    public void Writing_makes_a_file_that_could_not_be_read_readable_again()
    {
        Save("not json");
        var placement = Create();
        Assert.Null(placement.Read());

        placement.Write(new SavedPlace(120, 80, 1100, 720, false));

        Assert.Equal(new SavedPlace(120, 80, 1100, 720, false), placement.Read());
    }

    [Fact]
    public void A_failed_write_does_not_throw_logs_a_Warning_with_the_exception_and_keeps_the_old_place()
    {
        Save(Valid);
        _files.FailWritesUnder(Folder);
        var placement = Create();

        placement.Write(new SavedPlace(1, 2, 300, 400, true));

        var warning = Assert.Single(Warnings());
        Assert.IsType<IOException>(warning.Exception);
        Assert.Contains(Path, warning.RenderMessage());
        Assert.Equal(new SavedPlace(120, 80, 1100, 720, false), placement.Read());
    }

    [Fact]
    public void Creation_does_not_touch_the_file_system()
    {
        Create();

        Assert.Empty(_files.AllFilesUnder(Folder));
    }

    [Fact]
    public void Rejects_a_missing_file_system()
    {
        Assert.Throws<ArgumentNullException>(() => new SavedWindowPlacement(null!, Path, Name, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_a_missing_path()
    {
        Assert.Throws<ArgumentNullException>(() => new SavedWindowPlacement(_files, null!, Name, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_a_missing_name()
    {
        Assert.Throws<ArgumentNullException>(() => new SavedWindowPlacement(_files, Path, null!, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_a_missing_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new SavedWindowPlacement(_files, Path, Name, null!));
    }

    [Theory]
    [InlineData("place.json")]
    [InlineData(@"Device\place.json")]
    [InlineData("")]
    public void Rejects_a_path_that_is_not_fully_qualified(string path)
    {
        Assert.Throws<ArgumentException>(() => new SavedWindowPlacement(_files, path, Name, _log.CreateLogger()));
    }

    [Fact]
    public void Rejects_an_empty_name()
    {
        Assert.Throws<ArgumentException>(() => new SavedWindowPlacement(_files, Path, "", _log.CreateLogger()));
    }
}
