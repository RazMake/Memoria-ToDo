using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>What the editor and its tag parts need to be made: tag colors right after Tasks, and no five-chip part left.</summary>
[Unit]
public class TagColorsRequiredTests
{
    private readonly EditorServices _services = new();

    [Fact]
    public void The_Add_window_without_tag_colors_is_rejected()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            _services.Tasks, null!, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            _services.ImageFiles, _services.Files, _services.CloseWindowKeys));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void The_editor_without_tag_colors_is_rejected()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => new TaskEditorControl(
            _services.Tasks, null!, Palette.Colors, _services.Theme, _services.Time, _services.Clipboard,
            _services.ImageFiles, _services.Files));

        Assert.Equal("tagColors", failure.ParamName);
    }

    [Fact]
    public void The_tag_editor_without_tag_colors_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagEditor(_services.Tasks, null!, _services.Theme));
    }

    [Fact]
    public void The_tag_badges_without_tag_colors_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagBadges(null!, _services.Theme));
    }

    [Fact]
    public void The_top_tags_without_tag_colors_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TopTags(_services.Tasks, null!, _services.Theme));
    }

    [Fact]
    public void The_tag_chips_part_is_gone()
    {
        Assert.Null(typeof(TaskEditorControl).Assembly.GetType("Memoria.ToDo.Desktop.TaskEditor.TagChips"));
    }
}
