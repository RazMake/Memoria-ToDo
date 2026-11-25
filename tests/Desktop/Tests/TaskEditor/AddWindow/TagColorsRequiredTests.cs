using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

using AddWindow = Memoria.ToDo.Desktop.TaskEditor.AddWindow;

/// <summary>What the editor and its tag parts need to be made: tag colors right after Tasks, and no five-chip part left.</summary>
[Unit]
public class TagColorsRequiredTests
{
    private readonly EditorServices _services = new();

    [Fact]
    public void The_Add_window_without_tag_colors_is_rejected()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            _services.Tasks, null!, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            _services.ImageFiles, _services.Files, _services.CloseWindowKeys, _services.Updates, _services.SaveKeys, _services.Pointer, Logger.None));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void The_editor_without_tag_colors_is_rejected()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new TaskEditorControl(
            _services.Tasks, null!, _services.Contacts, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            _services.ImageFiles, _services.Files, _services.SaveKeys, _services.Pointer, Serilog.Core.Logger.None));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void The_tag_editor_without_tag_colors_is_rejected()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new TagEditor(_services.Tasks, null!, _services.Theme, _services.Pointer, Logger.None));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void Creating_the_TagEditor_without_a_logger_is_refused()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new TagEditor(_services.Tasks, _services.TagColors, _services.Theme, _services.Pointer, null!));

        Assert.Equal("logger", failure.ParamName);
    }

    [Fact]
    public void Creating_the_TagEditor_without_a_pointer_is_refused()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new TagEditor(_services.Tasks, _services.TagColors, _services.Theme, null!, Logger.None));

        Assert.Equal("pointer", failure.ParamName);
    }

    [Fact]
    public void The_tag_badges_without_tag_colors_are_rejected()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new TagBadges(null!, _services.Theme));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void The_tag_chips_part_is_gone()
    {
        Assert.Null(typeof(TaskEditorControl).Assembly.GetType("Memoria.ToDo.Desktop.TaskEditor.TagChips"));
    }
}
