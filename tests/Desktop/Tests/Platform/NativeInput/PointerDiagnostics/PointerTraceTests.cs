using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class PointerTraceTests
{
    private static readonly string Away = PointerTrace.Label(2, "away");
    private static readonly string Target = PointerTrace.Label(2, "target");

    private static string Entry(int number) => $"entry<{number:D4}>";

    [Fact]
    public void Shows_each_entry_under_the_call_and_step_that_recorded_it()
    {
        var trace = new PointerTrace();
        var firstTarget = PointerTrace.Label(1, "target");
        trace.Record(TimeSpan.Zero, firstTarget, "first-call-entry");
        trace.Record(TimeSpan.FromMilliseconds(5), Away, "second-call-away-entry");
        trace.Record(TimeSpan.FromMilliseconds(9), Target, "second-call-target-entry");

        var lines = trace.Describe().Split('\n');

        Assert.Contains(lines, line => line.Contains(firstTarget) && line.Contains("first-call-entry"));
        Assert.Contains(lines, line => line.Contains(Away) && line.Contains("second-call-away-entry"));
        Assert.Contains(lines, line => line.Contains(Target) && line.Contains("second-call-target-entry"));
    }

    [Fact]
    public void Drops_only_the_oldest_entry_when_a_201st_arrives()
    {
        var trace = new PointerTrace();

        for (var i = 1; i <= 201; i++)
        {
            trace.Record(TimeSpan.FromMilliseconds(i), Away, Entry(i));
        }

        var text = trace.Describe();
        Assert.Equal(1, trace.Dropped);
        Assert.DoesNotContain(Entry(1), text);
        Assert.Contains(Entry(2), text);
        Assert.Contains(Entry(201), text);
    }

    [Fact]
    public void Keeps_the_newest_200_entries_and_counts_every_dropped_one()
    {
        var trace = new PointerTrace();

        for (var i = 1; i <= 250; i++)
        {
            trace.Record(TimeSpan.FromMilliseconds(i), Away, Entry(i));
        }

        var text = trace.Describe();
        Assert.Equal(50, trace.Dropped);
        Assert.Contains(Entry(250), text);
        Assert.Contains(Entry(51), text);
        Assert.DoesNotContain(Entry(50), text);
        Assert.DoesNotContain(Entry(1), text);
    }

    [Fact]
    public void Drops_nothing_with_exactly_200_entries()
    {
        var trace = new PointerTrace();

        for (var i = 1; i <= 200; i++)
        {
            trace.Record(TimeSpan.Zero, Away, Entry(i));
        }

        Assert.Equal(0, trace.Dropped);
        Assert.Contains(Entry(1), trace.Describe());
        Assert.Contains(Entry(200), trace.Describe());
    }

    [Fact]
    public void Keeps_the_latest_moves_readings_and_outcome_under_its_call_and_step_however_many_entries_follow()
    {
        var trace = new PointerTrace();
        trace.Keep(Away, PointerTrace.AtSend, "send-reading-marker");
        trace.Keep(Away, PointerTrace.AtFailure, "failure-reading-marker");
        trace.Keep(Away, PointerTrace.Refused, "outcome-marker");

        for (var i = 1; i <= 600; i++)
        {
            trace.Record(TimeSpan.FromMilliseconds(i), Target, Entry(i));
        }

        var lines = trace.Describe().Split('\n');
        Assert.Contains(lines, line => line.Contains(Away) && line.Contains("send-reading-marker"));
        Assert.Contains(lines, line => line.Contains(Away) && line.Contains("failure-reading-marker"));
        Assert.Contains(lines, line => line.Contains(Away) && line.Contains("outcome-marker"));
    }

    [Fact]
    public void Replaces_the_kept_summary_when_a_later_move_keeps_its_own()
    {
        var trace = new PointerTrace();
        trace.Keep(PointerTrace.Label(1, "target"), PointerTrace.AtSend, "earlier-move-reading");
        trace.Keep(Away, PointerTrace.AtSend, "latest-move-reading");

        var text = trace.Describe();

        Assert.Contains("latest-move-reading", text);
        Assert.DoesNotContain("earlier-move-reading", text);
    }

    [Fact]
    public void Keeps_a_later_fact_of_the_same_move_beside_its_earlier_facts()
    {
        var trace = new PointerTrace();
        trace.Keep(Away, PointerTrace.AtSend, "send-reading-marker");
        trace.Keep(Away, PointerTrace.AtFailure, "failure-reading-marker");

        var text = trace.Describe();

        Assert.Contains("send-reading-marker", text);
        Assert.Contains("failure-reading-marker", text);
    }

    [Fact]
    public void Starts_a_test_with_an_empty_trace_that_the_same_test_keeps_getting()
    {
        var holder = new PointerTraceHolder();
        holder.For("test-one").Keep(Away, PointerTrace.Refused, "belongs-to-one");

        Assert.Same(holder.For("test-one"), holder.For("test-one"));
        Assert.Contains("belongs-to-one", holder.For("test-one").Describe());
    }

    [Fact]
    public void Gives_a_different_test_a_fresh_trace_without_the_earlier_tests_moves()
    {
        var holder = new PointerTraceHolder();
        var earlier = holder.For("test-one");
        earlier.Keep(Away, PointerTrace.Refused, "belongs-to-one");
        earlier.Record(TimeSpan.Zero, Away, "entry-of-one");

        var later = holder.For("test-two").Describe();

        Assert.DoesNotContain("belongs-to-one", later);
        Assert.DoesNotContain("entry-of-one", later);
    }

    [Fact]
    public void Does_not_bring_back_the_state_of_an_earlier_test_when_its_id_asks_again()
    {
        var holder = new PointerTraceHolder();
        holder.For("test-one").Record(TimeSpan.Zero, Away, "entry-of-one");
        holder.For("test-two");

        Assert.DoesNotContain("entry-of-one", holder.For("test-one").Describe());
    }
}