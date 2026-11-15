using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>What Task View refuses when it is created, before it makes a window. No window is made, so these run with no screen.</summary>
[Unit]
public class ViewTasksWindowCreateTests
{
    private readonly ListServices _services = new();

    [Fact]
    public void Create_rejects_missing_tag_colors_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, null!, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_update_status_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, null!, _services.SaveKeys));

        Assert.Equal("updates", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_missing_Close_window_keys_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, null!, _services.Updates, _services.SaveKeys));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_missing_Save_keys_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, null!));

        Assert.Equal("saveKeys", failure.ParamName);
    }
}