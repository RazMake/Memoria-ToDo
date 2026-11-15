using System.Text;
using System.Text.Json;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>What a task file looks like on disk, so a change shows as a few changed lines in git.</summary>
[Unit]
public sealed class TaskFileFormatTests
{
    private readonly TasksFixture f = new();

    [Fact]
    public void Writes_the_nine_fields_with_two_space_indents_in_the_designed_order()
    {
        var task = f.AddTask("Buy milk", "Two litres", "home", "shop");

        var expected = $$"""
            {
              "id": "{{task.Id}}",
              "title": "Buy milk",
              "description": "Two litres",
              "rank": "{{task.Rank}}",
              "tags": [
                "home",
                "shop"
              ],
              "created": "2026-11-12T08:30:15.123Z",
              "updated": "2026-11-12T08:30:15.123Z",
              "resolved": null,
              "version": 1
            }
            """.ReplaceLineEndings("\n");
        Assert.Equal(expected, f.ReadText(f.TaskFile(task.Id)).TrimEnd('\n'));
    }

    [Fact]
    public void Writes_an_empty_tag_list_as_an_empty_array()
    {
        var task = f.AddTask();

        Assert.Contains("\"tags\": []", f.ReadText(f.TaskFile(task.Id)));
    }

    [Fact]
    public void Writes_the_file_as_utf8_without_a_byte_order_mark()
    {
        var task = f.AddTask();

        var bytes = f.Files.ReadFile(f.TaskFile(task.Id));

        Assert.Equal((byte)'{', bytes[0]);
    }

    [Fact]
    public void Writes_only_line_feeds_as_line_breaks_escaping_them_inside_text()
    {
        var task = f.AddTask("T", "one\r\ntwo\nthree");

        var text = f.ReadText(f.TaskFile(task.Id));

        Assert.DoesNotContain('\r', text);
        Assert.Contains("\"description\": \"one\\ntwo\\nthree\"", text);
    }

    [Fact]
    public void Writes_text_that_needs_escaping_so_it_reads_back_as_typed()
    {
        f.AddTask("Say \"hi\" \\ there", "tab\there é ✓ 日本", "quote\"tag");

        var again = f.Restarted();
        again.UseRepositoryFolder(f.Clone);

        var read = Assert.Single(again.All);
        Assert.Equal("Say \"hi\" \\ there", read.Title);
        Assert.Equal("tab\there é ✓ 日本", read.Description);
        Assert.Equal(["quote\"tag"], read.Tags);
    }

    [Theory]
    [InlineData(4_567, "2026-11-12T08:30:15.123Z")]
    [InlineData(8_770_000, "2026-11-12T08:30:16.000Z")]
    public void Writes_times_in_utc_to_the_millisecond_with_three_digits(long ticksLater, string expected)
    {
        f.Time.Advance(TimeSpan.FromTicks(ticksLater));

        var task = f.AddTask();

        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal(expected, json.RootElement.GetProperty("created").GetString());
        Assert.Equal(expected, json.RootElement.GetProperty("updated").GetString());
    }
}
