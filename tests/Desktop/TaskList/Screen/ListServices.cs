using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The ports a View Tasks test builds its window on: the shared fakes, and a real Tasks module over the fake files.
/// Shared test setup, not a fake of a port.
/// </summary>
internal sealed class ListServices
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "task-list-tests");

    private readonly List<FakeDescriptionWebView> webViews = [];

    public FakeFileSystem Files { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public FakeTheme Theme { get; } = new();

    public FakeDefaultBrowser Browser { get; } = new();

    public FakeClipboard Clipboard { get; } = new();
    public FakeSyncService Sync { get; } = new();

    public ITasks Tasks { get; }

    /// <summary>Every file the fake file system holds, task files and images alike.</summary>
    public IReadOnlyList<string> AllFiles => Files.AllFilesUnder(Root);

    /// <summary>The web views View Tasks made, oldest first.</summary>
    public IReadOnlyList<FakeDescriptionWebView> WebViews => webViews;

    public string Clone { get; } = Path.Combine(Root, "clone");

    /// <summary>The folder Tasks writes task files to; failing writes under it makes every save fail.</summary>
    public string TaskFolder => Path.Combine(Clone, "tasks");

    public ListServices()
    {
        Tasks = TaskStore.Create(Files, new FakeImageEncoder(), Time, Path.Combine(Root, "device"));
        Tasks.UseRepositoryFolder(Clone);
    }

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

    /// <summary>Opens View Tasks on these services. It shows only after <c>Show</c>. Each web view it asks for is kept in <see cref="WebViews"/>.</summary>
    public IViewTasksWindow CreateWindow() => ViewTasksWindow.Create(
        Tasks, Palette.Colors, Theme, Time, Clipboard, new FakeImageFileChooser(), Files,
        () =>
        {
            var view = new FakeDescriptionWebView();
            webViews.Add(view);
            return view;
        },
        Browser, Sync);
}
