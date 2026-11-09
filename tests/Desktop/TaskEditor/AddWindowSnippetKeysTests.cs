using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The keys that insert Editing's snippets, and the link filled from the clipboard. Editing's own tests cover what each
/// snippet writes; these cover that the right key inserts the right snippet with the input the app owes it (the local time,
/// the clipboard's text), and that a snippet that doesn't work in the Title leaves the Title alone. Texts use "\n" line breaks.
/// </summary>
[UX]
public class AddWindowSnippetKeysTests
{
    private const string Url = "https://example.com/docs";

    [WpfFact]
    public void Ctrl_semicolon_inserts_the_local_date_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Time.Advance(TimeSpan.FromHours(10.5));
        screen.Edit(EditorFields.Description, "on ", 3);

        screen.Press(Key.LeftCtrl, Key.OemSemicolon);

        Assert.Equal("on " + screen.LocalNow.ToString("yyyy-MM-dd"), screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_Shift_semicolon_inserts_the_local_date_and_time_in_the_Description()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Time.Advance(TimeSpan.FromHours(10.5));
        screen.Edit(EditorFields.Description, "at ", 3);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.OemSemicolon);

        Assert.Equal("at " + screen.LocalNow.ToString("yyyy-MM-dd HH:mm"), screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(false, "yyyy-MM-dd")]
    [InlineData(true, "yyyy-MM-dd HH:mm")]
    public void The_stamp_keys_work_in_the_Title(bool withShift, string format)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Time.Advance(TimeSpan.FromHours(10.5));
        screen.Edit(EditorFields.Title, "Call ", 5);

        screen.PressCtrl(withShift, Key.OemSemicolon);

        Assert.Equal("Call " + screen.LocalNow.ToString(format), screen.TextOf(EditorFields.Title));
        Assert.Equal(EditorFields.Title, screen.FocusedField);
    }

    [WpfFact]
    public void A_stamp_uses_the_time_when_the_key_is_pressed()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Services.Time.Advance(TimeSpan.FromDays(3));

        screen.Press(Key.LeftCtrl, Key.OemSemicolon);

        Assert.Equal(screen.LocalNow.ToString("yyyy-MM-dd"), screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(Key.N, "> [!NOTE]\n> ")]
    [InlineData(Key.T, "> [!TIP]\n> ")]
    [InlineData(Key.W, "> [!WARNING]\n> ")]
    public void The_block_keys_insert_their_block_in_the_Description(Key key, string expected)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Press(Key.LeftCtrl, Key.LeftShift, key);

        Assert.Equal(expected, screen.TextOf(EditorFields.Description));
        Assert.Equal((expected.Length, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_block_key_after_text_starts_the_block_on_its_own_line()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "hello", 5);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.N);

        Assert.Equal("hello\n> [!NOTE]\n> ", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_Shift_E_inserts_a_code_block_with_the_cursor_inside_it()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.E);

        Assert.Equal("```\n\n```", screen.TextOf(EditorFields.Description));
        Assert.Equal((4, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_Shift_G_inserts_a_table()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.G);

        Assert.Equal("| Column 1 | Column 2 |\n| --- | --- |\n|  |  |", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_Shift_hyphen_inserts_a_horizontal_line_after_a_blank_line()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "text", 4);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.OemMinus);

        Assert.Equal("text\n\n---", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(Key.N)]
    [InlineData(Key.T)]
    [InlineData(Key.W)]
    [InlineData(Key.E)]
    [InlineData(Key.G)]
    [InlineData(Key.OemMinus)]
    public void A_snippet_that_does_not_work_in_the_Title_changes_nothing_there(Key key)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Title, "ab", 1);

        screen.Press(Key.LeftCtrl, Key.LeftShift, key);

        Assert.Equal("ab", screen.TextOf(EditorFields.Title));
        Assert.Equal((1, 0), screen.SelectionOf(EditorFields.Title));
        Assert.Equal(EditorFields.Title, screen.FocusedField);
        Assert.Equal("", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void A_snippet_key_changes_only_the_field_that_has_the_cursor()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Title", "");
        screen.Edit(EditorFields.Description, "", 0);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.N);

        Assert.Equal("Title", screen.TextOf(EditorFields.Title));
    }

    [WpfFact]
    public void A_snippet_key_keeps_the_cursor_in_the_field_and_does_not_save_or_close_the_window()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Title");
        screen.Edit(EditorFields.Description, "", 0);

        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.N);

        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.True(screen.IsShowing);
        Assert.Empty(screen.Saved);
    }

    [WpfFact]
    public void One_undo_reverts_a_snippet()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Edit(EditorFields.Description, "hello", 5);
        screen.Press(Key.LeftCtrl, Key.LeftShift, Key.N);

        screen.Undo(EditorFields.Description);

        Assert.Equal("hello", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData(EditorFields.Description)]
    [InlineData(EditorFields.Title)]
    public void Ctrl_K_makes_the_selection_the_text_of_a_link_to_the_URL_on_the_clipboard(string field)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = Url;
        screen.Edit(field, "see docs now", 4, 4);

        screen.Press(Key.LeftCtrl, Key.K);

        Assert.Equal($"see [docs]({Url}) now", screen.TextOf(field));
        Assert.Equal(field, screen.FocusedField);
    }

    [WpfFact]
    public void Ctrl_K_with_no_selection_leaves_the_cursor_in_the_empty_link_text()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = Url;
        screen.Edit(EditorFields.Description, "", 0);

        screen.Press(Key.LeftCtrl, Key.K);

        Assert.Equal($"[]({Url})", screen.TextOf(EditorFields.Description));
        Assert.Equal((1, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_K_trims_the_URL_on_the_clipboard()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = $"  {Url}\n";
        screen.Edit(EditorFields.Description, "docs", 0, 4);

        screen.Press(Key.LeftCtrl, Key.K);

        Assert.Equal($"[docs]({Url})", screen.TextOf(EditorFields.Description));
    }

    [WpfTheory]
    [InlineData("just some words")]
    [InlineData("ftp://example.com/file")]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData(null)]
    public void Ctrl_K_without_a_web_address_on_the_clipboard_leaves_the_cursor_in_the_empty_address(string? clipboard)
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = clipboard;
        screen.Edit(EditorFields.Description, "docs", 0, 4);

        screen.Press(Key.LeftCtrl, Key.K);

        Assert.Equal("[docs]()", screen.TextOf(EditorFields.Description));
        Assert.Equal((7, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Ctrl_K_reads_the_clipboard_when_the_key_is_pressed()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Services.Clipboard.Content = "old";
        screen.Edit(EditorFields.Description, "docs", 0, 4);
        screen.Services.Clipboard.Content = Url;

        screen.Press(Key.LeftCtrl, Key.K);

        Assert.Equal($"[docs]({Url})", screen.TextOf(EditorFields.Description));
    }
}
