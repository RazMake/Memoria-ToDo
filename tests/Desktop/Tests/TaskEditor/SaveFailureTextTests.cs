using System.IO;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class SaveFailureTextTests
{
    [Fact]
    public void Says_the_change_may_be_only_partly_saved_and_names_the_first_failure()
    {
        var text = SaveFailureText.Incomplete(new AggregateException(new IOException("a"), new IOException("b")));

        Assert.Equal("The change may be only partly saved: a", text);
    }

    [Fact]
    public void Names_the_exceptions_own_message_when_it_holds_no_failure()
    {
        var text = SaveFailureText.Incomplete(new AggregateException("Several things failed"));

        Assert.Equal("The change may be only partly saved: Several things failed", text);
    }

    [Fact]
    public void Rejects_a_missing_failure()
    {
        Assert.Throws<ArgumentNullException>(() => SaveFailureText.Incomplete(null!));
    }
}
