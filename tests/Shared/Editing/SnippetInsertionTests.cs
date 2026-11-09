using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class SnippetInsertionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 14, 7, 9, TimeSpan.FromHours(2));

    private static string Insert(ISnippet snippet, string marked, SnippetInput? input = null) =>
        Marked.Show(snippet.Insert(Marked.Field(marked), input ?? new SnippetInput()));

    private static TextAndSelection InsertInto(ISnippet snippet, string marked, SnippetInput? input = null) =>
        snippet.Insert(Marked.Field(marked), input ?? new SnippetInput());

    // Missing input

    [Fact]
    public void Date_stamp_rejects_missing_time()
    {
        Assert.ThrowsAny<ArgumentException>(() => Snippets.DateStamp.Insert(Marked.Field("«»"), new SnippetInput()));
    }

    [Fact]
    public void Date_and_time_stamp_rejects_missing_time()
    {
        Assert.ThrowsAny<ArgumentException>(() => Snippets.DateAndTimeStamp.Insert(Marked.Field("«»"), new SnippetInput()));
    }

    [Fact]
    public void Link_rejects_missing_clipboard_text()
    {
        Assert.ThrowsAny<ArgumentException>(() => Snippets.Link.Insert(Marked.Field("«»"), new SnippetInput()));
    }

    [Fact]
    public void Image_rejects_missing_image_markdown()
    {
        Assert.ThrowsAny<ArgumentException>(() => Snippets.Image.Insert(Marked.Field("«»"), new SnippetInput()));
    }

    [Fact]
    public void Task_reference_rejects_missing_task()
    {
        Assert.ThrowsAny<ArgumentException>(() => Snippets.TaskReference.Insert(Marked.Field("«»"), new SnippetInput()));
    }

    [Fact]
    public void A_snippet_rejects_input_that_only_holds_what_another_snippet_needs()
    {
        var onlyClipboard = new SnippetInput { ClipboardText = "https://example.com" };

        Assert.ThrowsAny<ArgumentException>(() => Snippets.DateStamp.Insert(Marked.Field("«»"), onlyClipboard));
    }

    [Fact]
    public void A_snippet_that_needs_nothing_accepts_empty_input()
    {
        Assert.Equal("---«»", Insert(Snippets.HorizontalLine, "«»"));
    }

    // Stamps

    [Fact]
    public void Date_stamp_inserts_the_date_as_year_month_day()
    {
        Assert.Equal("due 2026-03-05«» soon", Insert(Snippets.DateStamp, "due «» soon", new SnippetInput { Now = Now }));
    }

    [Fact]
    public void Date_stamp_replaces_the_selection()
    {
        Assert.Equal("due 2026-03-05«» soon", Insert(Snippets.DateStamp, "due «xx» soon", new SnippetInput { Now = Now }));
    }

    [Fact]
    public void Date_and_time_stamp_inserts_the_date_and_the_time_as_hours_and_minutes()
    {
        Assert.Equal("at 2026-03-05 14:07«»", Insert(Snippets.DateAndTimeStamp, "at «»", new SnippetInput { Now = Now }));
    }

    [Fact]
    public void A_stamp_uses_the_offset_of_the_time_it_is_given_without_converting_it()
    {
        var lateInTheEvening = new DateTimeOffset(2026, 12, 31, 23, 59, 0, TimeSpan.FromHours(-8));

        Assert.Equal("2026-12-31 23:59«»", Insert(Snippets.DateAndTimeStamp, "«»", new SnippetInput { Now = lateInTheEvening }));
    }

    [Fact]
    public void A_stamp_pads_single_digit_months_days_and_hours_with_zeros()
    {
        var early = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero);

        Assert.Equal("2026-01-02 03:04«»", Insert(Snippets.DateAndTimeStamp, "«»", new SnippetInput { Now = early }));
    }

    // Blocks

    [Theory]
    [InlineData("Note", "> [!NOTE]")]
    [InlineData("Tip", "> [!TIP]")]
    [InlineData("Warning", "> [!WARNING]")]
    public void An_alert_snippet_inserts_a_block_that_starts_with_its_alert_line(string which, string firstLine)
    {
        var snippet = which switch
        {
            "Note" => Snippets.Note,
            "Tip" => Snippets.Tip,
            _ => Snippets.Warning,
        };

        var result = InsertInto(snippet, "«»");

        Assert.StartsWith(firstLine, result.Text);
        Assert.Contains("\n> ", result.Text);
    }

    [Fact]
    public void An_alert_snippet_after_text_starts_on_a_line_of_its_own()
    {
        var result = InsertInto(Snippets.Note, "some text«»");

        Assert.Contains("some text\n", result.Text);
        Assert.Contains("\n> [!NOTE]", result.Text);
        Assert.StartsWith("some text", result.Text);
    }

    [Fact]
    public void Code_block_inserts_a_fenced_block_with_the_cursor_inside_it()
    {
        var result = InsertInto(Snippets.CodeBlock, "«»");

        var firstFence = result.Text.IndexOf("```", StringComparison.Ordinal);
        var lastFence = result.Text.LastIndexOf("```", StringComparison.Ordinal);
        Assert.Equal(0, firstFence);
        Assert.True(lastFence > firstFence);
        Assert.Equal(0, result.SelectionLength);
        Assert.True(result.SelectionStart > result.Text.IndexOf('\n'));
        Assert.True(result.SelectionStart <= lastFence);
    }

    [Fact]
    public void Table_inserts_a_pipe_table_with_two_columns()
    {
        var lines = InsertInto(Snippets.Table, "«»").Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToArray();

        Assert.True(lines.Length >= 3, "A header, a separator, and at least one row.");
        Assert.All(lines, line =>
        {
            Assert.StartsWith("|", line);
            Assert.EndsWith("|", line);
            Assert.Equal(3, line.Count(c => c == '|'));
        });
        Assert.Matches(@"^\|\s*:?-+:?\s*\|\s*:?-+:?\s*\|$", lines[1]);
    }

    [Fact]
    public void Horizontal_line_inserts_three_dashes()
    {
        Assert.StartsWith("---", InsertInto(Snippets.HorizontalLine, "«»").Text);
    }

    [Fact]
    public void Horizontal_line_under_text_leaves_a_blank_line_so_the_text_is_not_a_heading()
    {
        var lines = InsertInto(Snippets.HorizontalLine, "some text«»").Text.Split('\n');

        var rule = Array.IndexOf(lines, "---");
        Assert.True(rule >= 2, "The rule is on a line of its own, below the text.");
        Assert.Equal("some text", lines[0]);
        Assert.Equal("", lines[rule - 1]);
    }

    [Fact]
    public void A_block_in_the_middle_of_a_line_starts_on_a_line_of_its_own()
    {
        var result = InsertInto(Snippets.CodeBlock, "before«»after");

        Assert.Contains("before\n", result.Text);
        Assert.Contains("\n```", result.Text);
    }

    // Link

    [Fact]
    public void Link_uses_the_selection_as_the_text_and_a_url_on_the_clipboard_as_the_address()
    {
        var result = Insert(Snippets.Link, "see «docs» now", new SnippetInput { ClipboardText = "https://example.com/a" });

        Assert.StartsWith("see [docs](https://example.com/a)", result.Replace("«", "").Replace("»", ""));
    }

    [Fact]
    public void Link_trims_the_clipboard_url()
    {
        var result = InsertInto(Snippets.Link, "«docs»", new SnippetInput { ClipboardText = "  http://example.com/a \r\n" });

        Assert.Equal("[docs](http://example.com/a)", result.Text);
    }

    [Theory]
    [InlineData("just some words")]
    [InlineData("ftp://example.com/a")]
    [InlineData("example.com/a")]
    [InlineData("")]
    public void Link_leaves_the_cursor_in_an_empty_address_when_the_clipboard_has_no_url(string clipboard)
    {
        var result = Insert(Snippets.Link, "see «docs» now", new SnippetInput { ClipboardText = clipboard });

        Assert.StartsWith("see [docs](«»)", result);
    }

    // Image

    [Fact]
    public void Image_inserts_the_image_markdown_exactly_as_given()
    {
        var result = Insert(Snippets.Image, "a «»b", new SnippetInput { ImageMarkdown = "![shot](images/x 1.png)" });

        Assert.Equal("a ![shot](images/x 1.png)«»b", result);
    }

    // Task reference

    [Fact]
    public void Task_reference_links_the_title_to_the_tasks_address()
    {
        var input = new SnippetInput { Task = new TaskToReference("task:0abc", "Buy milk") };

        Assert.Equal("see [Buy milk](task:0abc)«»", Insert(Snippets.TaskReference, "see «»", input));
    }

    [Fact]
    public void Task_reference_puts_a_backslash_before_each_bracket_and_backslash_in_the_title()
    {
        var input = new SnippetInput { Task = new TaskToReference("task:0abc", @"fix [a] \ b") };

        Assert.Equal(@"[fix \[a\] \\ b](task:0abc)«»", Insert(Snippets.TaskReference, "«»", input));
    }

    [Fact]
    public void Task_reference_keeps_the_Markdown_of_the_title_in_the_link_text()
    {
        var input = new SnippetInput { Task = new TaskToReference("task:0abc", "*urgent* milk") };

        Assert.Equal("[*urgent* milk](task:0abc)«»", Insert(Snippets.TaskReference, "«»", input));
    }

    [Fact]
    public void Task_reference_escapes_only_the_brackets_and_backslashes_around_kept_Markdown()
    {
        var input = new SnippetInput { Task = new TaskToReference("task:0abc", @"**fix** [a ~~b~~ \") };

        Assert.Equal(@"[**fix** \[a ~~b~~ \\](task:0abc)«»", Insert(Snippets.TaskReference, "«»", input));
    }

    [Fact]
    public void Task_reference_replaces_the_selection()
    {
        var input = new SnippetInput { Task = new TaskToReference("task:0abc", "Buy milk") };

        Assert.Equal("see [Buy milk](task:0abc)«»", Insert(Snippets.TaskReference, "see «old»", input));
    }

    // Purity

    [Fact]
    public void Inserting_twice_with_the_same_field_and_input_gives_the_same_result()
    {
        var field = Marked.Field("due «»");
        var input = new SnippetInput { Now = Now };

        Assert.Equal(Snippets.DateStamp.Insert(field, input), Snippets.DateStamp.Insert(field, input));
    }
}
