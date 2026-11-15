using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

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
    public void Gives_each_new_top_task_a_smaller_key_than_the_last_and_lists_it_first()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var ranks = Enumerable.Range(0, 10).Select(i => f.Tasks.Add(f.Draft($"task {i}")).Rank).ToList();

        for (var i = 1; i < ranks.Count; i++)
            Assert.True(string.CompareOrdinal(ranks[i], ranks[i - 1]) < 0, $"{ranks[i]} should sort before {ranks[i - 1]}");
        Assert.Equal("task 9", f.Tasks.All[0].Title);
    }

    [Fact]
    public void Keeps_every_key_of_64_adds_distinct_in_digits_and_letters_and_grows_to_three_characters_at_the_64th()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);

        var ranks = Enumerable.Range(0, 64).Select(_ => f.Tasks.Add(f.Draft()).Rank).ToList();

        Assert.Equal(64, ranks.Distinct().Count());
        Assert.All(ranks, rank => Assert.Matches("^[0-9A-Za-z]+$", rank));
        Assert.All(ranks.Take(63), rank => Assert.Equal(2, rank.Length));
        Assert.Equal(3, ranks[63].Length);
        Assert.All(ranks.Take(63), rank => Assert.True(string.CompareOrdinal(ranks[63], rank) < 0));
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
