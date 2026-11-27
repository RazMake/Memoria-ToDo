using System.IO;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Custom, the New tag box and its colors: when the colors show, which color is marked, how Enter and a click on a color
/// add the tag, and what a color that can't be kept leaves alone.
/// </summary>
[Unit]
public class TagsModelCustomTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;
    private readonly List<IReadOnlyList<string>> _raised = [];

    public TagsModelCustomTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors, Serilog.Core.Logger.None);
        _model.TagsChanged += _raised.Add;
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
    public void Closing_Custom_when_it_is_closed_says_so_and_changes_nothing()
    {
        Assert.False(_model.CloseCustom());
    }

    [Fact]
    public void Closing_Custom_drops_the_typed_text_and_the_message()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.AddTypedTextInColor("red-1");

        _model.CloseCustom();

        Assert.Equal("", _model.TypedText);
        Assert.Equal("", _model.ColorMessage);
    }

    [Fact]
    public void Adding_a_menu_tag_while_Custom_is_open_adds_it_and_leaves_Custom_as_it_was()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.Add("home");

        Assert.Equal(["home"], _model.Tags);
        Assert.Equal([["home"]], _raised);
        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
    }

    [Fact]
    public void Replacing_the_tags_leaves_Custom_open_with_its_text()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.ReplaceTags(["x"]);

        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
    }

    // The colors.

    [Fact]
    public void The_colors_show_only_while_the_box_holds_text()
    {
        _model.OpenCustom();
        Assert.False(_model.ColorsShown);

        _model.TypedText = "e";
        Assert.True(_model.ColorsShown);

        _model.TypedText = "";
        Assert.False(_model.ColorsShown);
    }

    [Fact]
    public void The_colors_hide_when_Custom_closes()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.CloseCustom();

        Assert.False(_model.ColorsShown);
    }

    [Fact]
    public void Marks_no_color_while_nothing_is_typed()
    {
        _model.OpenCustom();

        Assert.Null(_model.MarkedColor);
    }

    [Fact]
    public void Marks_the_default_color_of_a_new_tag()
    {
        _model.OpenCustom();

        _model.TypedText = "errand";

        Assert.Equal(TagColors.DefaultFor("errand").Name, _model.MarkedColor);
    }

    [Fact]
    public void Marks_the_kept_color_of_a_tag_in_use_ignoring_case()
    {
        _services.UseTag("home");
        _services.TagColors.Seed("home", "teal-2");
        _model.OpenCustom();

        _model.TypedText = "HOME";

        Assert.Equal("teal-2", _model.MarkedColor);
    }

    [Fact]
    public void Marks_the_default_color_of_a_tag_in_use_that_has_no_kept_color()
    {
        _services.UseTag("home");
        _model.OpenCustom();

        _model.TypedText = "home";

        Assert.Equal(TagColors.DefaultFor("home").Name, _model.MarkedColor);
    }

    [Fact]
    public void The_mark_follows_the_text_as_it_changes()
    {
        _services.TagColors.Seed("home", "teal-2");
        _model.OpenCustom();
        _model.TypedText = "home";

        _model.TypedText = "errand";

        Assert.Equal(TagColors.DefaultFor("errand").Name, _model.MarkedColor);
    }

    [Fact]
    public void The_mark_follows_the_kept_color_when_it_changes_after_the_text_was_typed()
    {
        _services.UseTag("home");
        _services.TagColors.Seed("home", "teal-2");
        _model.OpenCustom();
        _model.TypedText = "home";

        _services.TagColors.SetColor("home", "pink-1");

        Assert.Equal("pink-1", _model.MarkedColor);
    }

    // Enter.

    [Fact]
    public void Enter_adds_the_typed_tag_hides_Custom_and_empties_the_box()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        Assert.True(_model.AddTypedText());

        Assert.Equal(["errand"], _model.Tags);
        Assert.Equal([["errand"]], _raised);
        Assert.False(_model.IsCustomOpen);
        Assert.Equal("", _model.TypedText);
    }

    [Fact]
    public void Enter_keeps_no_color_for_a_new_tag()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.AddTypedText();

        Assert.Empty(_services.TagColors.Kept);
    }

    [Fact]
    public void Enter_keeps_no_color_for_a_tag_that_has_one()
    {
        _services.UseTag("home");
        _services.TagColors.Seed("home", "teal-2");
        _model.OpenCustom();
        _model.TypedText = "home";

        _model.AddTypedText();

        Assert.Empty(_services.TagColors.Kept);
        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void Enter_adds_the_tag_even_when_colors_cant_be_kept_since_it_writes_none()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";

        Assert.True(_model.AddTypedText());

        Assert.Equal(["errand"], _model.Tags);
        Assert.Equal("", _model.ColorMessage);
    }

    [Fact]
    public void Enter_spells_the_tag_as_the_tag_in_use()
    {
        _services.UseTag("work");
        _model.OpenCustom();
        _model.TypedText = "work";

        _model.AddTypedText();

        Assert.Equal(["work"], _model.Tags);
    }

    [Fact]
    public void Enter_with_a_tag_the_task_already_has_raises_nothing_changes_no_tags_and_hides_Custom()
    {
        _model.Tags = ["home"];
        _model.OpenCustom();
        _model.TypedText = "home";

        Assert.True(_model.AddTypedText());

        Assert.Empty(_raised);
        Assert.Equal(["home"], _model.Tags);
        Assert.False(_model.IsCustomOpen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Choosing_the_tag_the_task_already_has_in_Custom_tells_observers_that_Custom_closed_though_it_raises_nothing(bool byColor)
    {
        _model.Tags = ["home"];
        _model.OpenCustom();
        _model.TypedText = "home";
        var customOpenWhenTold = new List<bool>();
        _model.Changed += () => customOpenWhenTold.Add(_model.IsCustomOpen);

        Assert.True(byColor ? _model.AddTypedTextInColor("green-3") : _model.AddTypedText());

        Assert.NotEmpty(customOpenWhenTold);
        Assert.All(customOpenWhenTold, open => Assert.False(open));
        Assert.Empty(_raised);
        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void Enter_with_nothing_typed_adds_no_tag_and_says_it_worked()
    {
        _model.OpenCustom();

        Assert.True(_model.AddTypedText());

        Assert.Empty(_model.Tags);
        Assert.Empty(_services.TagColors.Kept);
    }

    // A click on a color, or Space or Enter on it.

    [Fact]
    public void A_color_is_kept_for_the_tag_before_TagsChanged_is_raised()
    {
        var keptWhenRaised = new List<(string Tag, string Color)>();
        _model.TagsChanged += _ => keptWhenRaised.AddRange(_services.TagColors.Kept);
        _model.OpenCustom();
        _model.TypedText = "errand";

        var result = _model.AddTypedTextInColor("purple-2");

        Assert.True(result);
        Assert.Equal([("errand", "purple-2")], keptWhenRaised);
        Assert.Equal([["errand"]], _raised);
        Assert.Equal(["errand"], _model.Tags);
    }

    [Fact]
    public void A_color_is_kept_for_the_tag_as_Tasks_spells_it()
    {
        _services.UseTag("home");
        _model.OpenCustom();
        _model.TypedText = "HOME";

        _model.AddTypedTextInColor("red-1");

        Assert.Equal([("home", "red-1")], _services.TagColors.Kept);
        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void A_color_even_the_marked_one_is_kept_when_clicked()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.AddTypedTextInColor(_model.MarkedColor!);

        Assert.Equal([("errand", TagColors.DefaultFor("errand").Name)], _services.TagColors.Kept);
    }

    [Fact]
    public void A_color_hides_Custom_and_empties_the_box()
    {
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.AddTypedTextInColor("red-1");

        Assert.False(_model.IsCustomOpen);
        Assert.Equal("", _model.TypedText);
        Assert.False(_model.ColorsShown);
    }

    [Fact]
    public void A_color_for_a_tag_the_task_already_has_is_kept_and_raises_nothing()
    {
        _model.Tags = ["home"];
        _model.OpenCustom();
        _model.TypedText = "home";

        _model.AddTypedTextInColor("green-3");

        Assert.Empty(_raised);
        Assert.Equal("green-3", _services.TagColors.ColorOf("home"));
        Assert.Equal(["home"], _model.Tags);
    }

    [Fact]
    public void A_color_with_nothing_typed_adds_no_tag_and_keeps_no_color()
    {
        Assert.True(_model.AddTypedTextInColor("red-1"));

        Assert.Empty(_model.Tags);
        Assert.Empty(_services.TagColors.Kept);
    }

    [Fact]
    public void Without_a_repository_the_tag_isnt_added_and_the_message_points_to_Configure()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";

        var result = _model.AddTypedTextInColor("red-1");

        Assert.False(result);
        Assert.Empty(_model.Tags);
        Assert.Empty(_raised);
        Assert.Contains("Configure", _model.ColorMessage);
    }

    [Fact]
    public void A_color_that_cant_be_kept_leaves_Custom_open_with_its_text_and_colors()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";

        _model.AddTypedTextInColor("red-1");

        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
        Assert.True(_model.ColorsShown);
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

        var result = _model.AddTypedTextInColor("red-1");

        Assert.False(result);
        Assert.Empty(_model.Tags);
        Assert.Empty(_raised);
        Assert.Contains("disk trouble", _model.ColorMessage);
        Assert.True(_model.IsCustomOpen);
    }

    [Fact]
    public void A_color_that_was_kept_before_a_notice_failed_still_adds_the_tag_and_says_so_where_a_failed_save_shows()
    {
        _services.TagColors.Failure = new NoticeFailedException<TaskItem>(null, [new IOException("Changed failed.")]);
        _model.OpenCustom();
        _model.TypedText = "errand";

        var result = _model.AddTypedTextInColor("red-1");

        Assert.True(result);
        Assert.Equal(["errand"], _model.Tags);
        Assert.Equal([["errand"]], _raised);
        Assert.Equal("Saved, but the app couldn't finish updating: Changed failed.", _model.ColorMessage);
    }

    private string SavedMessageLeftWithCustomClosed()
    {
        _services.TagColors.Failure = new NoticeFailedException<TaskItem>(null, [new IOException("Changed failed.")]);
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.AddTypedTextInColor("red-1");
        Assert.False(_model.IsCustomOpen);
        return _model.ColorMessage;
    }

    [Fact]
    public void Opening_Custom_clears_a_message_left_by_a_notice_failure()
    {
        Assert.NotEqual("", SavedMessageLeftWithCustomClosed());

        _model.OpenCustom();

        Assert.Equal("", _model.ColorMessage);
    }

    [Fact]
    public void Setting_the_tags_clears_a_message_left_by_a_notice_failure()
    {
        Assert.NotEqual("", SavedMessageLeftWithCustomClosed());

        _model.Tags = ["other"];

        Assert.Equal("", _model.ColorMessage);
    }

    [Fact]
    public void A_message_left_by_a_notice_failure_stays_while_Custom_is_closed()
    {
        var message = SavedMessageLeftWithCustomClosed();

        _model.CloseCustom();
        _model.Add("other");

        Assert.Equal(message, _model.ColorMessage);
    }

    [Fact]
    public void A_failure_that_isnt_a_known_one_is_not_swallowed()
    {
        _services.TagColors.Failure = new InvalidOperationException("a bug");
        _model.OpenCustom();
        _model.TypedText = "errand";

        Assert.Throws<InvalidOperationException>(() => _model.AddTypedTextInColor("red-1"));
    }

    [Fact]
    public void The_message_clears_when_the_text_changes()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.AddTypedTextInColor("red-1");

        _model.TypedText = "errands";

        Assert.Equal("", _model.ColorMessage);
    }

    [Fact]
    public void A_later_color_that_works_clears_the_message_and_adds_the_tag()
    {
        _services.TagColors.Failure = new NoRepositoryFolderException();
        _model.OpenCustom();
        _model.TypedText = "errand";
        _model.AddTypedTextInColor("red-1");
        _services.TagColors.Failure = null;

        Assert.True(_model.AddTypedTextInColor("red-1"));

        Assert.Equal("", _model.ColorMessage);
        Assert.Equal(["errand"], _model.Tags);
    }

    // The task reaches 15 tags while Custom is open, such as by a tag chosen from the menu, so what is typed can't be added.

    private void FillToFifteenWhileCustomHoldsText(string typed)
    {
        _model.Tags = [.. Enumerable.Range(1, 14).Select(number => $"tag-{number}")];
        _model.OpenCustom();
        _model.TypedText = typed;
        _model.Add("tag-15");
        _raised.Clear();
    }

    [Fact]
    public void Enter_when_the_task_reached_fifteen_tags_adds_nothing_keeps_Custom_and_its_text_and_says_why()
    {
        FillToFifteenWhileCustomHoldsText("errand");

        var result = _model.AddTypedText();

        Assert.False(result);
        Assert.Equal(15, _model.Tags.Count);
        Assert.DoesNotContain("errand", _model.Tags);
        Assert.Empty(_raised);
        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
        Assert.NotEqual("", _model.ColorMessage);
    }

    [Fact]
    public void A_color_when_the_task_reached_fifteen_tags_keeps_no_color_adds_nothing_keeps_Custom_and_its_text_and_says_why()
    {
        FillToFifteenWhileCustomHoldsText("errand");

        var result = _model.AddTypedTextInColor("red-1");

        Assert.False(result);
        Assert.Empty(_services.TagColors.Kept);
        Assert.Equal(15, _model.Tags.Count);
        Assert.Empty(_raised);
        Assert.True(_model.IsCustomOpen);
        Assert.Equal("errand", _model.TypedText);
        Assert.NotEqual("", _model.ColorMessage);
    }

    [Fact]
    public void The_message_about_fifteen_tags_clears_when_the_text_changes_and_when_Custom_closes()
    {
        FillToFifteenWhileCustomHoldsText("errand");
        _model.AddTypedText();

        _model.TypedText = "errands";
        Assert.Equal("", _model.ColorMessage);

        _model.AddTypedText();
        _model.CloseCustom();
        Assert.Equal("", _model.ColorMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_typed_tag_the_task_already_has_still_closes_Custom_at_fifteen_tags(bool byColor)
    {
        FillToFifteenWhileCustomHoldsText("TAG-3");

        Assert.True(byColor ? _model.AddTypedTextInColor("green-3") : _model.AddTypedText());

        Assert.False(_model.IsCustomOpen);
        Assert.Equal("", _model.ColorMessage);
        Assert.Empty(_raised);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_typed_tag_replaces_the_pills_tag_at_fifteen_tags_since_the_count_stays_the_same(bool byColor)
    {
        FillToFifteenWhileCustomHoldsText("errand");
        _model.Target = 0;

        Assert.True(byColor ? _model.AddTypedTextInColor("green-3") : _model.AddTypedText());

        Assert.Equal(15, _model.Tags.Count);
        Assert.Equal("errand", _model.Tags[0]);
        Assert.Single(_raised);
        Assert.False(_model.IsCustomOpen);
    }

    // Setting the tags raises nothing.

    [Fact]
    public void Setting_the_tags_raises_neither_TagsChanged_nor_TagsChanged()
    {
        _model.Tags = ["a"];
        _model.Tags = ["b"];

        Assert.Empty(_raised);
    }
}
