using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Serilog.Core;
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
            _services.Tasks, null!, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_tag_order_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, null!, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("tagOrder", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_missing_contacts_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, _services.TagOrder, null!, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_update_status_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, null!, _services.SaveKeys, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("updates", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_missing_Close_window_keys_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, null!, _services.Updates, _services.SaveKeys, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_missing_Save_keys_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, null!, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("saveKeys", failure.ParamName);
    }

    [Fact]
    public void Create_rejects_a_missing_drag_pointer_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates,             _services.SaveKeys, null!,
                        _services.KeepAwake, _services.DeviceFolder, Logger.None));

        Assert.Equal("pointer", failure.ParamName);
    }

                [Fact]
                public void Create_rejects_a_missing_Keep_awake_status_by_name()
                {
                    var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
                        _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
                        new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
                        _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
                        null!, _services.DeviceFolder, Logger.None));

                    Assert.Equal("keepAwake", failure.ParamName);
                }

                [Fact]
                public void Create_rejects_a_missing_device_folder_by_name()
                {
                    var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
                        _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
                        new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
                        _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
                        _services.KeepAwake, null!, Logger.None));

                    Assert.Equal("deviceFolder", failure.ParamName);
                }

                [Fact]
                public void Create_rejects_a_device_folder_that_is_not_a_full_path() =>
                    Assert.ThrowsAny<ArgumentException>(() => ViewTasksWindow.Create(
                        _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
                        new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
                        _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
                        _services.KeepAwake, @"device\folder", Logger.None));

    [Fact]
    public void Create_rejects_a_missing_logger_by_name()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            _services.Tasks, _services.TagColors, _services.TagOrder, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            new FakeImageFileChooser(), _services.Files, () => new FakeDescriptionWebView(),
            _services.Browser, _services.Sync, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer,
            _services.KeepAwake, _services.DeviceFolder, null!));

        Assert.Equal("logger", failure.ParamName);
    }
}