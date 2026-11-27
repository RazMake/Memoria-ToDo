using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>After a save, Contacts raises Changed and then the repository reports a local save, on the calling thread and outside the lock; a failing notice after the save is thrown as NoticeFailedException of the contact.</summary>
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
    public void Keeps_the_saved_contact_and_still_reports_the_local_save_when_a_Changed_handler_fails()
    {
        f.Contacts.Changed += (_, _) => throw new InvalidOperationException("handler");
        var before = f.Notices.Count;

        Assert.Throws<NoticeFailedException<Contact>>(() => f.Contacts.Save("team", new Contact("1", "Ana Popa")));

        Assert.Equal(["Changed", "LocalSaved"], NoticesSince(before));
        Assert.Equal("Ana Popa", f.Member("team", "1").FullName);
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
