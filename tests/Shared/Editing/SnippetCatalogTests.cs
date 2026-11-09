using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Editing.Tests;

[Unit]
public class SnippetCatalogTests
{
    [Fact]
    public void Lists_the_eleven_snippets_in_the_pickers_order()
    {
        Assert.Equal(
            [
                Snippets.DateStamp, Snippets.DateAndTimeStamp, Snippets.Note, Snippets.Tip, Snippets.Warning,
                Snippets.Link, Snippets.Image, Snippets.CodeBlock, Snippets.TaskReference, Snippets.Table,
                Snippets.HorizontalLine,
            ],
            Snippets.All);
    }

    [Fact]
    public void Gives_every_snippet_a_distinct_non_empty_name()
    {
        var names = Snippets.All.Select(s => s.Name).ToArray();

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Works_in_the_Title_only_for_both_stamps_the_link_and_the_task_reference()
    {
        var inTitle = Snippets.All.Where(s => s.WorksInTitle).ToArray();

        Assert.Equal(
            [Snippets.DateStamp, Snippets.DateAndTimeStamp, Snippets.Link, Snippets.TaskReference],
            inTitle);
    }

    [Fact]
    public void Needs_the_time_for_both_stamps()
    {
        Assert.Equal(SnippetNeeds.Time, Snippets.DateStamp.Needs);
        Assert.Equal(SnippetNeeds.Time, Snippets.DateAndTimeStamp.Needs);
    }

    [Fact]
    public void Needs_the_clipboard_text_for_the_link()
    {
        Assert.Equal(SnippetNeeds.ClipboardText, Snippets.Link.Needs);
    }

    [Fact]
    public void Needs_the_image_markdown_for_the_image()
    {
        Assert.Equal(SnippetNeeds.ImageMarkdown, Snippets.Image.Needs);
    }

    [Fact]
    public void Needs_the_task_to_reference_for_the_task_reference()
    {
        Assert.Equal(SnippetNeeds.TaskToReference, Snippets.TaskReference.Needs);
    }

    [Fact]
    public void Needs_nothing_for_the_blocks_and_the_line()
    {
        ISnippet[] blocks = [Snippets.Note, Snippets.Tip, Snippets.Warning, Snippets.CodeBlock, Snippets.Table, Snippets.HorizontalLine];

        Assert.All(blocks, snippet => Assert.Equal(SnippetNeeds.None, snippet.Needs));
    }
}
