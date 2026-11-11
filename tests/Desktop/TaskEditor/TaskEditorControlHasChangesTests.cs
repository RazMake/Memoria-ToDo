using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// <c>HasChanges</c> says whether the user changed anything since the last <c>Value</c> was assigned, counting tag text typed
/// and not yet added. The Task list uses it to decide whether leaving an edit needs an Undo bar.
/// </summary>
[UX]
public class TaskEditorControlHasChangesTests
{
    [WpfFact]
    public void A_new_editor_has_no_changes()
    {
        using var host = new EditorHost();

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Assigning_a_value_makes_it_the_baseline_with_no_changes()
    {
        using var host = new EditorHost();

        host.Fill("Buy milk", "Two litres", "home");

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Assigning_a_value_again_resets_the_baseline_after_changes()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");
        EditorFields.Type(host.Editor, EditorFields.Title, "Buy oat milk");
        Assert.True(host.Editor.HasChanges);

        host.Fill("Call Ana");

        Assert.False(host.Editor.HasChanges);
    }

    [WpfTheory]
    [InlineData(EditorFields.Title, "Buy oat milk")]
    [InlineData(EditorFields.Description, "Three litres")]
    public void Changing_the_title_or_the_description_is_a_change(string field, string text)
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");

        EditorFields.Type(host.Editor, field, text);

        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Putting_the_original_text_back_is_no_change()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");
        EditorFields.Type(host.Editor, EditorFields.Title, "Buy oat milk");

        EditorFields.Type(host.Editor, EditorFields.Title, "Buy milk");

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Adding_a_tag_is_a_change()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home");
        EditorFields.Type(host.Editor, EditorFields.Tags, "errand");

        host.Editor.Save(_ => { });

        Assert.Equal(["home", "errand"], host.Editor.Value.Tags);
        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Removing_a_tag_is_a_change_and_adding_it_back_is_not()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home", "errand");

        OpenWindows.Click(EditorFields.RemoveButton(host.Editor, "errand"));
        Assert.True(host.Editor.HasChanges);

        EditorFields.Type(host.Editor, EditorFields.Tags, "errand");
        host.Editor.Save(_ => { });

        Assert.Equal(["home", "errand"], host.Editor.Value.Tags);
        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Tags_typed_in_another_case_are_no_change()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home", "Work");

        OpenWindows.Click(EditorFields.RemoveButton(host.Editor, "Work"));
        EditorFields.Type(host.Editor, EditorFields.Tags, "WORK");
        host.Editor.Save(_ => { });

        Assert.Equal(["home", "WORK"], host.Editor.Value.Tags);
        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void The_same_tags_in_another_order_are_no_change()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home", "work");

        OpenWindows.Click(EditorFields.RemoveButton(host.Editor, "home"));
        EditorFields.Type(host.Editor, EditorFields.Tags, "home");
        host.Editor.Save(_ => { });

        Assert.Equal(["work", "home"], host.Editor.Value.Tags);
        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Tag_text_typed_that_is_only_blanks_is_no_change()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home");

        EditorFields.Type(host.Editor, EditorFields.Tags, "   ");

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Tag_text_typed_and_not_yet_added_is_a_change_even_when_the_value_is_unchanged()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home");

        EditorFields.Type(host.Editor, EditorFields.Tags, "err");

        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Clearing_the_tag_text_typed_is_no_change_again()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home");
        EditorFields.Type(host.Editor, EditorFields.Tags, "err");

        EditorFields.Type(host.Editor, EditorFields.Tags, "");

        Assert.False(host.Editor.HasChanges);
    }

    [WpfFact]
    public void Asking_for_changes_adds_no_tag_marks_no_field_and_raises_no_request()
    {
        using var host = new EditorHost();
        host.Fill("", "Two litres", "home");
        EditorFields.Type(host.Editor, EditorFields.Tags, "err");

        var changed = host.Editor.HasChanges;

        Assert.True(changed);
        Assert.Equal("err", EditorFields.TextOf(host.Editor, EditorFields.Tags));
        Assert.Equal(["home"], host.Editor.Value.Tags);
        Assert.False(host.TitleIsMarked);
        Assert.Equal((0, 0, 0), host.Counts);
    }
}
