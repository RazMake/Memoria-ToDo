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
        _model = new TagsModel(_services.Tasks, _services.TagColors);
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
    public void Adding_a_tag_keeps_the_order_the_tags_were_added_in()
    {
        _model.Add("b");
        _model.Add("a");

        Assert.Equal(["b", "a"], _model.Tags);
    }

    [Fact]
    public void Removing_a_tag_keeps_the_others_in_order()
    {
        _model.Tags = ["a", "b", "c"];

        _model.Remove("b");

        Assert.Equal(["a", "c"], _model.Tags);
    }

    [Fact]
    public void Backspace_on_an_empty_line_removes_the_last_tag_and_is_handled()
    {
        _model.Tags = ["a", "b"];

        Assert.True(_model.Backspace());

        Assert.Equal(["a"], _model.Tags);
    }

    [Fact]
    public void Backspace_on_an_empty_line_with_no_tags_is_handled_and_changes_nothing()
    {
        Assert.True(_model.Backspace());

        Assert.Empty(_model.Tags);
    }

    [Fact]
    public void Backspace_with_text_in_the_line_is_left_to_the_line()
    {
        _model.Tags = ["a"];
        _model.TypedText = "x";

        Assert.False(_model.Backspace());

        Assert.Equal(["a"], _model.Tags);
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
    public void A_lowercase_typed_tag_that_matches_a_tag_in_use_is_added_with_the_spelling_in_use()
    {
        _services.UseTag("Work");
        _model.TypedText = "work";

        _model.AddTypedText();

        Assert.Equal(["Work"], _model.Tags);
    }

    [Fact]
    public void The_plus_menu_lists_the_tags_in_use_with_the_spelling_in_use()
    {
        _services.UseTag("Work", 2);
        _services.UseTag("home");

        Assert.Equal(["Work", "home"], _model.MenuTags);
    }

    [Fact]
    public void Tags_the_task_already_has_keep_their_spelling()
    {
        _model.Tags = ["Work", "HOME"];

        Assert.Equal(["Work", "HOME"], _model.Tags);
    }

    [Fact]
    public void A_tag_chosen_from_the_plus_menu_keeps_the_spelling_in_use()
    {
        _services.UseTag("Work");

        _model.Add(_model.MenuTags[0]);

        Assert.Equal(["Work"], _model.Tags);
    }

    [Fact]
    public void A_model_without_Tasks_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagsModel(null!, _services.TagColors));
    }

    [Fact]
    public void A_model_without_tag_colors_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagsModel(_services.Tasks, null!));
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
