using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>After a save, Contacts raises Changed and then the repository reports a local save, on the calling thread and outside the lock.</summary>
[Unit]
public sealed class SaveNoticeTests
{
    private readonly ContactsFixture f = new();

    public SaveNoticeTests()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"));
        f.Load();
    }

    private List<string> NoticesSince(int count) => [.. f.Notices.Skip(count)];

    [Fact]
    public void Raises_Changed_then_the_repository_s_LocalSaved()
    {
        var before = f.Notices.Count;

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.Equal(["Changed", "LocalSaved"], NoticesSince(before));
    }

    [Fact]
    public void Raises_Changed_on_the_calling_thread()
    {
        var before = f.ChangedThreads.Count;

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.Equal(Environment.CurrentManagedThreadId, f.ChangedThreads[before]);
    }

    [Fact]
    public async Task Raises_Changed_on_another_calling_thread_when_it_saves()
    {
        var before = f.ChangedThreads.Count;
        var saverThread = await Task.Run(() =>
        {
            f.Contacts.Save("team", new Contact("1", "Ana Popa"));
            return Environment.CurrentManagedThreadId;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(saverThread, f.ChangedThreads[before]);
    }

    [Fact]
    public void Raises_Changed_outside_the_repository_s_lock()
    {
        var before = f.ChangedOutsideLock.Count;

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.True(f.ChangedOutsideLock[before]);
    }

    [Fact]
    public void Still_reports_the_local_save_and_throws_the_handler_s_exception_as_it_is_when_a_Changed_handler_fails()
    {
        var failure = new InvalidOperationException("handler");
        f.Contacts.Changed += (_, _) => throw failure;
        var before = f.Notices.Count;

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "LocalSaved"], NoticesSince(before));
        Assert.Equal("Ana Popa", f.Member("team", "1").FullName);
    }

    [Fact]
    public void Still_raises_Changed_and_throws_the_report_s_exception_as_it_is_when_the_local_save_report_fails()
    {
        var failure = new InvalidOperationException("report");
        f.Repository.LocalSaved += (_, _) => throw failure;
        var before = f.Notices.Count;

        var thrown = Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));

        Assert.Same(failure, thrown);
        Assert.Equal(["Changed", "LocalSaved"], NoticesSince(before));
    }

    [Fact]
    public void Throws_an_AggregateException_with_Changed_s_failure_then_the_report_s_when_both_fail()
    {
        var changed = new InvalidOperationException("handler");
        var report = new InvalidOperationException("report");
        f.Contacts.Changed += (_, _) => throw changed;
        f.Repository.LocalSaved += (_, _) => throw report;

        var thrown = Assert.Throws<AggregateException>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));

        Assert.Equal(new Exception[] { changed, report }, thrown.InnerExceptions);
    }

    [Fact]
    public void Shows_the_saved_contact_inside_the_Changed_handler()
    {
        string? seen = null;
        f.Contacts.Changed += (_, _) => seen = f.Contacts.Find("1")?.FullName;

        f.Contacts.Save("team", new Contact("1", "Ana Popa"));

        Assert.Equal("Ana Popa", seen);
    }
}
