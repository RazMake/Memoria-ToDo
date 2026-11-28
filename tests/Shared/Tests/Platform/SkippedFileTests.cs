using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Which failures make a store skip a file, and how it reports the skipped file.</summary>
[Unit]
public sealed class SkippedFileTests
{
    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Skips_a_file_when_reading_it_fails_with_a_data_IO_or_access_failure(Type failureType)
    {
        Assert.True(SkippedFile.Skips((Exception)Activator.CreateInstance(failureType)!));
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(Exception))]
    public void Does_not_skip_a_file_for_any_other_failure(Type failureType)
    {
        Assert.False(SkippedFile.Skips((Exception)Activator.CreateInstance(failureType)!));
    }

    [Fact]
    public void Reports_the_given_relative_path_and_the_failures_message_as_the_reason()
    {
        var report = SkippedFile.Of("contacts/a.json", new InvalidDataException("Damaged."));

        Assert.Equal("contacts/a.json", report.RelativePath);
        Assert.Equal("Damaged.", report.Reason);
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void Reports_a_failure_that_is_not_a_too_new_version_as_not_needing_a_newer_app(Type failureType)
    {
        var report = SkippedFile.Of("a.json", (Exception)Activator.CreateInstance(failureType, "Failed.")!);

        Assert.False(report.RequiresUpdate);
        Assert.Equal("Failed.", report.Reason);
    }
}
