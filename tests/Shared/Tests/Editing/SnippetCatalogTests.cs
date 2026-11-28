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
    public void Needs_what_the_design_table_says_for_each_snippet_in_the_pickers_order()
    {
        // Date stamp, date and time stamp, note, tip, warning, link, image, code block, task reference, table, horizontal line.
        SnippetNeeds[] expected =
        [
            SnippetNeeds.Time, SnippetNeeds.Time, SnippetNeeds.None, SnippetNeeds.None, SnippetNeeds.None,
            SnippetNeeds.ClipboardText, SnippetNeeds.ImageMarkdown, SnippetNeeds.None, SnippetNeeds.TaskToReference,
            SnippetNeeds.None, SnippetNeeds.None,
        ];

        Assert.Equal(expected, Snippets.All.Select(s => s.Needs));
    }
}
