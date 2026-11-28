using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>Contacts answers one call at a time, so a read never sees a half-finished reload.</summary>
[Unit]
public sealed class ThreadingTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly ContactsFixture f = new();

    [Fact]
    public async Task Answers_reads_from_several_threads_at_once()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"), ContactFiles.Person("2", "Bob"));
        f.SeedDataTypes();
        f.Load();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => ContactsFixture.Ids(f.Contacts.Search("")), TestContext.Current.CancellationToken)))
            .WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.All(results, ids => Assert.Equal(["1", "2"], ids));
    }

    [Fact]
    public async Task Raises_Changed_for_a_save_on_the_calling_thread_outside_the_lock()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.SeedDataTypes();
        f.Load();
        var before = f.ChangedThreads.Count;

        var callingThread = await Task.Run(() =>
        {
            f.Contacts.Save("team", new Contact("1", "Ana Popa"));
            return Environment.CurrentManagedThreadId;
        }, TestContext.Current.CancellationToken).WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal([callingThread], f.ChangedThreads.Skip(before));
        Assert.All(f.ChangedOutsideLock.Skip(before), outside => Assert.True(outside));
    }

    [Fact]
    public void Raises_Changed_for_a_delete_on_the_calling_thread_outside_the_lock()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.SeedDataTypes();
        f.Load();
        var before = f.ChangedThreads.Count;

        f.Contacts.Delete("team", "1");

        Assert.Equal([Environment.CurrentManagedThreadId], f.ChangedThreads.Skip(before));
        Assert.All(f.ChangedOutsideLock.Skip(before), outside => Assert.True(outside));
    }

    [Fact]
    public void Raises_Changed_for_a_group_move_on_the_calling_thread_outside_the_lock()
    {
        f.SeedGroup("a", 1000);
        f.SeedGroup("b", 2000);
        f.SeedDataTypes();
        f.Load();
        var before = f.ChangedThreads.Count;

        f.Contacts.MoveGroup("b", null, "a");

        Assert.Equal([Environment.CurrentManagedThreadId], f.ChangedThreads.Skip(before));
        Assert.All(f.ChangedOutsideLock.Skip(before), outside => Assert.True(outside));
    }
}

/// <summary>Changed follows every reload of the repository, whether or not anything changed.</summary>
[Unit]
public sealed class ChangedAfterReloadTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Is_raised_after_a_selection_even_when_the_clone_has_no_contacts()
    {
        f.Load();

        Assert.Equal(["Changed"], f.Notices);
    }

    [Fact]
    public void Is_raised_after_an_update_that_changed_nothing()
    {
        f.SeedGroup("team", 1000);
        f.SeedDataTypes();
        f.Load();
        var before = f.Notices.Count;

        f.Repository.Update(f.Clone, () => { });

        Assert.Equal(["Changed"], f.Notices.Skip(before));
    }

    [Fact]
    public void Is_raised_on_the_thread_that_reloads_outside_the_lock()
    {
        f.Load();

        Assert.Equal([Environment.CurrentManagedThreadId], f.ChangedThreads);
        Assert.All(f.ChangedOutsideLock, outside => Assert.True(outside));
    }
}

/// <summary>The skipped files Contacts reports, to its caller and to the repository.</summary>
[Unit]
public sealed class SkippedFilesReportTests
{
    private readonly ContactsFixture f = new();

    [Fact]
    public void Lists_the_files_of_the_latest_load_with_a_reason_each()
    {
        f.SeedGroup("team", 1000);
        f.Seed(ContactsFixture.Folder + "/bad.json", "{ not json");
        f.SeedDataTypes();
        f.Load();

        f.AssertSkipped(ContactsFixture.Folder + "/bad.json");
        Assert.Single(f.Contacts.SkippedFiles);

        f.Repository.Update(f.Clone, () => f.Disk.DeleteFile(f.PathOf(ContactsFixture.Folder + "/bad.json")));

        Assert.Empty(f.Contacts.SkippedFiles);
    }

    [Fact]
    public void Appears_in_the_repository_list_of_skipped_files()
    {
        f.SeedGroup("team", 1000);
        f.Seed(ContactsFixture.Folder + "/bad.json", "{ not json");
        f.SeedDataTypes();
        f.Load();

        Assert.Equal(
            [ContactsFixture.Folder + "/bad.json"],
            f.Repository.SkippedFiles.Select(file => file.RelativePath));
        Assert.Equal(
            f.Contacts.SkippedFiles.Select(file => file.Reason),
            f.Repository.SkippedFiles.Select(file => file.Reason));
    }
}
