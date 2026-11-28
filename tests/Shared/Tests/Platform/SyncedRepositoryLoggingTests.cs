using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>
/// What the synced repository logs: each load of the clone, and each file a store skipped, which is what names the
/// files behind a "can't be read" notice. Entries are checked by level and the facts their message names.
/// </summary>
[Unit]
public sealed class SyncedRepositoryLoggingTests
{
    private const string Clone = RepositoryRig.Clone;
    private readonly InMemoryLogSink logs = new();
    private readonly RepositoryRig rig;

    public SyncedRepositoryLoggingTests() => rig = new RepositoryRig(logs.CreateLogger());

    private static FakeSkippedFile[] Skipped(string folder, int count) =>
        [.. Enumerable.Range(1, count).Select(n => new FakeSkippedFile($"{folder}/file-{n}.json", $"Reason {n}."))];

    [Fact]
    public void Refuses_to_be_created_without_a_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new SyncedRepository(null!));
    }

    [Fact]
    public void Logs_one_information_entry_for_a_selection_naming_the_folder_and_how_many_files_each_store_skipped()
    {
        rig.Add("a").SkippedFiles = Skipped("tasks", 11);
        rig.Add("b").SkippedFiles = Skipped("contacts", 13);

        rig.Repository.Select(Clone);

        logs.Events.SingleAt(LogEventLevel.Information, Clone, "11", "13");
    }

    [Fact]
    public void Logs_an_information_entry_for_each_load_after_a_sync_update_too()
    {
        rig.Add("a");
        rig.Repository.Select(Clone);
        var before = logs.Events.Count;

        rig.Repository.Update(Clone, () => { });

        logs.After(before).SingleAt(LogEventLevel.Information, Clone);
    }

    [Fact]
    public void Logs_a_warning_for_each_skipped_file_naming_its_path_relative_to_the_clone_and_its_reason()
    {
        rig.Add("a").SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Damaged."), new FakeSkippedFile("tasks/b.json", "Cut off.")];
        rig.Add("b").SkippedFiles = [new FakeSkippedFile("autocomplete/contacts/team.json", "Not JSON.")];

        rig.Repository.Select(Clone);

        logs.Events.SingleAt(LogEventLevel.Warning, "tasks/a.json", "Damaged.");
        logs.Events.SingleAt(LogEventLevel.Warning, "tasks/b.json", "Cut off.");
        logs.Events.SingleAt(LogEventLevel.Warning, "autocomplete/contacts/team.json", "Not JSON.");
        Assert.Equal(3, logs.Events.Count(e => e.Level == LogEventLevel.Warning));
    }

    [Fact]
    public void Says_in_each_skipped_files_warning_whether_the_file_needs_a_newer_app()
    {
        rig.Add("a").SkippedFiles =
        [
            new FakeSkippedFile("tasks/too-new.json", "From a newer app.", RequiresUpdate: true),
            new FakeSkippedFile("tasks/broken.json", "Damaged.", RequiresUpdate: false),
        ];

        rig.Repository.Select(Clone);

        Assert.True(logs.Events.SingleAt(LogEventLevel.Warning, "tasks/too-new.json").HasFlag(true));
        Assert.True(logs.Events.SingleAt(LogEventLevel.Warning, "tasks/broken.json").HasFlag(false));
    }

    [Fact]
    public void Logs_no_warning_when_no_store_skipped_a_file()
    {
        rig.Add("a");

        rig.Repository.Select(Clone);

        logs.Events.AssertNoProblems();
    }
}
