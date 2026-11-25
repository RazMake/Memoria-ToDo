using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What the + menu lists at its top: the tags in use that the task doesn't have, most-used first, up to eight. Search,
/// Custom…, and the menu's keys are the menu model's; how the menu looks is the tag control's, in its UX tests.
/// </summary>
[Unit]
public class TagsModelMenuTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;

    public TagsModelMenuTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors, Serilog.Core.Logger.None);
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
    public void Lists_only_the_eight_most_used()
    {
        for (var i = 1; i <= 10; i++)
        {
            _services.UseTag($"tag{i}", i);
        }

        Assert.Equal(["tag10", "tag9", "tag8", "tag7", "tag6", "tag5", "tag4", "tag3"], _model.MenuTags);
    }

    [Fact]
    public void Leaves_out_every_tag_the_task_has_and_still_lists_eight()
    {
        for (var i = 1; i <= 10; i++)
        {
            _services.UseTag($"tag{i}", i);
        }

        _model.Tags = ["tag10"];

        Assert.Equal(["tag9", "tag8", "tag7", "tag6", "tag5", "tag4", "tag3", "tag2"], _model.MenuTags);
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
    public void Drops_the_tag_the_task_has_from_the_menu()
    {
        _services.UseTag("home", 2);
        _services.UseTag("work");

        _model.Add("home");

        Assert.Equal(["work"], _model.MenuTags);
    }

    [Fact]
    public void Follows_the_tags_in_use_as_they_change()
    {
        _services.UseTag("home");

        _services.UseTag("work", 2);

        Assert.Equal(["work", "home"], _model.MenuTags);
    }

    [Fact]
    public void Adding_a_listed_tag_raises_TagsChanged_with_the_whole_list_after_Tags_holds_it()
    {
        _services.UseTag("home");
        var seen = new List<(IReadOnlyList<string> Raised, IReadOnlyList<string> Tags)>();
        _model.TagsChanged += raised => seen.Add((raised, _model.Tags));

        _model.Add("HOME");

        var (raised, tagsThen) = Assert.Single(seen);
        Assert.Equal(["home"], raised);
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
