using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class RepositorySelectionTests
{
    private const string Setting = @"C:\Selection tests\device\repository-folder.txt";
    private const string BadFolder = @"C:\Bad";

    // What the selection tells Tasks to use, in order; BadFolder is a folder Tasks cannot adopt.
    private readonly List<string?> used = [];

    private readonly InMemoryLogSink log = new();

    private RepositorySelection Selection(IFileSystem files) => new(files, UseRepositoryFolder, Setting, log.CreateLogger());

    [Fact]
    public void Creating_the_selection_without_a_logger_is_refused()
    {
        var error = Assert.Throws<ArgumentNullException>(
            () => new RepositorySelection(new FakeFileSystem(), UseRepositoryFolder, Setting, null!));

        Assert.Equal("logger", error.ParamName);
    }

    [Fact]
    public void Selecting_a_folder_logs_one_information_entry_that_names_the_folder_and_no_problem()
    {
        var selection = Selection(new FakeFileSystem());

        selection.Select(@"C:\Tasks");

        var entry = log.Events.SingleAt(LogEventLevel.Information, @"C:\Tasks");
        Assert.Null(entry.Exception);
        log.Events.AssertNoProblems();
    }

    [Fact]
    public void Selecting_a_folder_Tasks_cannot_adopt_logs_no_chosen_folder_and_leaves_the_failure_to_the_caller()
    {
        var selection = Selection(new FakeFileSystem());

        Assert.Throws<IOException>(() => selection.Select(BadFolder));

        Assert.DoesNotContain(log.Events, entry => entry.Level == LogEventLevel.Information);
        log.Events.AssertNoProblems();
    }

    [Fact]
    public void A_selection_that_cannot_be_saved_logs_no_chosen_folder_and_leaves_the_failure_to_the_caller()
    {
        var files = new FakeFileSystem();
        files.FailWritesUnder(@"C:\Selection tests\device");

        Assert.Throws<IOException>(() => Selection(files).Select(@"C:\New"));

        Assert.DoesNotContain(log.Events, entry => entry.Level == LogEventLevel.Information);
        log.Events.AssertNoProblems();
    }

    private void UseRepositoryFolder(string? folder)
    {
        if (folder == BadFolder)
        {
            throw new IOException("Tasks cannot adopt this folder.");
        }

        used.Add(folder);
    }

    [Fact]
    public void No_saved_selection_leaves_Tasks_without_a_repository()
    {
        var files = new FakeFileSystem();

        Selection(files).Load();

        Assert.Equal([null], used);
        Assert.Empty(files.AllFilesUnder(@"C:\Selection tests"));
    }

    [Fact]
    public void Creating_the_selection_reads_no_file_and_leaves_Tasks_unchanged()
    {
        var files = new FakeFileSystem();
        files.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Existing"));
        files.FailReadsUnder(@"C:\Selection tests\device");

        _ = Selection(files);

        Assert.Empty(used);
    }

    [Fact]
    public void Loading_an_unreadable_selection_reports_the_read_error_and_leaves_Tasks_unchanged()
    {
        var inner = new FakeFileSystem();
        inner.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Existing"));
        var files = new UnprobeableFileSystem(inner, Setting);

        Assert.Throws<UnauthorizedAccessException>(() => Selection(files).Load());

        Assert.Empty(used);
        Assert.Equal(0, files.Writes);
    }

    [Fact]
    public void Loading_a_saved_path_that_is_not_a_full_path_reports_invalid_data_and_keeps_the_file()
    {
        var files = new FakeFileSystem();
        var saved = Encoding.UTF8.GetBytes("not-a-full-path");
        files.WriteFile(Setting, saved);

        Assert.Throws<InvalidDataException>(() => Selection(files).Load());

        Assert.Empty(used);
        Assert.Equal(saved, files.ReadFile(Setting));
    }

    [Fact]
    public void Loading_a_saved_path_makes_that_folder_current_and_adds_no_second_setting()
    {
        var files = new FakeFileSystem();
        files.WriteFile(Setting, Encoding.UTF8.GetBytes("C:\\Existing\r\n"));

        Selection(files).Load();

        Assert.Equal([@"C:\Existing"], used);
        Assert.Equal([Setting], files.AllFilesUnder(@"C:\Selection tests"));
    }

    [Fact]
    public void Selecting_persists_the_folder_and_makes_the_same_folder_current()
    {
        var files = new FakeFileSystem();

        Selection(files).Select(@"C:\Tasks");

        Assert.Equal([@"C:\Tasks"], used);
        Assert.Equal(@"C:\Tasks", Encoding.UTF8.GetString(files.ReadFile(Setting)!));
    }

    [Fact]
    public void A_selection_saved_by_one_run_is_loaded_by_the_next()
    {
        var files = new FakeFileSystem();
        Selection(files).Select(@"C:\Tasks");
        used.Clear();

        Selection(files).Load();

        Assert.Equal([@"C:\Tasks"], used);
    }

    [Fact]
    public void Failed_persistence_keeps_the_old_folder_current()
    {
        var files = new FakeFileSystem();
        var selection = Selection(files);
        selection.Select(@"C:\Old");
        files.FailWritesUnder(@"C:\Selection tests\device");

        Assert.Throws<IOException>(() => selection.Select(@"C:\New"));

        Assert.Equal([@"C:\Old"], used);
        Assert.Equal(@"C:\Old", Encoding.UTF8.GetString(files.ReadFile(Setting)!));
    }

    [Fact]
    public void A_folder_Tasks_cannot_adopt_restores_the_exact_previous_setting_and_keeps_the_old_folder()
    {
        var files = new FakeFileSystem();
        var previous = Encoding.UTF8.GetBytes("C:\\Old\r\n");
        files.WriteFile(Setting, previous);
        var selection = Selection(files);
        selection.Load();

        Assert.Throws<IOException>(() => selection.Select(BadFolder));

        Assert.Equal([@"C:\Old"], used);
        Assert.Equal(previous, files.ReadFile(Setting));
    }

    [Fact]
    public void A_folder_Tasks_cannot_adopt_removes_a_setting_that_did_not_exist_before()
    {
        var files = new FakeFileSystem();
        var selection = Selection(files);

        Assert.Throws<IOException>(() => selection.Select(BadFolder));

        Assert.Empty(used);
        Assert.False(files.FileExists(Setting));
    }

    [Fact]
    public void A_failed_adoption_and_a_failed_restore_are_both_reported()
    {
        var inner = new FakeFileSystem();
        inner.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Old"));
        var selection = Selection(new WriteLimitedFileSystem(inner, allowedWrites: 1));
        selection.Load();

        var failure = Assert.Throws<AggregateException>(() => selection.Select(BadFolder));

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.All(failure.InnerExceptions, error => Assert.IsAssignableFrom<IOException>(error));
        Assert.Single(failure.InnerExceptions, error => error.Message == WriteLimitedFileSystem.RefusalMessage);
        Assert.Equal([@"C:\Old"], used);
    }

    [Fact]
    public void Selecting_never_overwrites_a_setting_that_cannot_be_read()
    {
        var inner = new FakeFileSystem();
        var previous = Encoding.UTF8.GetBytes(@"C:\Old");
        inner.WriteFile(Setting, previous);
        var files = new UnprobeableFileSystem(inner, Setting);

        Assert.Throws<UnauthorizedAccessException>(() => Selection(files).Select(@"C:\New"));

        Assert.Equal(0, files.Writes);
        Assert.Equal(previous, inner.ReadFile(Setting));
        Assert.Empty(used);
    }
}
