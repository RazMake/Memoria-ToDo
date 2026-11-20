using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The task's one tag: clearing it, replacing it by choosing another, and choosing the one it has. What each raises, with
/// which tag, and whether observers are told to draw again.
/// </summary>
[Unit]
public class TagsModelSingleTagTests
{
    private readonly EditorServices _services = new();
    private readonly TagsModel _model;
    private readonly List<string> _added = [];
    private readonly List<string> _removed = [];
    private int _changed;

    public TagsModelSingleTagTests()
    {
        _services.UseRepository();
        _model = new TagsModel(_services.Tasks, _services.TagColors, Serilog.Core.Logger.None);
        _model.TagAdded += _added.Add;
        _model.TagRemoved += _removed.Add;
        _model.Changed += () => _changed++;
    }

    [Fact]
    public void Clearing_empties_the_tags_raises_TagRemoved_once_with_the_cleared_tag_and_tells_observers()
    {
        _model.Tags = ["Home"];
        _changed = 0;

        _model.Clear();

        Assert.Empty(_model.Tags);
        Assert.Equal(["Home"], _removed);
        Assert.Empty(_added);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void Clearing_raises_TagRemoved_after_the_tags_are_empty()
    {
        _model.Tags = ["Home"];
        IReadOnlyList<string>? tagsThen = null;
        _model.TagRemoved += _ => tagsThen = _model.Tags;

        _model.Clear();

        Assert.Empty(tagsThen!);
    }

    [Fact]
    public void Clearing_when_the_task_has_no_tag_does_nothing()
    {
        _model.Clear();

        Assert.Empty(_model.Tags);
        Assert.Empty(_removed);
        Assert.Empty(_added);
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void Choosing_a_different_tag_replaces_the_tag_raises_TagAdded_once_with_it_and_tells_observers()
    {
        _model.Tags = ["home"];
        _changed = 0;

        _model.Add("work");

        Assert.Equal(["work"], _model.Tags);
        Assert.Equal(["work"], _added);
        Assert.Empty(_removed);
        Assert.Equal(1, _changed);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("HOME")]
    public void Choosing_the_tag_the_task_has_ignoring_case_raises_nothing_and_keeps_its_spelling(string chosen)
    {
        _model.Tags = ["home"];
        _changed = 0;

        _model.Add(chosen);

        Assert.Equal(["home"], _model.Tags);
        Assert.Empty(_added);
        Assert.Empty(_removed);
        Assert.Equal(0, _changed);
    }
}
