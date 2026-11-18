using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>The color name kept for each tag, in tags.json.</summary>
[Unit]
public sealed class TagColorsTests
{
    private const string NowText = "2026-11-12T08:30:15.123Z";
    private const string Earlier = "2026-01-01T00:00:00.000Z";
    private readonly TasksFixture f = new();

    private ITagColors Colors => f.Store.TagColors;

    private (Func<int> Colors, Func<int> Saved) CountNotices()
    {
        var colors = 0;
        var saved = 0;
        Colors.Changed += (_, _) => colors++;
        f.Repository.LocalSaved += (_, _) => saved++;
        return (() => colors, () => saved);
    }

    private (int Version, List<(string Tag, string Color, string Updated)> Entries) Read()
    {
        using var json = f.ReadJson(f.TagColorsFile);
        var entries = json.RootElement.GetProperty("tags").EnumerateArray()
            .Select(e => (e.GetProperty("tag").GetString()!, e.GetProperty("color").GetProperty("value").GetString()!, e.GetProperty("color").GetProperty("updated").GetString()!))
            .ToList();
        return (json.RootElement.GetProperty("version").GetInt32(), entries);
    }

    // Keeping a color

    [Fact]
    public void Creates_the_file_with_the_tag_color_and_current_time()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Colors.SetColor("work", "teal");

