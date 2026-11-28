using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// A change that raises notices can't run under the repository's lock, because the notices would then run under it.
/// Tasks refuses it before it changes anything.
/// </summary>
[Unit]
public sealed class TaskLockedChangeTests
{
    private readonly TasksFixture f = new();
    private int notices;

    public static IEnumerable<TheoryDataRow<Action<ITaskStore>>> Changes() =>
    [
        Change("Add", s => s.Add(new TaskDraft("01900000-0000-7000-8000-000000001000", "New", "", []))),
        Change("SaveEdit", s => s.SaveEdit(s.All.Single(t => t.Id == Open), new TaskDraft(Open, "Edited", "", ["work"]))),
        Change("Delete", s => s.Delete(Open)),
        Change("Move", s => s.Move(Expired, null, Open)),
        Change("Resolve", s => s.Resolve(Open)),
        Change("Reopen", s => s.Reopen(Done)),
        Change("SetDeletionDelayDays", s => s.Settings.SetDeletionDelayDays(3)),
        Change("SetAddNewTaskShortcut", s => s.Settings.SetAddNewTaskShortcut("Ctrl+K")),
        Change("SetManageTasksShortcut", s => s.Settings.SetManageTasksShortcut("Ctrl+K")),
        Change("SetCloseWindowShortcut", s => s.Settings.SetCloseWindowShortcut("Ctrl+K")),
        Change("SetSaveShortcut", s => s.Settings.SetSaveShortcut("Ctrl+K")),
        Change("SetConfigureShortcut", s => s.Settings.SetConfigureShortcut("Ctrl+K")),
        Change("SetContactsShortcut", s => s.Settings.SetContactsShortcut("Ctrl+K")),
        Change("SetColor", s => s.TagColors.SetColor("work", "red")),
        Change("TagOrder.Move", s => s.TagOrder.Move("work", null, "home")),
        Change("DeleteExpiredTasks", s => s.Synchronization.DeleteExpiredTasks()),
    ];

    private const string Open = "01900000-0000-7000-8000-000000000001";
    private const string Done = "01900000-0000-7000-8000-000000000002";
    private const string Expired = "01900000-0000-7000-8000-000000000003";

    private static TheoryDataRow<Action<ITaskStore>> Change(string name, Action<ITaskStore> change) =>
        new(change) { TestDisplayName = name };

    public TaskLockedChangeTests()
    {
        f.Seed(Open, "Open", rank: "a0", tags: ["work"]);
        f.Seed(Done, "Done", rank: "a1", tags: ["home"], resolved: "2026-11-11T08:30:15.123Z");
        f.Seed(Expired, "Expired", rank: "a2", resolved: "2020-01-01T00:00:00.000Z");
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.Changed += (_, _) => notices++;
        f.Store.Settings.Changed += (_, _) => notices++;
        f.Store.TagColors.Changed += (_, _) => notices++;
        f.Store.TagOrder.Changed += (_, _) => notices++;
        f.Repository.LocalSaved += (_, _) => notices++;
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void Refuses_a_change_under_the_repositorys_lock_and_changes_nothing(Action<ITaskStore> change)
    {
        var before = Observed();

        Assert.Throws<InvalidOperationException>(() => f.Repository.Locked(_ => change(f.Store)));

        Assert.Equal(before, Observed());
        Assert.Equal(0, notices);
    }

    [Fact]
    public void Still_reads_the_tasks_under_the_repositorys_lock()
    {
        var count = f.Repository.Locked(_ => f.Tasks.All.Count);

        Assert.Equal(3, count);
    }

    /// <summary>Everything a caller can see: the tasks, the settings, a tag's color and place, and every file.</summary>
    private string Observed()
    {
        var tasks = f.Tasks.All.Select(t => $"{t.Id}|{t.Title}|{t.Description}|{t.Rank}|{string.Join(',', t.Tags)}|{t.Updated:O}|{t.Resolved:O}");
        var settings = new object?[]
        {
            f.Store.Settings.DeletionDelayDays, f.Store.Settings.AddNewTaskShortcut, f.Store.Settings.ManageTasksShortcut,
            f.Store.Settings.CloseWindowShortcut, f.Store.Settings.SaveShortcut, f.Store.Settings.ConfigureShortcut,
                        f.Store.Settings.ContactsShortcut,            f.Store.TagColors.ColorOf("work"), string.Join(',', f.Store.TagOrder.Sort(["work", "home"])),
        };
        var root = Path.GetDirectoryName(f.Clone)!;
        var files = f.Files.AllFilesUnder(root).Order(StringComparer.Ordinal)
            .Select(path => path + "=" + Convert.ToBase64String(f.Files.ReadFile(path)));
        return string.Join("\n", tasks.Concat(settings.Select(s => s?.ToString() ?? "null")).Concat(files));
    }
}
