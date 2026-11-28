using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

[Unit]
public class LocalApiCreateTests
{
    private static readonly FakeTaskStore Tasks = new();
    private static readonly FakeSyncService Sync = new();
    private static readonly Memoria.ToDo.Shared.Contacts.IContacts Contacts = TestContactStore.Create(new FakeFileSystem()).Contacts;

    [Fact]
    public void Create_rejects_missing_tasks()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => LocalApiServer.Create(null!, Contacts, Sync, "pipe", Logger.None));

        Assert.Equal("tasks", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_missing_contacts()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => LocalApiServer.Create(Tasks, null!, Sync, "pipe", Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_sync()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => LocalApiServer.Create(Tasks, Contacts, null!, "pipe", Logger.None));

        Assert.Equal("sync", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_pipe_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => LocalApiServer.Create(Tasks, Contacts, Sync, null!, Logger.None));

        Assert.Equal("pipeName", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_logger()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => LocalApiServer.Create(Tasks, Contacts, Sync, "pipe", null!));

        Assert.Equal("logger", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_an_empty_pipe_name()
    {
        var failure = Assert.Throws<ArgumentException>(() => LocalApiServer.Create(Tasks, Contacts, Sync, "", Logger.None));

        Assert.Equal("pipeName", failure.ParamName);
        Assert.IsNotType<ArgumentNullException>(failure);
    }

    [Fact]
    public void Create_returns_a_lifetime()
    {
        var lifetime = LocalApiServer.Create(Tasks, Contacts, Sync, "Memoria-ToDo.LocalApi.test.never-started", Logger.None);

        Assert.NotNull(lifetime);
    }
}