        Assert.Equal(2, Read().Version);
        Assert.Equal([("work", "teal", NowText)], Read().Entries);
        Assert.Equal("teal", Colors.ColorOf("work"));
    }

    [Fact]
    public void Writes_the_field_order_version_then_tags()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Colors.SetColor("work", "teal");

        using var json = f.ReadJson(f.TagColorsFile);
        Assert.Equal(["version", "tags"], json.RootElement.EnumerateObject().Select(p => p.Name).ToList());
        Assert.Equal(["tag", "color", "place"], json.RootElement.GetProperty("tags")[0].EnumerateObject().Select(p => p.Name).ToList());
    }

    [Fact]
    public void Changes_a_tags_color_with_a_new_time_and_keeps_the_other_entries()
    {
        f.SeedTagColors(1, ("home", "red", Earlier), ("work", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Colors.SetColor("work", "gold");

        Assert.Equal([("home", "red", Earlier), ("work", "gold", NowText)], Read().Entries);
    }

    [Fact]
    public void Raises_one_color_notice_and_one_local_save_for_a_new_color()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (colors, saved) = CountNotices();

        Colors.SetColor("work", "teal");

        Assert.Equal(1, colors());
        Assert.Equal(1, saved());
    }

    [Fact]
    public void Writes_nothing_and_raises_no_notice_when_the_same_color_is_set_again()
    {
        f.SeedTagColors(1, ("work", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = f.Files.ReadFile(f.TagColorsFile);
        var (colors, saved) = CountNotices();
        f.Files.FailWritesUnder(f.Clone);

        Colors.SetColor("work", "teal");

        Assert.Equal(before, f.Files.ReadFile(f.TagColorsFile));
        Assert.Equal(0, colors());
        Assert.Equal(0, saved());
    }

    [Fact]
    public void Keeps_the_colors_after_a_restart()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        Colors.SetColor("work", "teal");

        var restarted = Assert.IsAssignableFrom<ITaskStore>(f.Restarted());
        restarted.UseRepositoryFolder(f.Clone);

        Assert.Equal("teal", restarted.TagColors.ColorOf("work"));
    }

    // Sorting, spelling, and never deleting

    [Fact]
    public void Sorts_the_entries_by_tag_ignoring_case()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Colors.SetColor("banana", "red");
        Colors.SetColor("Cherry", "blue");
        Colors.SetColor("apple", "green");
        Colors.SetColor("Banana2", "gold");

        Assert.Equal(["apple", "banana", "Banana2", "Cherry"], Read().Entries.Select(e => e.Tag).ToList());
    }

    [Fact]
    public void Saves_a_new_pick_of_a_tag_with_the_spelling_given_in_that_call()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        Colors.SetColor("Work", "teal");

        f.Time.Advance(TimeSpan.FromMinutes(1));
        Colors.SetColor("work", "gold");

        Assert.Equal([("work", "gold", "2026-11-12T08:31:15.123Z")], Read().Entries);
    }

    [Fact]
    public void Never_deletes_an_entry_for_a_tag_no_task_uses()
    {
        f.Seed(TasksFixture.Id(1), tags: ["work"]);
        f.SeedTagColors(1, ("old", "red", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Colors.SetColor("work", "teal");

        Assert.Equal(["old", "work"], Read().Entries.Select(e => e.Tag).ToList());
        Assert.Equal("red", Colors.ColorOf("old"));
    }

    [Fact]
    public void Does_not_create_the_file_when_settings_are_initialized_or_a_repository_is_selected()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Store.Synchronization.EnsureSettings();

        Assert.False(f.Files.FileExists(f.TagColorsFile));
    }

    // Reading

    [Fact]
    public void Matches_a_tag_ignoring_case()
    {
        f.SeedTagColors(1, ("Work", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal("teal", Colors.ColorOf("work"));
        Assert.Equal("teal", Colors.ColorOf("WORK"));
    }

    [Fact]
    public void Answers_null_for_a_tag_with_no_entry()
    {
        f.SeedTagColors(1, ("work", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Null(Colors.ColorOf("home"));
    }

    [Fact]
    public void Answers_null_when_there_is_no_file_or_no_repository()
    {
        Assert.Null(Colors.ColorOf("work"));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Null(Colors.ColorOf("work"));
    }

    // Refusals

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded ")]
    [InlineData("two\nlines")]
    public void Refuses_an_invalid_tag_and_writes_nothing(string tag)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => Colors.SetColor(tag, "teal"));

        Assert.False(f.Files.FileExists(f.TagColorsFile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("teal\ngold")]
    [InlineData("teal\r\ngold")]
    public void Refuses_a_blank_or_multi_line_color_name_and_writes_nothing(string color)
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.ThrowsAny<ArgumentException>(() => Colors.SetColor("work", color));

        Assert.False(f.Files.FileExists(f.TagColorsFile));
    }

    [Fact]
    public void Refuses_a_color_when_no_repository_is_selected()
    {
        Assert.Throws<NoRepositoryFolderException>(() => Colors.SetColor("work", "teal"));
    }

    [Theory]
    [MemberData(nameof(UnreadableFiles))]
    public void Fails_to_set_a_color_on_an_unreadable_file_and_leaves_it_untouched(string content)
    {
        f.WriteText(f.TagColorsFile, content);
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (colors, saved) = CountNotices();

        Assert.Throws<InvalidDataException>(() => Colors.SetColor("work", "teal"));

        Assert.Equal(content, f.ReadText(f.TagColorsFile));
        Assert.Equal(0, colors());
        Assert.Equal(0, saved());
    }

    [Fact]
    public void Fails_to_set_a_color_on_a_newer_version_and_leaves_it_untouched()
    {
        var newer = TasksFixture.        TagColorsJson(3, ("work", "teal", Earlier));
        f.WriteText(f.TagColorsFile, newer);
        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Throws<InvalidDataException>(() => Colors.SetColor("home", "red"));

        Assert.Equal(newer, f.ReadText(f.TagColorsFile));
    }

    // Unreadable or newer files never stop the tasks

    public static TheoryData<string> UnreadableFiles() => new()
    {
        "{ not json",
        TasksFixture.TagColorsJson(1, (" padded ", "teal", Earlier)),
        TasksFixture.TagColorsJson(1, ("", "teal", Earlier)),
        TasksFixture.TagColorsJson(1, ("work", "", Earlier)),
        TasksFixture.TagColorsJson(1, ("work", "teal\ngold", Earlier)),
        TasksFixture.TagColorsJson(1, ("work", "teal", "yesterday")),
        TasksFixture.TagColorsJson(1, ("Work", "teal", Earlier), ("work", "gold", Earlier)),
    };

    [Theory]
    [MemberData(nameof(UnreadableFiles))]
    public void Skips_an_unreadable_file_by_name_answers_null_and_still_loads_the_tasks(string content)
    {
        f.Seed(TasksFixture.Id(1), "Readable");
        f.WriteText(f.TagColorsFile, content);

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Readable"], f.Titles);
        Assert.Null(Colors.ColorOf("work"));
        var skipped = Assert.Single(f.Repository.SkippedFiles);
        Assert.Equal("tags.json", skipped.RelativePath);
        Assert.False(skipped.RequiresUpdate);
        Assert.False(string.IsNullOrWhiteSpace(skipped.Reason));
        Assert.Equal(content, f.ReadText(f.TagColorsFile));
    }

    [Fact]
    public void Skips_a_newer_file_asks_for_an_update_answers_null_and_still_loads_the_tasks()
    {
        f.Seed(TasksFixture.Id(1), "Readable");
        f.SeedTagColors(3, ("work", "teal", Earlier));

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(["Readable"], f.Titles);
        Assert.Null(Colors.ColorOf("work"));
        var skipped = Assert.Single(f.Repository.SkippedFiles);
        Assert.Equal("tags.json", skipped.RelativePath);
        Assert.True(skipped.RequiresUpdate);
    }

    // Reloads

    [Fact]
    public void Raises_a_color_notice_but_no_local_save_when_a_repository_is_selected()
    {
        f.SeedTagColors(1, ("work", "teal", Earlier));
        var (colors, saved) = CountNotices();

        f.Tasks.UseRepositoryFolder(f.Clone);

        Assert.Equal(1, colors());
        Assert.Equal(0, saved());
        Assert.Equal("teal", Colors.ColorOf("work"));
    }

    [Fact]
    public void Reloads_the_colors_and_raises_a_color_notice_when_an_update_changes_the_file()
    {
        f.SeedTagColors(1, ("work", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);
        var (colors, saved) = CountNotices();

        f.Repository.Update(f.Clone, () => f.SeedTagColors(1, ("work", "gold", NowText)));

        Assert.Equal("gold", Colors.ColorOf("work"));
        Assert.Equal(1, colors());
        Assert.Equal(0, saved());
    }

    [Fact]
    public void Shows_the_colors_of_the_newly_selected_repository()
    {
        f.SeedTagColors(1, ("work", "teal", Earlier));
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.UseRepositoryFolder(f.OtherClone);

        Assert.Null(Colors.ColorOf("work"));
    }
}
