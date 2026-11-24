using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>What counts as a change since the editor's value was last set.</summary>
[Unit]
public class EditorBaselineTests
{
    private static readonly TaskDraft Start = new("id", "Title", "Body", ["Home", "work"]);

    private readonly EditorBaseline _baseline = new();

    public EditorBaselineTests() => _baseline.Reset(Start);

    private bool Changed(string title = "Title", string description = "Body", string typed = "", params string[] tags) =>
        _baseline.HasChanges(title, description, tags.Length == 0 ? ["Home", "work"] : tags, typed);

    [Fact]
    public void A_new_baseline_is_empty_so_an_empty_editor_has_no_changes()
    {
        Assert.False(new EditorBaseline().HasChanges("", "", [], ""));
    }

    [Fact]
    public void A_new_baseline_counts_any_title_as_a_change()
    {
        Assert.True(new EditorBaseline().HasChanges("x", "", [], ""));
    }

    [Fact]
    public void The_value_just_set_has_no_changes()
    {
        Assert.False(Changed());
    }

    [Fact]
    public void A_different_title_is_a_change()
    {
        Assert.True(Changed(title: "Other"));
    }

    [Fact]
    public void A_different_description_is_a_change()
    {
        Assert.True(Changed(description: "Other"));
    }

    [Fact]
    public void Tags_in_another_order_or_case_are_not_a_change()
    {
        Assert.False(Changed(tags: ["WORK", "home"]));
    }

    [Fact]
    public void An_added_or_removed_tag_is_a_change()
    {
        Assert.True(Changed(tags: ["Home", "work", "x"]));
        Assert.True(Changed(tags: ["Home"]));
    }

    [Fact]
    public void Typed_tag_text_not_yet_added_is_a_change()
    {
        Assert.True(Changed(typed: "ho"));
    }

    [Fact]
    public void Blank_typed_tag_text_is_not_a_change()
    {
        Assert.False(Changed(typed: "  "));
    }

    [Fact]
    public void Setting_a_new_value_makes_it_the_baseline()
    {
        _baseline.Reset(new TaskDraft("id2", "Other", "", []));

        Assert.False(_baseline.HasChanges("Other", "", [], ""));
        Assert.True(_baseline.HasChanges("Title", "Body", ["Home", "work"], ""));
    }
}
