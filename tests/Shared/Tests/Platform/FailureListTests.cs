using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>How steps that must all run keep their failures and throw them together.</summary>
[Unit]
public sealed class FailureListTests
{
    [Fact]
    public void Throws_nothing_when_no_step_failed()
    {
        var failures = new FailureList();
        failures.Attempt(() => { });
        failures.Attempt(() => { });

        failures.ThrowIfAny();
    }

    [Fact]
    public void Keeps_a_failing_step_instead_of_throwing_it_and_still_runs_later_steps()
    {
        var failures = new FailureList();
        var laterStepRan = false;

        failures.Attempt(() => throw new InvalidOperationException("First."));
        failures.Attempt(() => laterStepRan = true);

        Assert.True(laterStepRan);
    }

    [Fact]
    public void Throws_a_single_failure_as_the_same_instance_with_its_original_stack_trace()
    {
        var failures = new FailureList();
        var original = new InvalidOperationException("Only.");
        failures.Attempt(() => ThrowOriginal(original));

        var thrown = Assert.Throws<InvalidOperationException>(failures.ThrowIfAny);

        Assert.Same(original, thrown);
        Assert.Contains(nameof(ThrowOriginal), thrown.StackTrace);
    }

    [Fact]
    public void Throws_several_failures_as_an_AggregateException_in_the_order_they_were_kept()
    {
        var failures = new FailureList();
        var first = new InvalidOperationException("First.");
        var second = new IOException("Second.");
        var third = new ArgumentException("Third.");

        failures.Attempt(() => throw first);
        failures.Add(second);
        failures.Attempt(() => throw third);

        var thrown = Assert.Throws<AggregateException>(failures.ThrowIfAny);

        Assert.Equal(new Exception[] { first, second, third }, thrown.InnerExceptions);
    }

    [Fact]
    public void Rejects_a_null_failure()
    {
        Assert.Throws<ArgumentNullException>(() => new FailureList().Add(null!));
    }

    [Fact]
    public void Throws_nothing_after_a_save_when_no_step_failed()
    {
        var failures = new FailureList();
        failures.Attempt(() => { });

        failures.ThrowIfAnyAfterSave(new object());
    }

    [Fact]
    public void Throws_a_single_failure_after_a_save_as_NoticeFailedException_with_what_was_saved()
    {
        var failures = new FailureList();
        var only = new InvalidOperationException("Only.");
        var saved = new object();
        failures.Attempt(() => throw only);

        var thrown = Assert.Throws<NoticeFailedException<object>>(() => failures.ThrowIfAnyAfterSave(saved));

        Assert.Equal([only], thrown.InnerExceptions);
        Assert.Same(saved, thrown.Saved);
    }

    [Fact]
    public void Throws_several_failures_after_a_save_as_NoticeFailedException_in_the_order_they_were_kept()
    {
        var failures = new FailureList();
        var first = new InvalidOperationException("First.");
        var second = new IOException("Second.");
        var third = new ArgumentException("Third.");
        failures.Attempt(() => throw first);
        failures.Add(second);
        failures.Attempt(() => throw third);

        var thrown = Assert.Throws<NoticeFailedException<string>>(() => failures.ThrowIfAnyAfterSave("saved"));

        Assert.Equal([first, second, third], thrown.InnerExceptions);
        Assert.Equal("saved", thrown.Saved);
    }

    [Fact]
    public void Throws_after_a_save_with_nothing_saved_when_the_change_saves_no_item()
    {
        var failures = new FailureList();
        failures.Add(new IOException("Notice."));

        var thrown = Assert.Throws<NoticeFailedException<string>>(() => failures.ThrowIfAnyAfterSave<string>(null));

        Assert.Null(thrown.Saved);
    }

    private static void ThrowOriginal(Exception failure) => throw failure;
}
