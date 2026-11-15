using System.IO;
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
        Tasks = TaskStore.Create(Files, new FakeImageEncoder(), Time, Path.Combine(Root, "device"));
        Tasks.UseRepositoryFolder(Clone);
    }

    /// <summary>The tag colors the window shows tags in.</summary>
    public FakeTagColors TagColors { get; } = new();

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

    /// <summary>Saves an open task at the top of the list.</summary>
    public TaskItem Add(string title, string description = "", params string[] tags) =>
        Tasks.Add(Tasks.StartDraft() with { Title = title, Description = description, Tags = tags });

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
