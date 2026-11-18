using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// A real Tasks module over the fake files and the fake clock, with no window. The setup unit tests share;
/// <see cref="ListServices"/> adds the ports a window needs. Shared test setup, not a fake of a port.
/// </summary>
internal class ListTasks
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "task-list-tests");

    public FakeFileSystem Files { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public ITasks Tasks { get; }

    /// <summary>Every file the fake file system holds, task files and images alike.</summary>
    public IReadOnlyList<string> AllFiles => Files.AllFilesUnder(Root);

    public string Clone { get; } = Path.Combine(Root, "clone");

    /// <summary>The folder Tasks writes task files to; failing writes under it makes every save fail.</summary>
    public string TaskFolder => Path.Combine(Clone, "tasks");

    public ListTasks()
    {
        Tasks = TestTaskStore.Create(Files, new FakeImageEncoder(), Time, Path.Combine(Root, "device"));
        Tasks.UseRepositoryFolder(Clone);
    }

    /// <summary>The tag colors the window shows tags in.</summary>
    public FakeTagColors TagColors { get; } = new();

    /// <summary>The tag order the window sorts its groups by and saves a group's move in.</summary>
    public FakeTagOrder TagOrder { get; } = new();

    /// <summary>Pastes an image into an edit of the task and returns the files the paste added.</summary>
    public IReadOnlyList<string> PasteImage(string taskId)
    {
        var before = AllFiles;
        Tasks.SaveImage(taskId, FakeImageEncoder.Image(ImageFormat.Png, 100, 80), "photo");
        var added = AllFiles.Except(before).ToList();
        Assert.NotEmpty(added);
        return added;
    }

    /// <summary>Makes deleting the pasted images fail, as a locked file would.</summary>
    public void StopDeleting(IReadOnlyList<string> images) => Files.FailDeletesUnder(Path.GetDirectoryName(images[0])!);

    /// <summary>Saves an open task at the top of the list. It holds at most one tag, as Tasks' Add requires.</summary>
    public TaskItem Add(string title, string description = "", params string[] tags) =>
        Tasks.Add(Tasks.StartDraft() with { Title = title, Description = description, Tags = tags });

    /// <summary>
    /// Saves an open task at the top of the list that holds these tags, which can be two or more as a task saved before the
    /// one-tag rule does: Tasks' Add refuses them, so the task's file is rewritten and the clone read again.
    /// </summary>
    public TaskItem AddOlder(string title, string description = "", params string[] tags)
    {
        var added = Add(title, description, [.. tags.Take(1)]);
        var path = Path.Combine(TaskFolder, added.Id + ".json");
        var file = JsonNode.Parse(Files.ReadFile(path))!.AsObject();
        file["tags"] = new JsonArray([.. tags.Select(tag => (JsonNode?)JsonValue.Create(tag))]);
        Files.WriteFile(path, Encoding.UTF8.GetBytes(file.ToJsonString()));
        Tasks.UseRepositoryFolder(Clone);
        return Tasks.All.Single(task => task.Id == added.Id);
    }

    /// <summary>The task Tasks keeps with the title.</summary>
    public TaskItem Stored(string title) => Tasks.All.Single(task => task.Title == title);

    /// <summary>Counts the saves Tasks makes from now on, one notice for each; read the count by calling what it returns.</summary>
    public Func<int> CountSaves()
    {
        var saves = 0;
        Tasks.Changed += (_, _) => saves++;
        return () => saves;
    }

    /// <summary>Saves a task, then resolves it.</summary>
    public TaskItem AddResolved(string title, string description = "", params string[] tags) =>
        Tasks.Resolve(Add(title, description, tags).Id);

    /// <summary>The titles of the open tasks in rank order, as Tasks keeps them.</summary>
    public IReadOnlyList<string> OpenTitlesInRankOrder() =>
        [.. Tasks.All.Where(task => task.Resolved is null).OrderBy(task => task.Rank, StringComparer.Ordinal).Select(task => task.Title)];

    /// <summary>The titles of the resolved tasks in rank order, as Tasks keeps them.</summary>
    public IReadOnlyList<string> ResolvedTitlesInRankOrder() =>
        [.. Tasks.All.Where(task => task.Resolved is not null).OrderBy(task => task.Rank, StringComparer.Ordinal).Select(task => task.Title)];
}
