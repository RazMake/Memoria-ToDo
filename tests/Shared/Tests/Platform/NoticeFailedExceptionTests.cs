using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The exception a store throws when a change was saved and then a notice, or the local save report, failed.</summary>
[Unit]
public sealed class NoticeFailedExceptionTests
{
    private static readonly IOException First = new("Disk.");
    private static readonly InvalidOperationException Second = new("Handler.");

    [Fact]
    public void Rejects_null_failures()
    {
        Assert.Throws<ArgumentNullException>(() => new NoticeFailedException(null!));
    }

    [Fact]
    public void Rejects_an_empty_list_of_failures()
    {
        Assert.Throws<ArgumentException>(() => new NoticeFailedException([]));
    }

    [Fact]
    public void Rejects_a_list_that_holds_null()
    {
        Assert.Throws<ArgumentException>(() => new NoticeFailedException([First, null!]));
    }

    [Fact]
    public void Rejects_null_failures_when_it_also_holds_what_was_saved()
    {
        Assert.Throws<ArgumentNullException>(() => new NoticeFailedException<string>("saved", null!));
    }

    [Fact]
    public void Rejects_an_empty_list_of_failures_when_it_also_holds_what_was_saved()
    {
        Assert.Throws<ArgumentException>(() => new NoticeFailedException<string>("saved", []));
    }

    [Fact]
    public void Rejects_a_list_that_holds_null_when_it_also_holds_what_was_saved()
    {
        Assert.Throws<ArgumentException>(() => new NoticeFailedException<string>("saved", [null!]));
    }

    [Fact]
    public void Holds_every_failure_in_order_and_takes_the_first_as_its_inner_exception()
    {
        var thrown = new NoticeFailedException([First, Second]);

        Assert.Equal(new Exception[] { First, Second }, thrown.InnerExceptions);
        Assert.Same(First, thrown.InnerException);
    }

    [Fact]
    public void Says_a_notice_failed_after_the_change_was_saved_then_gives_the_first_failure_s_message()
    {
        var thrown = new NoticeFailedException([First, Second]);

        Assert.Equal("A notice failed after the change was saved: Disk.", thrown.Message);
    }

    [Fact]
    public void Is_an_Exception_and_not_an_AggregateException()
    {
        var thrown = new NoticeFailedException([First]);

        Assert.IsNotAssignableFrom<AggregateException>(thrown);
        Assert.IsAssignableFrom<Exception>(thrown);
    }

    [Fact]
    public void Gives_back_what_was_saved_and_behaves_as_the_base_type()
    {
        var saved = new object();

        var thrown = new NoticeFailedException<object>(saved, [First, Second]);

        Assert.Same(saved, thrown.Saved);
        var asBase = Assert.IsAssignableFrom<NoticeFailedException>(thrown);
        Assert.Equal(new Exception[] { First, Second }, asBase.InnerExceptions);
        Assert.Same(First, asBase.InnerException);
        Assert.Equal("A notice failed after the change was saved: Disk.", asBase.Message);
    }

    [Fact]
    public void Its_text_holds_every_failure_with_its_stack_trace_in_order()
    {
        var changed = Thrown(new IOException("Changed failed."));
        var report = Thrown(new InvalidOperationException("The report failed."));

        var text = new NoticeFailedException([changed, report]).ToString();

        var firstAt = text.IndexOf(changed.ToString(), StringComparison.Ordinal);
        var secondAt = text.IndexOf(report.ToString(), StringComparison.Ordinal);
        Assert.True(firstAt >= 0, "The first failure's text is missing.");
        Assert.True(secondAt > firstAt, "The second failure's text is missing or comes before the first.");
    }

    private static Exception Thrown(Exception failure)
    {
        try
        {
            throw failure;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    [Fact]
    public void Gives_back_null_when_the_change_saved_no_item()
    {
        Assert.Null(new NoticeFailedException<object>(null, [First]).Saved);
    }
}
