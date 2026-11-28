using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// Every change to a task gives it an Updated strictly later than before, since Updated is the token a stale-update check compares.
/// With a clock that hasn't moved since the last change, that is the previous Updated plus one millisecond.
/// </summary>
[Unit]
public sealed class UpdatedAdvancesTests
{
    private static readonly TimeSpan Millisecond = TimeSpan.FromMilliseconds(1);

    private readonly TasksFixture f = new();
    private readonly string first = TasksFixture.Id(1);
    private readonly string second = TasksFixture.Id(2);
    private readonly string third = TasksFixture.Id(3);

    public UpdatedAdvancesTests()
    {
        f.Seed(first, "First", rank: "a0");
        f.Seed(second, "Second", rank: "a1");
        f.Seed(third, "Third", rank: "a2");
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Time.Advance(TimeSpan.FromHours(2));
    }

    private IDirectTaskChanges Direct => f.Store.Direct;

    private static DateTimeOffset Seeded => DirectChangeChecks.SeededTime;

    private DateTimeOffset Now => f.Time.GetUtcNow();

    private TaskItem Current(string id) => f.Tasks.All.Single(task => task.Id == id);

    [Fact]
    public void Two_updates_with_the_clock_not_moving_give_each_an_updated_one_millisecond_after_the_last()
    {
        var one = Direct.Update(first, Seeded, "One", null, null);
        var two = Direct.Update(first, one.Updated, "Two", null, null);
        var three = Direct.Update(first, two.Updated, "Three", null, null);

        Assert.Equal(Now, one.Updated);
        Assert.Equal(one.Updated + Millisecond, two.Updated);
        Assert.Equal(two.Updated + Millisecond, three.Updated);
    }

    [Fact]
    public void The_advanced_updated_is_what_the_task_keeps_in_memory_and_in_its_file()
    {
        var one = Direct.Update(first, Seeded, "One", null, null);
        var two = Direct.Update(first, one.Updated, "Two", null, null);

        Assert.Equal(two.Updated, Current(first).Updated);
        using var json = f.ReadJson(f.TaskFile(first));
        Assert.Equal(two.Updated, json.RootElement.GetProperty("updated").GetDateTimeOffset());
    }

    [Fact]
    public void An_update_after_the_clock_moved_on_gets_the_time_now()
    {
        var one = Direct.Update(first, Seeded, "One", null, null);
        f.Time.Advance(TimeSpan.FromMinutes(5));

        var two = Direct.Update(first, one.Updated, "Two", null, null);

        Assert.Equal(Now, two.Updated);
    }

    [Fact]
    public void An_update_holding_the_token_of_an_earlier_update_is_stale_even_though_the_clock_did_not_move()
    {
        var one = Direct.Update(first, Seeded, "One", null, null);
        Direct.Update(first, one.Updated, "Two", null, null);

        var stale = Assert.Throws<StaleTaskException>(() => Direct.Update(first, one.Updated, "Lost", null, null));

        Assert.Equal("Two", stale.Current.Title);
        Assert.Equal("Two", Current(first).Title);
    }

    [Fact]
    public void An_update_holding_the_token_from_before_a_resolve_is_stale_even_though_the_clock_did_not_move()
    {
        var one = Direct.Update(first, Seeded, "One", null, null);
        var resolved = f.Tasks.Resolve(first);

        Assert.Equal(one.Updated + Millisecond, resolved.Updated);
        Assert.Throws<StaleTaskException>(() => Direct.Update(first, one.Updated, "Lost", null, null));
    }

    [Fact]
    public void Two_moves_with_the_clock_not_moving_give_each_an_updated_one_millisecond_after_the_last()
    {
        var down = f.Tasks.Move(first, third, null);
        var up = f.Tasks.Move(first, null, second);

        Assert.Equal(Now, down.Updated);
        Assert.Equal(down.Updated + Millisecond, up.Updated);
    }

    [Fact]
    public void A_resolve_and_a_reopen_with_the_clock_not_moving_give_each_an_updated_one_millisecond_after_the_last()
    {
        var resolved = f.Tasks.Resolve(first);
        var reopened = f.Tasks.Reopen(first);
        var again = f.Tasks.Resolve(first);

        Assert.Equal(Now, resolved.Updated);
        Assert.Equal(resolved.Updated + Millisecond, reopened.Updated);
        Assert.Equal(reopened.Updated + Millisecond, again.Updated);
    }

    [Fact]
    public void Two_added_images_with_the_clock_not_moving_give_each_an_updated_one_millisecond_after_the_last()
    {
        var one = Direct.AddImage(first, TasksFixture.TinyPng(200), "one");
        var two = Direct.AddImage(first, TasksFixture.TinyPng(300), "two");

        Assert.Equal(Now, one.Updated);
        Assert.Equal(one.Updated + Millisecond, two.Updated);
    }

    [Fact]
    public void Two_saved_edits_with_the_clock_not_moving_give_each_an_updated_one_millisecond_after_the_last()
    {
        var started = Current(first);
        var one = f.Tasks.SaveEdit(started, new TaskDraft(first, "One", started.Description, started.Tags));
        var two = f.Tasks.SaveEdit(one, new TaskDraft(first, "Two", one.Description, one.Tags));

        Assert.Equal(Now, one.Updated);
        Assert.Equal(one.Updated + Millisecond, two.Updated);
    }

    [Fact]
    public void Changes_of_every_kind_in_a_row_with_the_clock_not_moving_each_move_updated_forward()
    {
        var times = new List<DateTimeOffset>
        {
            Direct.Update(first, Seeded, "One", null, null).Updated,
            f.Tasks.Move(first, third, null).Updated,
            f.Tasks.Resolve(first).Updated,
            f.Tasks.Reopen(first).Updated,
            Direct.AddImage(first, TasksFixture.TinyPng(), "pic").Updated,
        };
        var started = Current(first);
        times.Add(f.Tasks.SaveEdit(started, new TaskDraft(first, "Two", started.Description, started.Tags)).Updated);

        Assert.Equal(Enumerable.Range(0, times.Count).Select(n => Now + n * Millisecond), times);
    }
}
