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
    public void Writes_only_line_feeds_as_line_breaks()
    {
        var task = f.AddTask("T", "one\r\ntwo");

        Assert.DoesNotContain('\r', f.ReadText(f.TaskFile(task.Id)));
    }

    [Fact]
    public void Writes_a_description_line_break_as_an_escaped_line_feed()
    {
        var task = f.AddTask("T", "one\ntwo");

        Assert.Contains("\"description\": \"one\\ntwo\"", f.ReadText(f.TaskFile(task.Id)));
    }

    [Fact]
    public void Writes_text_that_needs_escaping_so_it_reads_back_as_typed()
    {
        var task = f.AddTask("Say \"hi\" \\ there", "tab\there", "quote\"tag");

        var again = f.Restarted();
        again.UseRepositoryFolder(f.Clone);

        var read = Assert.Single(again.All);
        Assert.Equal("Say \"hi\" \\ there", read.Title);
        Assert.Equal("tab\there", read.Description);
        Assert.Equal(["quote\"tag"], read.Tags);
    }

    [Fact]
    public void Writes_times_in_utc_to_the_millisecond()
    {
        var time = new DateTimeOffset(2026, 11, 12, 8, 30, 15, TimeSpan.Zero).AddTicks(1_234_567);
        f.Time.Advance(time - TasksFixture.Start);

        var task = f.AddTask();

        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal("2026-11-12T08:30:15.123Z", json.RootElement.GetProperty("created").GetString());
        Assert.Equal("2026-11-12T08:30:15.123Z", json.RootElement.GetProperty("updated").GetString());
    }

    [Fact]
    public void Writes_a_time_with_no_milliseconds_with_three_zero_digits()
    {
        f.Time.Advance(TimeSpan.FromMilliseconds(877));

        var task = f.AddTask();

        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal("2026-11-12T08:30:16.000Z", json.RootElement.GetProperty("created").GetString());
    }

    [Fact]
    public void Writes_the_task_id_inside_the_file_as_the_file_name()
    {
        var task = f.AddTask();

        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal(task.Id, json.RootElement.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("version").ValueKind);
        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void Writes_a_resolved_time_as_null_for_a_new_task()
    {
        var task = f.AddTask();

        using var json = f.ReadJson(f.TaskFile(task.Id));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("resolved").ValueKind);
    }

    [Fact]
    public void Writes_valid_json_that_other_tools_can_parse()
    {
        var task = f.AddTask("T", "é ✓ 日本");

        var text = Encoding.UTF8.GetString(f.Files.ReadFile(f.TaskFile(task.Id)));

        using var json = JsonDocument.Parse(text);
        Assert.Equal("é ✓ 日本", json.RootElement.GetProperty("description").GetString());
    }
}

/// <summary>Where a new task goes in the order: rank keys of the characters 0-9, A-Z, and a-z, never renumbered.</summary>
[Unit]
public sealed class TaskRankTests
{
    private readonly TasksFixture f = new();

    [Fact]
    public void Gives_the_first_task_the_key_a0()
    {
        var task = f.AddTask();

        Assert.Equal("a0", task.Rank);
    }

    [Fact]
    public void Puts_a_new_task_before_every_other_task()
    {
        f.Seed(TasksFixture.Id(1), rank: "a0");
        f.Seed(TasksFixture.Id(2), rank: "a1");
        f.Seed(TasksFixture.Id(3), rank: "Zz");
        f.Tasks.UseRepositoryFolder(f.Clone);

        var task = f.Tasks.Add(f.Draft("Newest"));

        Assert.Equal("Newest", f.Tasks.All[0].Title);
        Assert.All(f.Tasks.All.Skip(1), other => Assert.True(string.CompareOrdinal(task.Rank, other.Rank) < 0));
    }

    [Fact]
    public void Puts_each_new_task_before_the_one_added_before_it()
    {
        f.AddTask("one");
        f.Tasks.Add(f.Draft("two"));
        f.Tasks.Add(f.Draft("three"));

        Assert.Equal(["three", "two", "one"], f.Titles);
    }

    [Fact]
    public void Gives_each_new_top_task_a_smaller_key_than_the_last()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var ranks = Enumerable.Range(0, 10).Select(_ => f.Tasks.Add(f.Draft()).Rank).ToList();

        for (var i = 1; i < ranks.Count; i++)
            Assert.True(string.CompareOrdinal(ranks[i], ranks[i - 1]) < 0, $"{ranks[i]} should sort before {ranks[i - 1]}");
    }

    [Fact]
    public void Uses_only_digits_and_letters_in_keys()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var ranks = Enumerable.Range(0, 70).Select(_ => f.Tasks.Add(f.Draft()).Rank).ToList();

        Assert.All(ranks, rank => Assert.Matches("^[0-9A-Za-z]+$", rank));
    }

    [Fact]
    public void Gives_the_first_63_tasks_keys_of_two_characters()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var ranks = Enumerable.Range(0, 63).Select(_ => f.Tasks.Add(f.Draft()).Rank).ToList();

        Assert.All(ranks, rank => Assert.Equal(2, rank.Length));
        Assert.Equal(63, ranks.Distinct().Count());
    }

    [Fact]
    public void Gives_the_64th_task_a_key_that_still_sorts_first()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var ranks = Enumerable.Range(0, 63).Select(_ => f.Tasks.Add(f.Draft()).Rank).ToList();

        var next = f.Tasks.Add(f.Draft("64th"));

        Assert.Equal(3, next.Rank.Length);
        Assert.All(ranks, rank => Assert.True(string.CompareOrdinal(next.Rank, rank) < 0));
        Assert.Equal("64th", f.Tasks.All[0].Title);
    }

    [Fact]
    public void Never_changes_the_key_of_a_task_already_added()
    {
        var first = f.AddTask("first");
        f.Tasks.Add(f.Draft("second"));
        f.Tasks.Add(f.Draft("third"));

        Assert.Equal(first.Rank, f.Tasks.All.Single(t => t.Id == first.Id).Rank);
        using var json = f.ReadJson(f.TaskFile(first.Id));
        Assert.Equal(first.Rank, json.RootElement.GetProperty("rank").GetString());
    }

    [Fact]
    public void Leaves_the_other_task_files_unchanged_when_it_adds_one()
    {
        f.Seed(TasksFixture.Id(1), rank: "a0");
        var before = f.Files.ReadFile(f.TaskFile(TasksFixture.Id(1)));
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Tasks.Add(f.Draft());

        Assert.Equal(before, f.Files.ReadFile(f.TaskFile(TasksFixture.Id(1))));
    }
}
