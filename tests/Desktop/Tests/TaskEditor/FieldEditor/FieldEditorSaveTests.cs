using System.IO;
using System.Windows.Automation;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.FieldEditor;

/// <summary>
/// What the Title editor and the Description editor the Task list hosts do when asked to save, how they report a change, and how
/// the Description editor sets an image's width. The editors are built in memory and never shown.
/// </summary>
[Unit]
public class FieldEditorSaveTests
{
    private static TitleEditor Title(string value)
    {
        var services = new EditorServices();
        services.UseRepository();
        var editor = services.CreateTitleEditor();
        editor.Value = value;
        return editor;
    }

    private static DescriptionEditor Description(string value)
    {
        var services = new EditorServices();
        services.UseRepository();
        var editor = services.CreateDescriptionEditor();
        editor.TaskId = services.Tasks.StartDraft().TaskId;
        editor.Value = value;
        return editor;
    }

    private static void Edit(System.Windows.DependencyObject editor, string field, string text) => EditorFields.Type(editor, field, text);

    private static bool IsMarked(System.Windows.DependencyObject editor) =>
        !string.IsNullOrEmpty(AutomationProperties.GetItemStatus(EditorFields.Field(editor, EditorFields.Title)));

    [WpfFact]
    public void A_Title_editor_has_no_changes_until_its_text_differs_from_the_value_last_set()
    {
        var editor = Title("Buy milk");
        Assert.False(editor.HasChanges);

        Edit(editor, EditorFields.Title, "Buy milk!");
        Assert.True(editor.HasChanges);

        Edit(editor, EditorFields.Title, "Buy milk");
        Assert.False(editor.HasChanges);
    }

    [WpfTheory]
    [InlineData("buy milk")]
    [InlineData("Buy milk ")]
    public void A_Title_editor_counts_a_change_of_case_or_of_a_space(string edited)
    {
        var editor = Title("Buy milk");

        Edit(editor, EditorFields.Title, edited);

        Assert.True(editor.HasChanges);
    }

    [WpfFact]
    public void Setting_the_Title_editors_value_starts_HasChanges_over()
    {
        var editor = Title("Buy milk");
        Edit(editor, EditorFields.Title, "Buy tea");

        editor.Value = "Buy tea";

        Assert.False(editor.HasChanges);
    }

    [WpfFact]
    public void Saving_a_Title_calls_the_function_with_the_title_and_returns_true()
    {
        var editor = Title("Buy milk");
        Edit(editor, EditorFields.Title, "Buy oat milk");
        string? saved = null;

        var result = editor.Save(title => saved = title);

        Assert.True(result);
        Assert.Equal("Buy oat milk", saved);
    }

    [WpfTheory]
    [InlineData("")]
    [InlineData("   ")]
    public void Saving_an_empty_Title_returns_false_marks_the_field_and_calls_nothing(string title)
    {
        var editor = Title("Buy milk");
        Edit(editor, EditorFields.Title, title);
        var called = false;

        var result = editor.Save(_ => called = true);

        Assert.False(result);
        Assert.False(called);
        Assert.True(IsMarked(editor));
    }

    [WpfFact]
    public void The_mark_on_an_empty_Title_goes_when_the_title_is_edited()
    {
        var editor = Title("");
        editor.Save(_ => { });
        Assert.True(IsMarked(editor));

        Edit(editor, EditorFields.Title, "Buy milk");

        Assert.False(IsMarked(editor));
    }

