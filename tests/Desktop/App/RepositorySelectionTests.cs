using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class RepositorySelectionTests
{
    private const string Setting = @"C:\Selection tests\device\repository-folder.txt";

    [Fact]
    public void No_saved_selection_leaves_Tasks_without_a_repository()
    {
        var files = new FakeFileSystem();
        var tasks = Store(files);

        new RepositorySelection(files, tasks, Setting).Load();

        Assert.Null(tasks.Synchronization.RepositoryFolder);
        Assert.Empty(files.AllFilesUnder(@"C:\Selection tests"));
    }

    [Fact]
    public void Creating_the_selection_reads_nothing_and_leaves_Tasks_unchanged()
    {
        var files = new FakeFileSystem();
        files.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Existing"));
        files.FailReadsUnder(@"C:\Selection tests\device");
        var tasks = Store(files);

        _ = new RepositorySelection(files, tasks, Setting);

        Assert.Null(tasks.Synchronization.RepositoryFolder);
    }

    [Fact]
    public void Loading_an_unreadable_selection_reports_the_read_error_and_leaves_Tasks_unchanged()
    {
        var inner = new FakeFileSystem();
        inner.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Existing"));
        var files = new UnprobeableFileSystem(inner, Setting);
        var tasks = Store(inner);

        Assert.Throws<UnauthorizedAccessException>(() => new RepositorySelection(files, tasks, Setting).Load());

        Assert.Null(tasks.Synchronization.RepositoryFolder);
        Assert.Equal(0, files.Writes);
    }

    [Fact]
    public void Loading_a_saved_path_that_is_not_a_full_path_reports_invalid_data_and_keeps_the_file()
    {
        var files = new FakeFileSystem();
        var saved = Encoding.UTF8.GetBytes(@"not-a-full-path");
        files.WriteFile(Setting, saved);
        var tasks = Store(files);

        Assert.Throws<System.IO.InvalidDataException>(() => new RepositorySelection(files, tasks, Setting).Load());

        Assert.Null(tasks.Synchronization.RepositoryFolder);
        Assert.Equal(saved, files.ReadFile(Setting));
    }

    [Fact]
    public void Selecting_persists_the_folder_and_makes_the_same_folder_current_in_Tasks()
    {
        var files = new FakeFileSystem();
        var tasks = Store(files);
        var selection = new RepositorySelection(files, tasks, Setting);

        selection.Select(@"C:\Tasks");

        Assert.Equal(@"C:\Tasks", tasks.Synchronization.RepositoryFolder);
        Assert.Equal(@"C:\Tasks", Encoding.UTF8.GetString(files.ReadFile(Setting)!));
        var restarted = Store(files);
        new RepositorySelection(files, restarted, Setting).Load();
        Assert.Equal(@"C:\Tasks", restarted.Synchronization.RepositoryFolder);
    }

    [Fact]
    public void The_existing_selected_path_file_is_loaded_without_a_second_setting()
    {
        var files = new FakeFileSystem();
        files.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Existing"));
        var tasks = Store(files);

        new RepositorySelection(files, tasks, Setting).Load();

        Assert.Equal(@"C:\Existing", tasks.Synchronization.RepositoryFolder);
        Assert.Equal([Setting], files.AllFilesUnder(@"C:\Selection tests"));
    }

    [Fact]
    public void Failed_persistence_keeps_the_old_folder_current()
    {
        var files = new FakeFileSystem();
        var tasks = Store(files);
        var selection = new RepositorySelection(files, tasks, Setting);
        selection.Select(@"C:\Old");
        files.FailWritesUnder(@"C:\Selection tests\device");

        Assert.Throws<System.IO.IOException>(() => selection.Select(@"C:\New"));

        Assert.Equal(@"C:\Old", tasks.Synchronization.RepositoryFolder);
        Assert.Equal(@"C:\Old", Encoding.UTF8.GetString(files.ReadFile(Setting)!));
    }

    [Fact]
    public void A_folder_Tasks_cannot_adopt_restores_the_exact_previous_setting_and_keeps_the_old_folder()
    {
        var files = new FakeFileSystem();
        var previous = Encoding.UTF8.GetBytes("C:\\Old\r\n");
        files.WriteFile(Setting, previous);
        files.WriteFile(@"C:\Bad\tasks", [1]);
        var tasks = Store(files);
        var selection = new RepositorySelection(files, tasks, Setting);
        selection.Load();

        Assert.Throws<System.IO.IOException>(() => selection.Select(@"C:\Bad"));

        Assert.Equal(@"C:\Old", tasks.Synchronization.RepositoryFolder);
        Assert.Equal(previous, files.ReadFile(Setting));
    }

    [Fact]
    public void A_folder_Tasks_cannot_adopt_removes_a_setting_that_did_not_exist_before()
    {
        var files = new FakeFileSystem();
        files.WriteFile(@"C:\Bad\tasks", [1]);
        var tasks = Store(files);
        var selection = new RepositorySelection(files, tasks, Setting);
        selection.Load();

        Assert.Throws<System.IO.IOException>(() => selection.Select(@"C:\Bad"));

        Assert.Null(tasks.Synchronization.RepositoryFolder);
        Assert.False(files.FileExists(Setting));
    }

    [Fact]
    public void A_failed_adoption_and_a_failed_restore_are_both_reported()
    {
        var inner = new FakeFileSystem();
        inner.WriteFile(Setting, Encoding.UTF8.GetBytes(@"C:\Old"));
        inner.WriteFile(@"C:\Bad\tasks", [1]);
        var tasks = Store(inner);
        var selection = new RepositorySelection(new WriteLimitedFileSystem(inner, allowedWrites: 1), tasks, Setting);
        selection.Load();

        var failure = Assert.Throws<AggregateException>(() => selection.Select(@"C:\Bad"));

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.All(failure.InnerExceptions, error => Assert.IsAssignableFrom<System.IO.IOException>(error));
        Assert.Single(failure.InnerExceptions, error => error.Message == WriteLimitedFileSystem.RefusalMessage);
        Assert.Equal(@"C:\Old", tasks.Synchronization.RepositoryFolder);
    }

    [Fact]
    public void Selecting_never_overwrites_a_setting_that_cannot_be_read()
    {
        var inner = new FakeFileSystem();
        var previous = Encoding.UTF8.GetBytes(@"C:\Old");
        inner.WriteFile(Setting, previous);
        var files = new UnprobeableFileSystem(inner, Setting);
        var tasks = Store(inner);
        var selection = new RepositorySelection(files, tasks, Setting);

        Assert.Throws<UnauthorizedAccessException>(() => selection.Select(@"C:\New"));

        Assert.Equal(0, files.Writes);
        Assert.Equal(previous, inner.ReadFile(Setting));
        Assert.Null(tasks.Synchronization.RepositoryFolder);
    }

    private static ITaskStore Store(FakeFileSystem files) =>
        TaskStore.Create(files, new FakeImageEncoder(), new FakeTimeProvider(), @"C:\Selection tests\draft");
}
