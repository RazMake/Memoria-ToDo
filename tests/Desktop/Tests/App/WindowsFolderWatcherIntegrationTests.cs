using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The real watcher, repository, and Contacts store notice a mention templates file an editor replaced on disk.</summary>
[Integration]
public sealed class WindowsFolderWatcherIntegrationTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly string clone = Path.Combine(Path.GetTempPath(), "memoria-folder-watcher", Guid.NewGuid().ToString("N"));
    private CloneReloads? reloads;

    public void Dispose()
    {
        reloads?.Dispose();
        if (Directory.Exists(clone))
            Directory.Delete(clone, recursive: true);
    }

    private void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public async Task A_templates_file_replaced_by_a_rename_over_it_reaches_Contacts_through_the_real_watcher()
    {
        var path = TestContactStore.PathOf(clone, TestContactStore.TemplatesFile);
        Write(path, ContactFiles.Templates("{fullName}", "{firstName}"));
        Write(TestContactStore.PathOf(clone, TestContactStore.TeamFile), ContactFiles.Group(1000, ContactFiles.Person("1", "Ana")));
        var logs = new InMemoryLogSink();
        var repository = new SyncedRepository(logs.CreateLogger());
        var contacts = ContactStore.Create(new WindowsFileSystem(), repository, logs.CreateLogger()).Contacts;
        reloads = new CloneReloads(new WindowsFolderWatcher(), repository, TimeProvider.System, logs.CreateLogger());
        repository.Select(clone);
        reloads.Watch(clone);
        Assert.Equal(["{fullName}", "{firstName}"], contacts.MentionTemplates);
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        contacts.Changed += (_, _) =>
        {
            if (contacts.MentionTemplates is ["{firstName}", ..])
                reported.TrySetResult();
        };

        var replacement = path + ".tmp";
        Write(replacement, ContactFiles.Templates("{firstName}", "{fullName}"));
        File.Move(replacement, path, overwrite: true);
        await reported.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(["{firstName}", "{fullName}"], contacts.MentionTemplates);
    }
}