    [WpfTheory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TaskNotFoundException))]
    public void A_Title_save_that_fails_in_a_known_way_returns_false_and_keeps_the_text(Type failure)
    {
        var editor = Title("Buy milk");

        var result = editor.Save(_ => throw (Exception)Activator.CreateInstance(failure)!);

        Assert.False(result);
        Assert.Equal("Buy milk", editor.Value);
    }

    [WpfFact]
    public void A_Title_save_that_fails_in_an_unknown_way_throws()
    {
        var editor = Title("Buy milk");

        Assert.Throws<InvalidOperationException>(() => editor.Save(_ => throw new InvalidOperationException("bug")));
    }

    [WpfFact]
    public void A_Description_editor_has_no_changes_until_its_text_differs_from_the_value_last_set()
    {
        var editor = Description("Two litres");
        Assert.False(editor.HasChanges);

        Edit(editor, EditorFields.Description, "Two litres!");
        Assert.True(editor.HasChanges);

        Edit(editor, EditorFields.Description, "Two litres");
        Assert.False(editor.HasChanges);
    }

    [WpfFact]
    public void Setting_the_Description_editors_value_starts_HasChanges_over()
    {
        var editor = Description("Two litres");
        Edit(editor, EditorFields.Description, "Three litres");

        editor.Value = "Three litres";

        Assert.False(editor.HasChanges);
    }

    [WpfTheory]
    [InlineData("Three litres")]
    [InlineData("")]
    public void Saving_a_Description_calls_the_function_with_the_description_and_returns_true(string description)
    {
        var editor = Description("Two litres");
        Edit(editor, EditorFields.Description, description);
        string? saved = null;

        var result = editor.Save(text => saved = text);

        Assert.True(result);
        Assert.Equal(description, saved);
    }

    [WpfTheory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(TaskNotFoundException))]
    public void A_Description_save_that_fails_in_a_known_way_returns_false_and_keeps_the_text(Type failure)
    {
        var editor = Description("Two litres");

        var result = editor.Save(_ => throw (Exception)Activator.CreateInstance(failure)!);

        Assert.False(result);
        Assert.Equal("Two litres", editor.Value);
    }

    [WpfFact]
    public void A_Description_save_that_fails_in_an_unknown_way_throws()
    {
        var editor = Description("Two litres");

        Assert.Throws<InvalidOperationException>(() => editor.Save(_ => throw new InvalidOperationException("bug")));
    }

    [WpfFact]
    public void SetImageWidth_adds_the_width_to_the_numbered_image_and_reports_changes()
    {
        var editor = Description("![a](images/a.png) ![b](images/b.png)");

        editor.SetImageWidth(1, 200);

        Assert.Equal("![a](images/a.png) ![b|200](images/b.png)", editor.Value);
        Assert.True(editor.HasChanges);
    }

    [WpfFact]
    public void SetImageWidth_replaces_a_width_the_image_already_has()
    {
        var editor = Description("![photo|100](images/a.png)");

        editor.SetImageWidth(0, 250);

        Assert.Equal("![photo|250](images/a.png)", editor.Value);
        Assert.True(editor.HasChanges);
    }

    [WpfFact]
    public void SetImageWidth_with_the_width_the_text_already_has_changes_nothing()
    {
        var editor = Description("![photo|300](images/a.png)");

        editor.SetImageWidth(0, 300);

        Assert.Equal("![photo|300](images/a.png)", editor.Value);
        Assert.False(editor.HasChanges);
    }

    [WpfTheory]
    [InlineData(1, 300)]
    [InlineData(-1, 300)]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    [InlineData(0, 10001)]
    public void SetImageWidth_for_a_missing_image_or_a_width_outside_1_to_10000_throws_and_keeps_the_text(int image, int width)
    {
        var editor = Description("![photo](images/a.png)");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetImageWidth(image, width));

        Assert.Equal("![photo](images/a.png)", editor.Value);
        Assert.False(editor.HasChanges);
    }

    [WpfTheory]
    [InlineData(1)]
    [InlineData(10000)]
    public void SetImageWidth_accepts_the_smallest_and_largest_width(int width)
    {
        var editor = Description("![photo](images/a.png)");

        editor.SetImageWidth(0, width);

        Assert.Equal($"![photo|{width}](images/a.png)", editor.Value);
    }
}
