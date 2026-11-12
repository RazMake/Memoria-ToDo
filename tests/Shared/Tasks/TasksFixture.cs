using System.Text;
using System.Text.Json;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>
/// The setup every Tasks test shares: Tasks over the in-memory file system, image encoder, and clock, with a clone
/// folder and a device folder, and helpers that seed task files as literal JSON, so the tests don't depend on how Tasks writes them.
/// </summary>
internal sealed class TasksFixture
{
    public static readonly DateTimeOffset Start = new(2026, 11, 12, 8, 30, 15, 123, TimeSpan.Zero);

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "memoria-tasks-tests");
    private int nextDraft = 1000;

    public TasksFixture()
    {
        Time = new FakeTimeProvider(Start);
        Tasks = TaskStore.Create(Files, Encoder, Time, Device);
    }

    public FakeFileSystem Files { get; } = new();
    public FakeImageEncoder Encoder { get; } = new();
    public FakeTimeProvider Time { get; }
    public ITasks Tasks { get; }

    /// <summary>The same store, with the settings and synchronization capabilities.</summary>
    public ITaskStore Store => Xunit.Assert.IsAssignableFrom<ITaskStore>(Tasks);

    public string Clone { get; } = Path.Combine(Root, "clone");
    public string Device { get; } = Path.Combine(Root, "device");

    /// <summary>A valid task ID, lowercase with hyphens, UUID version 7 in shape; IDs sort in the order of <paramref name="n"/>.</summary>
    public static string Id(int n) => $"01900000-0000-7000-8000-{n:D12}";

    public string TaskFile(string id) => Path.Combine(Clone, "tasks", id + ".json");
    public string DraftFile => Path.Combine(Device, "draft.json");
    public string DraftImages(string id) => Path.Combine(Device, "images", id);
    public string CloneImages(string id) => Path.Combine(Clone, "images", id);

    /// <summary>Another Tasks over the same files, as after the app restarts.</summary>
    public ITasks Restarted() => TaskStore.Create(Files, Encoder, Time, Device);

    public string SettingsFile => Path.Combine(Clone, "settings.json");

    /// <summary>Writes settings.json in the clone as literal JSON. Each setting is a value and a UTC time.</summary>
    public void SeedSettings(int days = 7, string? add = null, string? manage = null, int version = 1,
        string daysTime = "2026-01-01T00:00:00.000Z", string addTime = "2026-01-01T00:00:00.000Z", string manageTime = "2026-01-01T00:00:00.000Z") =>
        WriteText(SettingsFile, SettingsJson(days, add, manage, version, daysTime, addTime, manageTime));

    public static string SettingsJson(int days = 7, string? add = null, string? manage = null, int version = 1,
        string daysTime = "2026-01-01T00:00:00.000Z", string addTime = "2026-01-01T00:00:00.000Z", string manageTime = "2026-01-01T00:00:00.000Z") =>
        $$"""
        {
          "version": {{version}},
          "deletionDelayDays": { "value": {{days}}, "updated": {{Quote(daysTime)}} },
          "addNewTaskShortcut": { "value": {{(add is null ? "null" : Quote(add))}}, "updated": {{Quote(addTime)}} },
          "manageTasksShortcut": { "value": {{(manage is null ? "null" : Quote(manage))}}, "updated": {{Quote(manageTime)}} }
        }
        """;

    public void WriteText(string path, string text) => Files.WriteFile(path, Encoding.UTF8.GetBytes(text));

    public string ReadText(string path) => Encoding.UTF8.GetString(Files.ReadFile(path));

    public JsonDocument ReadJson(string path) => JsonDocument.Parse(Files.ReadFile(path));

    /// <summary>Writes a task file in the clone as the design describes it.</summary>
    public void Seed(string id, string title = "Seeded", string rank = "a0", string description = "",
        string[]? tags = null, string? resolved = null, int version = 1,
        string created = "2026-01-01T00:00:00.000Z", string updated = "2026-01-01T00:00:00.000Z")
    {
        var tagList = string.Join(", ", (tags ?? []).Select(Quote));
        var resolvedValue = resolved is null ? "null" : Quote(resolved);
        WriteText(TaskFile(id),
            $$"""
            {
              "id": {{Quote(id)}},
              "title": {{Quote(title)}},
              "description": {{Quote(description)}},
              "rank": {{Quote(rank)}},
              "tags": [{{tagList}}],
              "created": {{Quote(created)}},
              "updated": {{Quote(updated)}},
              "resolved": {{resolvedValue}},
              "version": {{version}}
            }
            """);
    }

    /// <summary>Seeds a task and has Tasks read the clone.</summary>
    public void SeedAndUse(string id, string title = "Seeded", string rank = "a0", string[]? tags = null, string description = "", string? resolved = null)
    {
        Seed(id, title, rank, description, tags, resolved);
        Tasks.UseRepositoryFolder(Clone);
    }

    /// <summary>Seeds one open task per tag list, then has Tasks read the clone.</summary>
    public void SeedTagsAndUse(params string[][] tagLists)
    {
        for (var i = 0; i < tagLists.Length; i++)
            Seed(Id(i + 1), rank: $"a{i}", tags: tagLists[i]);
        Tasks.UseRepositoryFolder(Clone);
    }

    /// <summary>A draft with a new valid ID.</summary>
    public TaskDraft Draft(string title = "A task", string description = "", params string[] tags) =>
        new(Id(nextDraft++), title, description, tags);

    /// <summary>Opens the clone and adds one task.</summary>
    public TaskItem AddTask(string title = "A task", string description = "", params string[] tags)
    {
        Tasks.UseRepositoryFolder(Clone);
        return Tasks.Add(Draft(title, description, tags));
    }

    public IReadOnlyList<string> Titles => Tasks.All.Select(t => t.Title).ToList();

    public IReadOnlyList<string> Ids => Tasks.All.Select(t => t.Id).ToList();

    /// <summary>Counts <see cref="ITasks.Changed"/> notices from now on.</summary>
    public Counter CountChanges()
    {
        var counter = new Counter();
        Tasks.Changed += (_, _) => counter.Count++;
        return counter;
    }

    public sealed class Counter
    {
        public int Count { get; set; }
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
