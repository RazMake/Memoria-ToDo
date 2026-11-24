using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Shared setup for the direct task change tests: what changed on disk, and how often Tasks announced a change.</summary>
internal static class DirectChangeChecks
{
    /// <summary>Every file in the clone and the device folder with its bytes, to compare before and after a call.</summary>
    public static Dictionary<string, string> Snapshot(this TasksFixture f) =>
        f.Files.AllFilesUnder(f.Clone).Concat(f.Files.AllFilesUnder(f.Device))
            .ToDictionary(path => path, path => Convert.ToBase64String(f.Files.ReadFile(path)));

    /// <summary>Every file in the device folder (the draft and waiting images) with its bytes.</summary>
    public static Dictionary<string, string> DeviceSnapshot(this TasksFixture f) =>
        f.Files.AllFilesUnder(f.Device).ToDictionary(path => path, path => Convert.ToBase64String(f.Files.ReadFile(path)));

    /// <summary>Asserts the kept draft holds what <paramref name="expected"/> does; a draft's tags are a list, so record equality would compare list references.</summary>
    public static void AssertDraftKept(this TasksFixture f, TaskDraft expected)
    {
        var kept = Xunit.Assert.IsType<TaskDraft>(f.Tasks.LoadDraft());
        Xunit.Assert.Equal(expected.TaskId, kept.TaskId);
        Xunit.Assert.Equal(expected.Title, kept.Title);
        Xunit.Assert.Equal(expected.Description, kept.Description);
        Xunit.Assert.Equal(expected.Tags, kept.Tags);
    }

    /// <summary>Counts the local save reports from now on.</summary>
    public static TasksFixture.Counter CountLocalSaves(this TasksFixture f)
    {
        var counter = new TasksFixture.Counter();
        f.Repository.LocalSaved += (_, _) => counter.Count++;
        return counter;
    }

    /// <summary>The time the seeded tasks carry as Updated.</summary>
    public static readonly DateTimeOffset SeededTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
