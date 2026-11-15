using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Adding and removing tags with the Custom box and its picked color: when the color is kept, what is reported, and what
/// is left alone when keeping the color fails.
/// </summary>
[Unit]
public class TagsModelCustomTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;
    private readonly List<string> _added = [];
    private readonly List<string> _removed = [];

    public TagsModelCustomTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors);
        _model.TagAdded += _added.Add;
        _model.TagRemoved += _removed.Add;
    }

    [Fact]
    public void Custom_starts_closed_and_opens_and_closes_on_request()
    {
        Assert.False(_model.IsCustomOpen);

        _model.OpenCustom();
        Assert.True(_model.IsCustomOpen);

        Assert.True(_model.CloseCustom());
        Assert.False(_model.IsCustomOpen);
    }

    [Fact]
    public void Adding_a_menu_tag_while_Custom_is_open_adds_it_and_leaves_Custom_as_it_was()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        _model.Add("home");

        Assert.Equal(["home"], _model.Tags);
        Assert.Equal(["home"], _added);
        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
        Assert.Equal("red-1", _model.PickedColor);
    }

    [Fact]
    public void Closing_Custom_when_it_is_closed_says_so_and_changes_nothing()
    {
        Assert.False(_model.CloseCustom());
    }

    [Fact]
    public void Closing_Custom_drops_the_typed_text_the_picked_color_and_the_message()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";
        _model.AddTypedText();

        _model.CloseCustom();

        Assert.Equal("", _model.TypedText);
        Assert.Null(_model.PickedColor);
        Assert.Equal("", _model.ColorMessage);
    }

    [Fact]
    public void Typing_a_tag_in_use_shows_its_kept_color_as_picked_ignoring_case()
    {
        _services.UseTag("home");
        _services.TagColors.Seed("home", "teal-2");
        _model.OpenCustom();

        _model.TypedText = "HOME";

        Assert.Equal("teal-2", _model.PickedColor);
    }

    [Fact]
    public void Typing_a_tag_in_use_with_no_kept_color_picks_none()
    {
        _services.UseTag("home");
        _model.OpenCustom();

        _model.TypedText = "home";

        Assert.Null(_model.PickedColor);
    }

    [Fact]
    public void Typing_a_tag_in_use_with_no_kept_color_clears_a_color_picked_before()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.PickedColor = "blue-3";

        _model.TypedText = "home";

        Assert.Null(_model.PickedColor);
    }

    [Fact]
    public void Typing_a_new_tag_leaves_the_picked_color_alone()
    {
        _model.OpenCustom();
        _model.PickedColor = "blue-3";

        _model.TypedText = "errand";

        Assert.Equal("blue-3", _model.PickedColor);
    }

    [Fact]
    public void Adding_with_a_picked_color_keeps_it_for_the_tag_before_TagAdded_is_raised()
    {
        var keptWhenRaised = new List<(string Tag, string Color)>();
        _model.TagAdded += _ => keptWhenRaised.AddRange(_services.TagColors.Kept);
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "purple-2";

        var result = _model.AddTypedText();

        Assert.True(result);
        Assert.Equal([("errand", "purple-2")], keptWhenRaised);
        Assert.Equal(["errand"], _added);
        Assert.Equal(["errand"], _model.Tags);
    }

    [Fact]
    public void Adding_keeps_the_color_for_the_tag_as_Tasks_spells_it()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.TypedText = "HOME";
        _model.PickedColor = "red-1";

        _model.AddTypedText();

        Assert.Equal([("home", "red-1")], _services.TagColors.Kept);
        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void Adding_hides_Custom_and_clears_the_typed_text_and_the_picked_color()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        _model.AddTypedText();

        Assert.False(_model.IsCustomOpen);
        Assert.Equal("", _model.TypedText);
        Assert.Null(_model.PickedColor);
    }

    [Fact]
    public void Enter_adds_the_typed_text_with_the_picked_color()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        _model.AddChosenOrTyped();

        Assert.Equal(["errand"], _model.Tags);
        Assert.Equal([("errand", "red-1")], _services.TagColors.Kept);
    }

    [Fact]
    public void A_tag_added_with_no_color_picked_writes_no_color()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        Assert.True(_model.AddTypedText());

        Assert.Equal(["errand"], _model.Tags);
        Assert.Empty(_services.TagColors.Kept);
    }

    [Fact]
    public void Adding_a_tag_the_task_already_has_raises_nothing_and_changes_no_tags()
    {
        _model.Tags = ["Home"];
        _model.OpenCustom();
        _model.TypedText = "home";

        Assert.True(_model.AddTypedText());

        Assert.Empty(_added);
        Assert.Equal(["Home"], _model.Tags);
    }

    [Fact]
    public void Adding_a_tag_the_task_already_has_still_keeps_a_new_color_for_it()
    {
        _model.Tags = ["Home"];
        _model.OpenCustom();
        _model.TypedText = "home";
        _model.PickedColor = "green-3";

        _model.AddTypedText();

        Assert.Empty(_added);
        Assert.Equal("green-3", _services.TagColors.ColorOf("Home"));
    }

    [Fact]
    public void Adding_nothing_typed_adds_no_tag_and_keeps_no_color_and_says_it_worked()
    {
        _model.PickedColor = "red-1";

        Assert.True(_model.AddTypedText());

        Assert.Empty(_model.Tags);
        Assert.Empty(_services.TagColors.Kept);
    }

    [Fact]
    public void Without_a_repository_the_tag_isnt_added_and_the_message_points_to_Configure()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        var result = _model.AddTypedText();

        Assert.False(result);
        Assert.Empty(_model.Tags);
        Assert.Empty(_added);
        Assert.Contains("Configure", _model.ColorMessage);
    }

    [Fact]
    public void A_failed_add_keeps_Custom_open_with_the_text_and_the_picked_color()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        _model.AddTypedText();

        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
        Assert.Equal("red-1", _model.PickedColor);
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void A_color_that_cant_be_read_or_written_isnt_added_and_says_why(Type failure)
    {
        _services.TagColors.Failure = (Exception)Activator.CreateInstance(failure, "disk trouble")!;
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        var result = _model.AddTypedText();

        Assert.False(result);
        Assert.Empty(_model.Tags);
        Assert.Empty(_added);
        Assert.Contains("disk trouble", _model.ColorMessage);
        Assert.True(_model.IsCustomOpen);
    }

    [Fact]
    public void A_failure_that_isnt_a_known_one_is_not_swallowed()
    {
        _services.TagColors.Failure = new InvalidOperationException("a bug");
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";

        Assert.Throws<InvalidOperationException>(() => _model.AddTypedText());
    }

    [Fact]
    public void A_later_add_that_works_clears_the_message()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.PickedColor = "red-1";
        _model.AddTypedText();
        _services.TagColors.Failure = null;

        Assert.True(_model.AddTypedText());

        Assert.Equal("", _model.ColorMessage);
        Assert.Equal(["errand"], _model.Tags);
    }

    [Fact]
    public void Choosing_a_suggestion_in_Custom_keeps_the_picked_color_before_TagAdded_then_closes_Custom()
    {
        _services.UseTag("home");
        var keptWhenRaised = new List<(string Tag, string Color)>();
        _model.TagAdded += _ => keptWhenRaised.AddRange(_services.TagColors.Kept);
        _model.OpenCustom();
        _model.TypedText = "ho";
        _model.PickedColor = "red-1";

        _model.AddSuggestion(0);

        Assert.Equal([("home", "red-1")], keptWhenRaised);
        Assert.Equal(["home"], _added);
        Assert.Equal(["home"], _model.Tags);
        Assert.False(_model.IsCustomOpen);
        Assert.Equal("", _model.TypedText);
        Assert.Null(_model.PickedColor);
    }

    [Fact]
    public void Down_then_Enter_in_Custom_accepts_the_suggestion_with_the_picked_color_and_closes_Custom()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.TypedText = "ho";
        _model.PickedColor = "red-1";

        _model.MoveDown();
        _model.AddChosenOrTyped();

        Assert.Equal(["home"], _model.Tags);
        Assert.Equal([("home", "red-1")], _services.TagColors.Kept);
        Assert.False(_model.IsCustomOpen);
    }

    [Fact]
    public void Choosing_a_suggestion_in_Custom_with_no_color_picked_writes_no_color()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.TypedText = "ho";

        _model.AddSuggestion(0);

        Assert.Equal(["home"], _model.Tags);
        Assert.Empty(_services.TagColors.Kept);
        Assert.False(_model.IsCustomOpen);
    }

    [Fact]
    public void A_suggestion_whose_color_cant_be_kept_isnt_added_and_Custom_stays_open_with_the_reason()
    {
        _services.UseTag("home");
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "ho";
        _model.PickedColor = "red-1";

        _model.AddSuggestion(0);

        Assert.Empty(_model.Tags);
        Assert.Empty(_added);
        Assert.True(_model.IsCustomOpen);
        Assert.Equal("ho", _model.TypedText);
        Assert.Equal("red-1", _model.PickedColor);
        Assert.Contains("Configure", _model.ColorMessage);
    }

    [Fact]
    public void Removing_a_tag_raises_TagRemoved_after_Tags_no_longer_holds_it()
    {
        _model.Tags = ["a", "b"];
        var tagsThen = new List<IReadOnlyList<string>>();
        _model.TagRemoved += _ => tagsThen.Add(_model.Tags);

        _model.Remove("a");

        Assert.Equal(["a"], _removed);
        Assert.Equal(["b"], Assert.Single(tagsThen));
    }

    [Fact]
    public void Backspace_in_the_empty_box_removes_the_last_tag_and_reports_it()
    {
        _model.Tags = ["a", "b"];

        _model.Backspace();

        Assert.Equal(["b"], _removed);
    }

    [Fact]
    public void Setting_the_tags_raises_neither_TagAdded_nor_TagRemoved()
    {
        _model.Tags = ["a"];
        _model.Tags = ["b"];

        Assert.Empty(_added);
        Assert.Empty(_removed);
    }
}
