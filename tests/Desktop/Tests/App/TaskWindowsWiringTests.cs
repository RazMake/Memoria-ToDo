using System.IO;
using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The composition root gives Add and Task View the store's own tag colors, so both paint a tag in the color kept in the
/// store and not in a default. The wiring that picks the windows' parts is <c>TaskWindows.Create</c>, which Program calls;
/// it needs no tray, shortcut or real browser. The real parts of the windows are in use: only the file system, theme,
/// clipboard, file chooser, browser and sync are fakes.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class TaskWindowsWiringTests : IDisposable
{
    private readonly OpenWindows _windows = new();
    private readonly FakeFileSystem _files = new();
    private readonly ITaskStore _tasks;

    public TaskWindowsWiringTests()
    {
        _tasks = TaskStore.Create(_files, new FakeImageEncoder(), new FakeTimeProvider(), Path.GetFullPath("device-folder"));
        _tasks.UseRepositoryFolder("clone");
    }

    private (IAddWindow Add, IViewTasksWindow ViewTasks) Wire() => TaskWindows.Create(
        _tasks, new FakeTheme(), new FakeTimeProvider(), new FakeClipboard(), new FakeImageFileChooser(), _files,
        () => new FakeDescriptionWebView(), new FakeDefaultBrowser(), new FakeSyncService(), new Platform.CloseWindowKeys(),
        new FakeUpdateStatus(), new Platform.SaveKeys());

    public void Dispose() => _windows.Dispose();

    [WpfFact]
    public void Task_View_paints_a_tag_in_the_color_the_store_keeps()
    {
        _tasks.Add(_tasks.StartDraft() with { Title = "Buy milk", Tags = ["home"] });
        _tasks.TagColors.SetColor("home", "teal-3");
        var (_, viewTasks) = Wire();

        viewTasks.Show();

        Assert.Equal(
            TagControls.Expected(TagColors.Find("teal-3")!, dark: false),
            TagControls.FillOf(_windows.Visible().Single(), "home"));
    }

    [WpfFact]
    public void Add_paints_a_tag_of_the_kept_draft_in_the_color_the_store_keeps()
    {
        _tasks.KeepDraft(_tasks.StartDraft() with { Title = "Buy milk", Tags = ["home"] });
        _tasks.TagColors.SetColor("home", "blue-1");
        var (add, _) = Wire();

        add.Show();

        Assert.Equal(
            TagControls.Expected(TagColors.Find("blue-1")!, dark: false),
            TagControls.PillFill(_windows.Visible().Single()));
        add.Close();
    }
}
