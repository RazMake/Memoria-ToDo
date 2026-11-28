using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class TagsModelTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;

    public TagsModelTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors, Serilog.Core.Logger.None);
    }

    [Fact]
    public void Adding_the_typed_text_spells_it_as_the_tag_in_use()
    {
        _services.UseTag("home");
        _model.TypedText = "HOME";

        _model.AddTypedText();

        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void Adding_a_blank_typed_text_adds_no_tag()
    {
        _model.TypedText = "   ";

        _model.AddTypedText();

        Assert.Empty(_model.Tags);
    }


    [Fact]
    public void Adding_the_typed_text_adds_a_new_tag_and_clears_the_line()
    {
        _model.TypedText = "errands";

        _model.AddTypedText();

        Assert.Equal(["errands"], _model.Tags);
        Assert.Equal("", _model.TypedText);
    }

    [Fact]
    public void Setting_the_tags_clears_the_typed_text()
    {
        _model.TypedText = "half";

        _model.Tags = ["a"];

        Assert.Equal("", _model.TypedText);
    }

    [Fact]
    public void Setting_no_tags_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _model.Tags = null!);
    }

    [Fact]
    public void A_typed_tag_is_added_normalized_so_it_matches_a_tag_in_use_however_it_is_spelled()
    {
        _services.UseTag("My Work");
        _model.TypedText = "MY  work";

        _model.AddTypedText();

        Assert.Equal(["my-work"], _model.Tags);
    }

    [Fact]
    public void The_plus_menu_lists_the_tags_in_use_normalized()
    {
        _services.UseTag("My Work", 2);
        _services.UseTag("home");

        Assert.Equal(["my-work", "home"], _model.MenuTags);
    }

    [Fact]
    public void A_tag_chosen_from_the_plus_menu_is_the_normalized_tag_in_use()
    {
        _services.UseTag("My Work");

        _model.Add(_model.MenuTags[0]);

        Assert.Equal(["my-work"], _model.Tags);
    }

    [Fact]
    public void A_model_without_Tasks_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagsModel(null!, _services.TagColors, Serilog.Core.Logger.None));
    }

    [Fact]
    public void A_model_without_tag_colors_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagsModel(_services.Tasks, null!, Serilog.Core.Logger.None));
    }

    [Fact]
    public void Changed_is_raised_when_the_tags_or_the_typed_text_are_set()
    {
        var raised = 0;
        _model.Changed += () => raised++;

        _model.TypedText = "ho";
        _model.Tags = ["a"];

        Assert.Equal(2, raised);
    }
}
