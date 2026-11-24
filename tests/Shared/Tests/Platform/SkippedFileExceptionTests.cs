using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The exception a store throws for a call that needs a file it skipped.</summary>
[Unit]
public sealed class SkippedFileExceptionTests
{
    private static readonly ISkippedRepositoryFile Report = SkippedFile.Of("tasks/a.json", new InvalidDataException("Damaged."));

    [Fact]
    public void Rejects_a_missing_report()
    {
        Assert.Throws<ArgumentNullException>(() => new SkippedFileException(null!));
    }

    [Fact]
    public void Gives_back_the_report_it_was_made_with()
    {
        Assert.Same(Report, new SkippedFileException(Report).File);
    }

    [Fact]
    public void Names_the_relative_path_and_the_reason_in_its_message()
    {
        var message = new SkippedFileException(Report).Message;

        Assert.Contains("tasks/a.json", message);
        Assert.Contains("Damaged.", message);
    }

    [Fact]
    public void Is_an_InvalidOperationException()
    {
        Assert.IsAssignableFrom<InvalidOperationException>(new SkippedFileException(Report));
    }
}
