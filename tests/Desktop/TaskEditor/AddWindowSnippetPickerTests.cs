using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Typing "/" in the Description opens Editing's snippet picker as a list at the cursor. A test finds the list as the
/// element whose automation id is "SnippetList", whose items are the snippet names, each with a button, as the color list is.
/// Editing's own tests cover when "/" opens the picker and what matches; these cover that the Description asks for them,
/// shows the answer, and inserts the chosen snippet. Texts use "\n" line breaks.
/// </summary>
[UX]
public class AddWindowSnippetPickerTests
{
    private static readonly string[] AllOffered =
    [
        "Date stamp", "Date and time stamp", "Note", "Tip", "Warning", "Link", "Image", "Code block", "Task reference",
        "Table", "Horizontal line",
    ];

    [WpfFact]
    public void The_snippet_list_is_closed_until_a_slash_is_typed()
    {
        using var screen = new AddScreen();
        screen.Show();

        Assert.Empty(screen.ShownSnippets);
    }

    [WpfFact]
    public void Typing_a_slash_at_the_start_of_the_Description_lists_every_snippet_that_works_yet()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("/");

        Assert.Equal(AllOffered, screen.ShownSnippets);
    }

    [WpfFact]
    public void The_snippet_list_offers_Image_after_Link()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("/");

        var shown = screen.ShownSnippets;
        Assert.Equal(shown.ToList().IndexOf("Link") + 1, shown.ToList().IndexOf("Image"));
    }

    [WpfTheory]
    [InlineData("a ")]
    [InlineData("a\n")]
    public void Typing_a_slash_after_a_space_or_a_line_break_opens_the_list(string before)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, before, before.Length);

        screen.Type("/");

        Assert.Equal(AllOffered, screen.ShownSnippets);
    }

    [WpfFact]
    public void Typing_a_slash_inside_a_word_does_not_open_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "and", 3);

        screen.Type("/");

        Assert.Empty(screen.ShownSnippets);
        Assert.Equal("and/", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Typing_a_slash_in_the_Title_does_not_open_the_list()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "", 0);

        screen.Type("/");

        Assert.Empty(screen.ShownSnippets);
        Assert.Equal("/", screen.TextOf(EditorFields.Title));
    }

    [WpfFact]
    public void Opening_the_list_leaves_the_typed_slash_in_the_text()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("/");

        Assert.Equal("/", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfTheory]
    [InlineData("/ta", new[] { "Task reference", "Table" })]
    [InlineData("/TIP", new[] { "Tip" })]
    [InlineData("/lin", new[] { "Link", "Horizontal line" })]
    [InlineData("/date", new[] { "Date stamp", "Date and time stamp" })]
    public void Typing_after_the_slash_narrows_the_list_to_names_with_a_word_that_starts_with_it(string typed, string[] expected)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type(typed);

        Assert.Equal(expected, screen.ShownSnippets);
    }

    [WpfFact]
    public void When_nothing_matches_the_list_closes_and_the_typed_text_stays()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("/zzz");

        Assert.Empty(screen.ShownSnippets);
        Assert.Equal("/zzz", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Down_then_Enter_replaces_the_slash_and_what_was_typed_with_the_snippet()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/no");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal("> [!NOTE]\n> ", screen.TextOf(EditorFields.Description));
        Assert.Equal((12, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Clicking_a_snippet_inserts_it_where_the_slash_was_typed()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "a ", 2);
        screen.Type("/ti");

        screen.ClickSnippet("Tip");

        Assert.Equal("a \n> [!TIP]\n> ", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_a_snippet_keeps_the_text_after_the_cursor()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "see  end", 4);
        screen.Type("/da");

        screen.ClickSnippet("Date stamp");

        Assert.Equal("see " + screen.LocalNow.ToString("yyyy-MM-dd") + " end", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_the_Link_snippet_uses_the_URL_on_the_clipboard()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = "https://example.com";
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/link");

        screen.ClickSnippet("Link");

        Assert.Equal("[](https://example.com)", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Choosing_a_snippet_that_needs_a_task_opens_the_task_list_and_the_chosen_task_is_linked_in_place_of_the_slash()
    {
        using var screen = new AddScreen();
        var task = screen.Services.AddTaskTitled("Buy milk");
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/task");
        screen.ClickSnippet("Task reference");
        Assert.Equal(["Buy milk"], screen.ShownTaskReferences);

        screen.ClickTaskReference("Buy milk");

        Assert.Equal($"[Buy milk](task:{task.Id})", screen.TextOf(EditorFields.Description));
        Assert.Empty(screen.ShownSnippets);
    }

    [WpfFact]
    public void Choosing_a_snippet_closes_the_list_and_keeps_the_cursor_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/tip");

        screen.ClickSnippet("Tip");

        Assert.Empty(screen.ShownSnippets);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void Choosing_a_snippet_does_not_save_or_close_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Title");
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/tip");

        screen.ClickSnippet("Tip");

        Assert.True(screen.IsShowing);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void One_undo_puts_back_the_slash_and_what_was_typed()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/tip");
        screen.ClickSnippet("Tip");

        screen.Undo(EditorFields.Description);

        Assert.Equal("/tip", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Esc_closes_the_list_first_and_leaves_the_window_open_and_the_typed_text_alone()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/ti");

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownSnippets);
        Assert.True(screen.IsShowing);
        Assert.Equal("/ti", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_second_Esc_closes_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("/ti");
        screen.Press(Key.Escape);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
    }

    [WpfFact]
    public void Enter_with_the_list_closed_breaks_the_line_as_usual()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "ab", 1);

        screen.Press(Key.Enter);

        Assert.Equal("a\nb", screen.TextOf(EditorFields.Description).Replace("\r\n", "\n"));
    }
}
