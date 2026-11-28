using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The task's ordered list of up to 15 tags: adding at the end, replacing one in place, removing one, the pill the menu was
/// opened from, and that TagsChanged is raised once per real change with the whole list.
/// </summary>
[Unit]
public class TagsModelTagListTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;
    private readonly List<(IReadOnlyList<string> Raised, IReadOnlyList<string> Held)> _raised = [];

    public TagsModelTagListTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors, Serilog.Core.Logger.None);
        _model.TagsChanged += raised => _raised.Add((raised, _model.Tags));
    }

    [Fact]
    public void Adding_tags_appends_them_in_order_and_raises_the_whole_list_each_time()
    {
        _model.Add("work");
        _model.Add("Home");

        Assert.Equal(["work", "home"], _model.Tags);
        Assert.Equal([["work"], ["work", "home"]], _raised.Select(r => r.Raised));
        Assert.Equal([["work"], ["work", "home"]], _raised.Select(r => r.Held));
    }

    [Fact]
    public void A_tag_the_task_has_is_not_added_twice_even_spelled_differently()
    {
        _model.Add("my-work");

        _model.Add("My Work");

        Assert.Equal(["my-work"], _model.Tags);
        Assert.Single(_raised);
    }

    [Fact]
    public void Nothing_is_added_at_fifteen_tags()
    {
        _model.Tags = [.. Enumerable.Range(1, 15).Select(number => $"tag-{number}")];

        _model.Add("one-more");

        Assert.Equal(15, _model.Tags.Count);
        Assert.Empty(_raised);
    }

    [Fact]
    public void Replacing_a_tag_puts_the_new_one_in_its_place_and_raises_the_whole_list()
    {
        _model.Tags = ["a", "b", "c"];

        _model.Replace(1, "Side Job");

        Assert.Equal(["a", "side-job", "c"], _model.Tags);
        Assert.Equal([["a", "side-job", "c"]], _raised.Select(r => r.Raised));
    }

    [Fact]
    public void Replacing_a_tag_with_one_the_task_already_has_changes_nothing()
    {
        _model.Tags = ["a", "b", "c"];

        _model.Replace(1, "c");
        _model.Replace(1, "b");

        Assert.Equal(["a", "b", "c"], _model.Tags);
        Assert.Empty(_raised);
    }

    [Fact]
    public void Removing_a_tag_drops_only_it_and_raises_the_whole_list()
    {
        _model.Tags = ["a", "b", "c"];

        _model.RemoveAt(0);

        Assert.Equal(["b", "c"], _model.Tags);
        Assert.Equal([["b", "c"]], _raised.Select(r => r.Raised));
    }

    [Fact]
    public void Removing_the_last_tag_raises_an_empty_list()
    {
        _model.Tags = ["a"];

        _model.RemoveAt(0);

        Assert.Empty(_model.Tags);
        Assert.Equal([[]], _raised.Select(r => r.Raised));
    }

    [Fact]
    public void A_place_outside_the_list_is_refused_before_anything_changes()
    {
        _model.Tags = ["a"];

        Assert.Throws<ArgumentOutOfRangeException>(() => _model.RemoveAt(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _model.Replace(-1, "b"));

        Assert.Equal(["a"], _model.Tags);
        Assert.Empty(_raised);
    }

    [Fact]
    public void Choosing_replaces_the_tag_the_menu_was_opened_from_and_appends_when_it_was_opened_from_the_add_pill()
    {
        _model.Tags = ["a", "b"];

        _model.Target = 0;
        _model.Choose("x");
        _model.Target = null;
        _model.Choose("y");

        Assert.Equal(["x", "b", "y"], _model.Tags);
        Assert.Equal([["x", "b"], ["x", "b", "y"]], _raised.Select(r => r.Raised));
    }

    [Fact]
    public void Choosing_after_a_sync_added_tags_replaces_the_tag_the_menu_was_opened_from_wherever_it_now_sits()
    {
        _model.Tags = ["work", "home"];
        _model.Target = 0;

        _model.ReplaceTags(["work", "home", "remote"]);
        _model.Choose("ideas");

        Assert.Equal(["ideas", "home", "remote"], _model.Tags);
        Assert.Equal([["ideas", "home", "remote"]], _raised.Select(r => r.Raised));
    }

    [Fact]
    public void Choosing_after_a_sync_moved_the_tag_replaces_it_in_its_new_place_and_the_cursor_follows()
    {
        var chosenAt = new List<int>();
        _model.Chosen += chosenAt.Add;
        _model.Tags = ["work", "home"];
        _model.Target = 0;

        _model.ReplaceTags(["home", "remote", "work"]);
        _model.Choose("ideas");

        Assert.Equal(["home", "remote", "ideas"], _model.Tags);
        Assert.Equal([2], chosenAt);
    }

    [Fact]
    public void Choosing_after_a_sync_that_only_reorders_other_tags_still_replaces_the_menus_tag()
    {
        _model.Tags = ["a", "b", "c"];
        _model.Target = 1;

        _model.ReplaceTags(["c", "b", "a"]);
        _model.Choose("x");

        Assert.Equal(["c", "x", "a"], _model.Tags);
    }

    [Fact]
    public void Adds_tags_that_differ_only_in_a_letter_with_two_lowercase_forms_as_two_tags()
    {
        _model.Add("σ");
        _model.Add("ς");

        Assert.Equal(["σ", "ς"], _model.Tags);
        Assert.Equal(2, _raised.Count);
    }

    [Fact]
    public void Replaces_a_tag_with_one_that_differs_from_it_only_in_a_letter_with_two_lowercase_forms()
    {
        _model.Tags = ["σ"];

        Assert.True(_model.Replace(0, "ς"));

        Assert.Equal(["ς"], _model.Tags);
    }

    [Fact]
    public void The_menu_still_lists_a_tag_that_differs_from_the_tasks_only_in_a_letter_with_two_lowercase_forms()
    {
        _services.UseTag("ς", 2);

        _model.Tags = ["σ"];

        Assert.Equal(["ς"], _model.MenuTags);
    }

    [Fact]
    public void The_menu_leaves_out_every_tag_the_task_has()
    {
        _services.UseTag("a", 3);
        _services.UseTag("b", 2);
        _services.UseTag("c");

        _model.Tags = ["a", "c"];

        Assert.Equal(["b"], _model.MenuTags);
    }

    [Fact]
    public void A_typed_tag_the_task_has_is_not_added_again_and_Custom_closes()
    {
        _model.Tags = ["work"];
        _model.OpenCustom();
        _model.TypedText = "Work";

        _model.AddTypedText();

        Assert.Equal(["work"], _model.Tags);
        Assert.Empty(_raised);
        Assert.False(_model.IsCustomOpen);
    }

    [Fact]
    public void A_typed_tag_the_task_has_is_only_recolored_when_a_color_is_clicked()
    {
        _model.Tags = ["work"];
        _model.OpenCustom();
        _model.TypedText = "work";

        _model.AddTypedTextInColor("red-2");

        Assert.Equal(["work"], _model.Tags);
        Assert.Empty(_raised);
        Assert.Equal([("work", "red-2")], _services.TagColors.Kept);
    }

    [Fact]
    public void Setting_the_tags_raises_nothing_and_normalizes_nothing_it_was_given()
    {
        _model.Tags = ["a", "b"];

        Assert.Equal(["a", "b"], _model.Tags);
        Assert.Empty(_raised);
    }
}
