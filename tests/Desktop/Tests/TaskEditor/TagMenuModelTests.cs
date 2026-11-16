using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The + menu's rules without a screen: when it is open, what it lists, Search and its matches, what choosing an item
/// does, and what each key does. The tags' own rules are in the tags model's tests; how the menu looks is in the tag
/// control's UX tests.
/// </summary>
[Unit]
public class TagMenuModelTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _tags;
    private readonly SuggestingTasks _suggesting;
    private readonly TagMenuModel _menu;
    private readonly List<string> _added = [];

    public TagMenuModelTests()
    {
        _services.UseRepository();
        _tags = new TagsModel(_services.Tasks, _services.TagColors);
        _tags.TagAdded += _added.Add;
        var (tasks, suggesting) = SuggestingTasks.Over(_services.Tasks);
        _suggesting = suggesting;
        _menu = new TagMenuModel(tasks, _tags);
    }

    // Opening and closing.

    [Fact]
    public void Starts_closed_with_no_search_text()
    {
        Assert.False(_menu.IsOpen);
        Assert.Equal("", _menu.SearchText);
    }

    [Fact]
    public void Opens_with_no_item_highlighted_and_no_submenu()
    {
        _menu.Open();

        Assert.True(_menu.IsOpen);
        Assert.Equal(-1, _menu.FocusedIndex);
        Assert.False(_menu.IsSubmenuOpen);
    }

    [Fact]
    public void A_second_click_on_the_plus_closes_the_menu()
    {
        _menu.Toggle();
        Assert.True(_menu.IsOpen);

        _menu.Toggle();

        Assert.False(_menu.IsOpen);
    }

    [Fact]
    public void Close_says_whether_the_menu_was_open()
    {
        Assert.False(_menu.Close());

        _menu.Open();

        Assert.True(_menu.Close());
        Assert.False(_menu.IsOpen);
    }

    [Fact]
    public void A_click_outside_closes_the_menu_as_Close_does_and_drops_the_search_text()
    {
        _menu.Open();
        _menu.SearchText = "wo";

        _menu.Close();

        Assert.Equal("", _menu.SearchText);
        Assert.False(_menu.IsSubmenuOpen);
    }

    [Fact]
    public void Reopening_starts_with_empty_search_and_nothing_highlighted()
    {
        UseThreeTags();
        _menu.Open();
        _menu.MoveDown();
        _menu.SearchText = "wo";
        _menu.Close();

        _menu.Open();

        Assert.Equal("", _menu.SearchText);
        Assert.Equal(-1, _menu.FocusedIndex);
    }

    [Fact]
    public void Setting_search_text_while_closed_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => _menu.SearchText = "wo");
    }

    // The top tags.

    [Fact]
    public void Lists_the_tags_in_use_the_task_lacks_most_used_first()
    {
        UseThreeTags();
        _tags.Tags = ["b"];

        _menu.Open();

        Assert.Equal(["a", "c"], _menu.TopTags);
    }

    [Fact]
    public void Lists_at_most_eight_tags()
    {
        for (var i = 1; i <= 10; i++)
        {
            _services.UseTag($"tag{i}", i);
        }

        _menu.Open();

        Assert.Equal(8, _menu.TopTags.Count);
        Assert.Equal("tag10", _menu.TopTags[0]);
    }

    [Fact]
    public void With_no_top_tags_Search_is_the_first_item_and_Custom_the_second()
    {
        _menu.Open();

        Assert.Empty(_menu.TopTags);
        Assert.Equal(0, _menu.SearchIndex);
        Assert.Equal(1, _menu.CustomIndex);
    }

    [Fact]
    public void Search_follows_the_top_tags_and_Custom_follows_Search()
    {
        UseThreeTags();
        _menu.Open();

        Assert.Equal(3, _menu.SearchIndex);
        Assert.Equal(4, _menu.CustomIndex);
    }

    // Search.

    [Fact]
    public void Search_keeps_the_text_as_typed_with_capitals_and_spaces()
    {
        _menu.Open();

        _menu.SearchText = "Big Errand";

        Assert.Equal("Big Errand", _menu.SearchText);
    }

    [Fact]
    public void Matches_are_what_Tasks_suggests_for_the_text_and_the_tags_the_task_has()
    {
        _tags.Tags = ["x"];
        _suggesting.Suggest = (_, _) => ["work", "network"];
        _menu.Open();

        _menu.SearchText = "ork";

        Assert.Equal(["work", "network"], _menu.Matches);
        Assert.Contains(_suggesting.Calls, call => call.Typed == "ork" && call.Tags.SequenceEqual(["x"]));
    }

    [Fact]
    public void Matches_may_repeat_a_top_tag()
    {
        _services.UseTag("work", 2);
        _suggesting.Suggest = (_, _) => ["work"];
        _menu.Open();

        _menu.SearchText = "wo";

        Assert.Equal(["work"], _menu.TopTags);
        Assert.Equal(["work"], _menu.Matches);
    }

    [Fact]
    public void Typing_in_Search_opens_the_submenu_with_the_matches()
    {
        _suggesting.Suggest = (_, _) => ["work"];
        _menu.Open();

        _menu.SearchText = "wo";

        Assert.True(_menu.IsSubmenuOpen);
        Assert.False(_menu.ShowsNoMatch);
    }

    [Fact]
    public void An_empty_Search_box_shows_no_submenu_even_if_Tasks_would_suggest_tags()
    {
        _suggesting.Suggest = (_, _) => ["work"];
        _menu.Open();
        _menu.SearchText = "wo";

        _menu.SearchText = "";

        Assert.False(_menu.IsSubmenuOpen);
        Assert.Empty(_menu.Matches);
        Assert.False(_menu.ShowsNoMatch);
    }

    [Fact]
    public void With_no_match_the_submenu_shows_No_matching_tags()
    {
        _menu.Open();

        _menu.SearchText = "zzz";

        Assert.True(_menu.IsSubmenuOpen);
        Assert.True(_menu.ShowsNoMatch);
        Assert.Empty(_menu.Matches);
    }

    [Fact]
    public void The_submenu_is_closed_with_the_menu()
    {
        _menu.Open();
        _menu.SearchText = "zzz";

        _menu.Close();

        Assert.False(_menu.IsSubmenuOpen);
        Assert.False(_menu.ShowsNoMatch);
    }

    [Fact]
    public void Search_text_is_not_part_of_the_task_or_the_typed_tag()
    {
        _menu.Open();

        _menu.SearchText = "wo";
        _menu.Close();

        Assert.Empty(_tags.Tags);
        Assert.Equal("", _tags.TypedText);
    }

    // Choosing.

    [Fact]
    public void Clicking_a_top_tag_adds_it_closes_the_menu_and_drops_the_search_text()
    {
        UseThreeTags();
        _menu.Open();
        _menu.SearchText = "x";

        _menu.ChooseTag("b");

        Assert.Equal(["b"], _tags.Tags);
        Assert.Equal(["b"], _added);
        Assert.False(_menu.IsOpen);
        Assert.False(_menu.IsSubmenuOpen);
        Assert.Equal("", _menu.SearchText);
    }

    [Fact]
    public void A_top_tag_is_added_as_Tasks_spells_it_and_keeps_no_color()
    {
        _services.UseTag("Work");
        _menu.Open();

        _menu.ChooseTag("Work");

        Assert.Equal(["Work"], _tags.Tags);
        Assert.Empty(_services.TagColors.Kept);
    }

    [Fact]
    public void Clicking_a_match_adds_that_tag_closes_both_menus_and_drops_the_search_text()
    {
        _suggesting.Suggest = (_, _) => ["work", "network"];
        _menu.Open();
        _menu.SearchText = "ork";

        _menu.ChooseMatch(1);

        Assert.Equal(["network"], _tags.Tags);
        Assert.Equal(["network"], _added);
        Assert.False(_menu.IsOpen);
        Assert.False(_menu.IsSubmenuOpen);
        Assert.Equal("", _menu.SearchText);
    }

    [Fact]
    public void Adding_a_tag_from_the_menu_while_Custom_shows_leaves_Custom_and_its_text()
    {
        UseThreeTags();
        _tags.OpenCustom();
        _tags.TypedText = "errand";
        _menu.Open();

        _menu.ChooseTag("a");

        Assert.True(_tags.IsCustomOpen);
        Assert.Equal("errand", _tags.TypedText);
        Assert.Equal(["a"], _tags.Tags);
    }

    [Fact]
    public void Custom_closes_the_menu_and_opens_Custom_and_drops_the_search_text()
    {
        _menu.Open();
        _menu.SearchText = "wo";

        _menu.ChooseCustom();

        Assert.False(_menu.IsOpen);
        Assert.Equal("", _menu.SearchText);
        Assert.True(_tags.IsCustomOpen);
        Assert.Empty(_tags.Tags);
    }

    // Keys.

    [Fact]
    public void Down_moves_through_the_tags_then_Search_then_Custom_and_stays_on_Custom()
    {
        UseThreeTags();
        _menu.Open();

        var seen = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            _menu.MoveDown();
            seen.Add(_menu.FocusedIndex);
        }

        Assert.Equal([0, 1, 2, _menu.SearchIndex, _menu.CustomIndex, _menu.CustomIndex], seen);
    }

    [Fact]
    public void Up_moves_back_through_Custom_Search_and_the_tags()
    {
        UseThreeTags();
        _menu.Open();
        for (var i = 0; i < 5; i++)
        {
            _menu.MoveDown();
        }

        var seen = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            _menu.MoveUp();
            seen.Add(_menu.FocusedIndex);
        }

        Assert.Equal([_menu.SearchIndex, 2, 1, 0, 0], seen);
    }

    [Fact]
    public void With_no_top_tags_Down_goes_to_Search_first()
    {
        _menu.Open();

        _menu.MoveDown();

        Assert.Equal(_menu.SearchIndex, _menu.FocusedIndex);
    }

    [Theory]
    [InlineData('w')]
    [InlineData('W')]
    [InlineData('7')]
    public void A_letter_or_digit_outside_Search_goes_into_the_Search_box_and_puts_the_cursor_there(char typed)
    {
        UseThreeTags();
        _menu.Open();
        _menu.MoveDown();

        var handled = _menu.TypeOutsideSearch(typed);

        Assert.True(handled);
        Assert.Equal(typed.ToString(), _menu.SearchText);
        Assert.Equal(_menu.SearchIndex, _menu.FocusedIndex);
    }

    [Fact]
    public void Letters_typed_outside_Search_add_to_what_is_there()
    {
        _menu.Open();
        _menu.SearchText = "wo";

        _menu.TypeOutsideSearch('r');

        Assert.Equal("wor", _menu.SearchText);
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('-')]
    [InlineData('!')]
    public void Other_characters_outside_Search_are_left_alone(char typed)
    {
        _menu.Open();

        var handled = _menu.TypeOutsideSearch(typed);

        Assert.False(handled);
        Assert.Equal("", _menu.SearchText);
    }

    [Fact]
    public void In_Search_with_matches_Down_moves_a_highlight_through_them_while_the_cursor_stays_in_Search()
    {
        UseSearchWithMatches("a1", "a2", "a3");

        _menu.MoveDown();
        Assert.Equal(0, _menu.HighlightedMatch);
        _menu.MoveDown();
        Assert.Equal(1, _menu.HighlightedMatch);

        Assert.Equal(_menu.SearchIndex, _menu.FocusedIndex);
    }

    [Fact]
    public void In_Search_Up_from_the_first_match_leaves_none_highlighted()
    {
        UseSearchWithMatches("a1", "a2");
        _menu.MoveDown();

        _menu.MoveUp();

        Assert.Equal(-1, _menu.HighlightedMatch);
        Assert.Equal(_menu.SearchIndex, _menu.FocusedIndex);
    }

    [Fact]
    public void In_Search_Up_moves_the_highlight_back_one_match()
    {
        UseSearchWithMatches("a1", "a2", "a3");
        _menu.MoveDown();
        _menu.MoveDown();

        _menu.MoveUp();

        Assert.Equal(0, _menu.HighlightedMatch);
    }

    [Fact]
    public void In_Search_with_no_matches_Down_moves_to_Custom()
    {
        UseThreeTags();
        _menu.Open();
        _menu.TypeOutsideSearch('z');

        _menu.MoveDown();

        Assert.Equal(_menu.CustomIndex, _menu.FocusedIndex);
    }

    [Fact]
    public void In_Search_with_no_matches_Up_moves_to_the_last_tag()
    {
        UseThreeTags();
        _menu.Open();
        _menu.TypeOutsideSearch('z');

        _menu.MoveUp();

        Assert.Equal(2, _menu.FocusedIndex);
    }

    [Fact]
    public void In_an_empty_Search_Down_moves_to_Custom()
    {
        UseThreeTags();
        _menu.Open();
        _menu.MoveDown();
        _menu.MoveDown();
        _menu.MoveDown();
        _menu.MoveDown();
        Assert.Equal(_menu.SearchIndex, _menu.FocusedIndex);

        _menu.MoveDown();

        Assert.Equal(_menu.CustomIndex, _menu.FocusedIndex);
    }

    [Fact]
    public void In_an_empty_Search_Up_moves_to_the_last_tag()
    {
        UseThreeTags();
        _menu.Open();
        _menu.MoveDown();
        _menu.MoveDown();
        _menu.MoveDown();
        _menu.MoveDown();
        Assert.Equal(_menu.SearchIndex, _menu.FocusedIndex);

        _menu.MoveUp();

        Assert.Equal(2, _menu.FocusedIndex);
    }

    [Fact]
    public void Enter_in_Search_adds_the_first_match_when_none_is_highlighted()
    {
        UseSearchWithMatches("a1", "a2");

        _menu.Enter();

        Assert.Equal(["a1"], _tags.Tags);
        Assert.False(_menu.IsOpen);
    }

    [Fact]
    public void Enter_in_Search_adds_the_highlighted_match()
    {
        UseSearchWithMatches("a1", "a2");
        _menu.MoveDown();
        _menu.MoveDown();

        _menu.Enter();

        Assert.Equal(["a2"], _tags.Tags);
    }

    [Fact]
    public void Enter_in_Search_with_no_match_does_nothing()
    {
        _menu.Open();
        _menu.TypeOutsideSearch('z');

        _menu.Enter();

        Assert.Empty(_tags.Tags);
        Assert.True(_menu.IsOpen);
        Assert.Equal("z", _menu.SearchText);
    }

    [Fact]
    public void Enter_on_a_tag_adds_it_and_closes_the_menu()
    {
        UseThreeTags();
        _menu.Open();
        _menu.MoveDown();
        _menu.MoveDown();

        _menu.Enter();

        Assert.Equal(["b"], _tags.Tags);
        Assert.False(_menu.IsOpen);
    }

    [Fact]
    public void Enter_on_Custom_closes_the_menu_and_opens_Custom()
    {
        UseThreeTags();
        _menu.Open();
        for (var i = 0; i < 5; i++)
        {
            _menu.MoveDown();
        }

        _menu.Enter();

        Assert.False(_menu.IsOpen);
        Assert.True(_tags.IsCustomOpen);
        Assert.Empty(_tags.Tags);
    }

    [Fact]
    public void Enter_with_nothing_highlighted_does_nothing()
    {
        UseThreeTags();
        _menu.Open();

        _menu.Enter();

        Assert.True(_menu.IsOpen);
        Assert.Empty(_tags.Tags);
    }

    [Fact]
    public void Esc_closes_the_menu_and_drops_the_search_text()
    {
        _menu.Open();
        _menu.SearchText = "wo";

        Assert.True(_menu.Escape());

        Assert.False(_menu.IsOpen);
        Assert.Equal("", _menu.SearchText);
    }

    [Fact]
    public void Esc_with_the_menu_closed_says_so_and_does_nothing()
    {
        Assert.False(_menu.Escape());
    }

    [Fact]
    public void Changed_is_raised_when_the_menu_opens_moves_and_closes()
    {
        var raised = 0;
        _menu.Changed += () => raised++;

        _menu.Open();
        _menu.MoveDown();
        _menu.Close();

        Assert.Equal(3, raised);
    }

    private void UseThreeTags()
    {
        _services.UseTag("a", 3);
        _services.UseTag("b", 2);
        _services.UseTag("c");
    }

    // Search holds the cursor, Tasks suggests the matches, and nothing is highlighted yet.
    private void UseSearchWithMatches(params string[] matches)
    {
        _suggesting.Suggest = (_, _) => matches;
        _menu.Open();
        _menu.TypeOutsideSearch('a');
    }
}
