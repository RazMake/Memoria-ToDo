using System.IO;
using System.Text;
using System.Text.Json;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The ports an editor test builds its windows on: the shared fakes, and a real Tasks module over the fake files.
/// Shared test setup, not a fake of a port.
/// </summary>
internal sealed class EditorServices
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "editor-tests");

    public FakeFileSystem Files { get; } = new();

    public FakeImageEncoder Encoder { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public FakeTheme Theme { get; } = new();

    public FakeTagColors TagColors { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeUpdateStatus Updates { get; } = new();

    public FakeImageFileChooser ImageFiles { get; } = new();

    public string Clone { get; } = Path.Combine(Root, "clone");

    public string Device { get; } = Path.Combine(Root, "device");

    public ITasks Tasks { get; }

    public EditorServices() => Tasks = TaskStore.Create(Files, Encoder, Time, Device);

    /// <summary>Gives Tasks its repository folder, as Configure's choice does.</summary>
    public void UseRepository() => Tasks.UseRepositoryFolder(Clone);

    /// <summary>The folder Tasks writes task files to; failing writes under it makes every save fail.</summary>
    public string TaskFolder => Path.Combine(Clone, "tasks");

    /// <summary>The file that holds this device's kept draft.</summary>
    public string DraftFile => Path.Combine(Device, "draft.json");

    /// <summary>Leaves a draft file Tasks can't read.</summary>
    public void KeepUnreadableDraft() => Files.WriteFile(DraftFile, "not a draft"u8);

    /// <summary>Saves a task with the tags, so they count as in use. Needs <see cref="UseRepository"/> first.</summary>
    public void AddTask(params string[] tags) =>
        Tasks.Add(Tasks.StartDraft() with { Title = $"Task {Tasks.All.Count + 1}", Tags = tags });

    /// <summary>Saves an open task with the title, at the top of the list. Needs <see cref="UseRepository"/> first.</summary>
    public TaskItem AddTaskTitled(string title) => Tasks.Add(Tasks.StartDraft() with { Title = title });

    /// <summary>Puts a resolved task with the title in the clone, as another device's sync would, and has Tasks read the clone again.</summary>
    public void AddResolvedTask(string title, string rank = "a0")
    {
        var id = $"01900000-0000-7000-8000-{Tasks.All.Count + 1:D12}";
        var quoted = JsonSerializer.Serialize(title);
        Files.WriteFile(Path.Combine(TaskFolder, id + ".json"), Encoding.UTF8.GetBytes(
            $$"""
            {
              "id": "{{id}}", "title": {{quoted}}, "description": "", "rank": "{{rank}}", "tags": [],
              "created": "2026-01-01T00:00:00.000Z", "updated": "2026-01-01T00:00:00.000Z",
              "resolved": "2026-01-02T00:00:00.000Z", "version": 1
            }
            """));
        UseRepository();
    }

    /// <summary>Saves <paramref name="count"/> tasks that each have the tag.</summary>
    public void UseTag(string tag, int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            AddTask(tag);
        }
    }

    /// <summary>Like <see cref="UseTag"/>, but from a worker thread, so Tasks raises Changed there as a background sync does.</summary>
    public Task UseTagOnWorkerThread(string tag, int count = 1) => Task.Run(() => UseTag(tag, count));

    /// <summary>The Close window keys the Add window is attached to; a test sets them as Configure does.</summary>
    public CloseWindowKeys CloseWindowKeys { get; } = new();

    public IAddWindow CreateAddWindow() => AddWindow.Create(Tasks, TagColors, Palette.Colors, Theme, Time, Clipboard, ImageFiles, Files, CloseWindowKeys, Updates);

    public TaskEditorControl CreateEditor() => new(Tasks, TagColors, Palette.Colors, Theme, Time, Clipboard, ImageFiles, Files);

    /// <summary>The tag control over this Tasks, these tag colors, and this theme.</summary>
    public TagEditor CreateTagEditor() => new(Tasks, TagColors, Theme);

    public TagBadges CreateTagBadges() => new(TagColors, Theme);

    public TopTags CreateTopTags() => new(Tasks, TagColors, Theme);

    /// <summary>Puts an image file where the user "chooses" or "drops" it, and returns its path. The fake image tools read its bytes.</summary>
    public string PutImageFile(string name, byte[] image)
    {
        var path = Path.Combine(Root, "pictures", name);
        Files.WriteFile(path, image);
        return path;
    }

    /// <summary>Where this device keeps the draft's image that a description links to, as <c>images/&lt;task-id&gt;/&lt;file&gt;</c>.</summary>
    public string DraftImagePath(string link) => Path.Combine(Device, link.Replace('/', Path.DirectorySeparatorChar));
}
