using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>What <see cref="AddWindow.Create"/> and the editors require: every part, the platform's Close window keys and Save keys included.</summary>
[Unit]
public class AddWindowCreateTests
{
    [Fact]
    public void Rejects_missing_Close_window_keys()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, null!, services.Updates, services.SaveKeys, services.Pointer, Logger.None));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }

    [Fact]
    public void Rejects_a_missing_update_status()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, services.CloseWindowKeys, null!, services.SaveKeys, services.Pointer, Logger.None));

        Assert.Equal("updates", failure.ParamName);
    }

    [Fact]
    public void Rejects_missing_Save_keys()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, services.CloseWindowKeys, services.Updates, null!, services.Pointer, Logger.None));

        Assert.Equal("saveKeys", failure.ParamName);
    }

    [Fact]
    public void Rejects_a_missing_logger()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, services.CloseWindowKeys, services.Updates, services.SaveKeys, services.Pointer, null!));

        Assert.Equal("logger", failure.ParamName);
    }

    [Fact]
    public void The_editor_rejects_missing_Save_keys()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new TaskEditorControl(
            services.Tasks, services.TagColors, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, null!, services.Pointer, Serilog.Core.Logger.None));

        Assert.Equal("saveKeys", failure.ParamName);
    }

    [Fact]
    public void The_Add_window_rejects_missing_contacts()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, null!, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, services.CloseWindowKeys, services.Updates, services.SaveKeys, services.Pointer, Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void The_editor_rejects_missing_contacts()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new TaskEditorControl(
            services.Tasks, services.TagColors, null!, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, services.SaveKeys, services.Pointer, Serilog.Core.Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void The_Title_editor_rejects_missing_contacts()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new TitleEditor(
            services.Tasks, null!, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.SaveKeys, services.Pointer, Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void The_Description_editor_rejects_missing_contacts()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new DescriptionEditor(
            services.Tasks, null!, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles, services.Files,
            services.SaveKeys, services.Pointer, Logger.None));

        Assert.Equal("contacts", failure.ParamName);
    }

    [Fact]
    public void The_Title_editor_rejects_missing_Save_keys()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new TitleEditor(
            services.Tasks, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, null!, services.Pointer, Logger.None));

        Assert.Equal("saveKeys", failure.ParamName);
    }

    [Fact]
    public void The_Description_editor_rejects_missing_Save_keys()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new DescriptionEditor(
            services.Tasks, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles, services.Files, null!, services.Pointer, Logger.None));

        Assert.Equal("saveKeys", failure.ParamName);
    }

    [Fact]
    public void Creating_the_TitleEditor_without_a_logger_is_refused()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new TitleEditor(
            services.Tasks, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.SaveKeys, services.Pointer, null!));

        Assert.Equal("logger", failure.ParamName);
    }

    [Fact]
    public void Creating_the_DescriptionEditor_without_a_logger_is_refused()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => new DescriptionEditor(
            services.Tasks, services.Contacts, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles, services.Files,
            services.SaveKeys, services.Pointer, null!));

        Assert.Equal("logger", failure.ParamName);
    }
}
