using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What the + menu lists: the tags in use that the task doesn't have, most-used first, up to seven. The Custom item
/// the menu always ends with, and how the menu looks and moves, are the tag control's, in its UX tests.
/// </summary>
[Unit]
public class TagsModelMenuTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;

    public TagsModelMenuTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors);
    }

    [Fact]
    public void Lists_the_tags_in_use_most_used_first_with_ties_alphabetical()
    {
        _services.UseTag("beta", 2);
        _services.UseTag("alpha", 2);
        _services.UseTag("gamma", 3);
        _services.UseTag("delta");

        Assert.Equal(["gamma", "alpha", "beta", "delta"], _model.MenuTags);
    }

    [Fact]
    public void Lists_only_the_seven_most_used()
    {
        for (var i = 1; i <= 9; i++)
        {
            _services.UseTag($"tag{i}", i);
        }

        Assert.Equal(["tag9", "tag8", "tag7", "tag6", "tag5", "tag4", "tag3"], _model.MenuTags);
    }

    [Fact]
    public void Leaves_out_the_tags_the_task_has_ignoring_case_and_still_lists_seven()
    {
        for (var i = 1; i <= 9; i++)
        {
            _services.UseTag($"tag{i}", i);
        }

        _model.Tags = ["TAG9"];

        Assert.Equal(["tag8", "tag7", "tag6", "tag5", "tag4", "tag3", "tag2"], _model.MenuTags);
    }

    [Fact]
    public void Is_empty_when_no_tag_is_in_use_or_the_task_has_them_all()
    {
        Assert.Empty(_model.MenuTags);

        _services.UseTag("home");
        _model.Tags = ["home"];

        Assert.Empty(_model.MenuTags);
    }

    [Fact]
    public void Drops_a_tag_once_it_is_added_and_brings_it_back_when_it_is_removed()
    {
        _services.UseTag("home", 2);
        _services.UseTag("work");

        _model.Add("home");
        Assert.Equal(["work"], _model.MenuTags);

        _model.Remove("home");
        Assert.Equal(["home", "work"], _model.MenuTags);
    }

    [Fact]
    public void Follows_the_tags_in_use_after_a_refresh()
    {
        _services.UseTag("home");

        _services.UseTag("work", 2);
        _model.Refresh();

        Assert.Equal(["work", "home"], _model.MenuTags);
    }

    [Fact]
    public void Adding_a_listed_tag_raises_TagAdded_after_Tags_holds_it()
    {
        _services.UseTag("home");
        var seen = new List<(string Tag, IReadOnlyList<string> Tags)>();
        _model.TagAdded += tag => seen.Add((tag, _model.Tags));

        _model.Add("HOME");

        var (added, tagsThen) = Assert.Single(seen);
        Assert.Equal("home", added);
        Assert.Equal(["home"], tagsThen);
    }

    [Fact]
    public void Adding_a_listed_tag_keeps_no_color()
    {
        _services.UseTag("home");

        _model.Add("home");

        Assert.Empty(_services.TagColors.Kept);
    }
}
