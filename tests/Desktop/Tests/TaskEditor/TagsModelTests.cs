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
        _model = new TagsModel(_services.Tasks);
    }

    [Fact]
    public void Typing_lists_the_tags_in_use_that_start_with_it_most_used_first()
    {
        _services.UseTag("hobby");
        _services.UseTag("home", 2);
        _services.UseTag("work", 3);

        _model.TypedText = "ho";

        Assert.Equal(["home", "hobby"], _model.Suggestions.Items);
    }

    [Fact]
    public void Suggestions_narrow_as_more_is_typed()
    {
        _services.UseTag("home", 2);
        _services.UseTag("hobby");
        _model.TypedText = "ho";

        _model.TypedText = "hom";

        Assert.Equal(["home"], _model.Suggestions.Items);
    }

    [Fact]
    public void A_tag_the_task_already_has_is_not_suggested()
    {
        _services.UseTag("home", 2);
        _services.UseTag("hobby");
        _model.Tags = ["home"];

        _model.TypedText = "ho";

        Assert.Equal(["hobby"], _model.Suggestions.Items);
    }

    [Fact]
    public void Suggestions_close_when_no_tag_matches_or_the_typed_text_is_erased()
    {
        _services.UseTag("home");
        _model.TypedText = "ho";
        Assert.True(_model.Suggestions.IsOpen);

        _model.TypedText = "zzz";
        Assert.False(_model.Suggestions.IsOpen);

        _model.TypedText = "ho";
        _model.TypedText = "";
        Assert.False(_model.Suggestions.IsOpen);
    }

    [Fact]
    public void Suggestions_are_refreshed_on_request_after_the_tags_in_use_change()
    {
        _services.UseTag("home");
        _model.TypedText = "ho";

        _services.UseTag("hobby", 2);
        _model.Refresh();

        Assert.Equal(["hobby", "home"], _model.Suggestions.Items);
    }

    [Fact]
    public void Enter_adds_the_suggestion_that_Down_moved_to()
    {
        _services.UseTag("home", 2);
        _services.UseTag("hobby");
        _model.TypedText = "ho";
        _model.MoveDown();
        _model.MoveDown();

        _model.AddChosenOrTyped();

        Assert.Equal(["hobby"], _model.Tags);
        Assert.Equal("", _model.TypedText);
    }

    [Fact]
    public void Enter_with_no_suggestion_chosen_adds_the_typed_text_spelled_as_the_tag_in_use()
    {
        _services.UseTag("home");
        _model.TypedText = "HOME";

        _model.AddChosenOrTyped();

        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void Enter_on_a_blank_line_adds_no_tag()
    {
        _model.TypedText = "   ";

        _model.AddChosenOrTyped();

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
    public void Clicking_a_suggestion_adds_that_tag()
    {
        _services.UseTag("home", 2);
        _services.UseTag("hobby");
        _model.TypedText = "ho";

        _model.AddSuggestion(1);

        Assert.Equal(["hobby"], _model.Tags);
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
    public void A_model_without_Tasks_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TagsModel(null!));
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

    [Fact]
    public void Closing_the_suggestions_says_whether_any_were_open()
    {
        _services.UseTag("home");
        Assert.False(_model.CloseSuggestions());

        _model.TypedText = "ho";

        Assert.True(_model.CloseSuggestions());
        Assert.False(_model.Suggestions.IsOpen);
    }

    [Fact]
    public void Moving_down_with_no_suggestions_open_opens_nothing()
    {
        _model.MoveDown();

        Assert.False(_model.Suggestions.IsOpen);
    }
}
