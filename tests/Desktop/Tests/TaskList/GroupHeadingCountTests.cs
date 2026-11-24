using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The count a collapsed group's heading shows, on a heading with no window: only the number is bold, the count shows only when given
/// text, and the heading takes the hand cursor. When a click collapses or expands a group is in the Task View path tests.
/// </summary>
[Unit]
public class GroupHeadingCountTests
{
    private static GroupHeading Heading(string? tag) => new(tag, new FakeTagColors(), new FakeTheme(), new FakeDragPointer());

    private static TextBlock CountOf(GroupHeading heading) =>
        ((StackPanel)heading.Child).Children.OfType<TextBlock>().Last();

    [WpfTheory]
    [InlineData(null)]
    [InlineData("work")]
    public void A_shown_count_has_only_its_number_bold(string? tag)
    {
        var heading = Heading(tag);

        heading.Show("( 12 )");

        var pieces = CountOf(heading).Inlines.OfType<Run>().ToList();
        Assert.Equal("12", string.Concat(pieces.Where(p => p.FontWeight >= FontWeights.Bold).Select(p => p.Text)));
        Assert.Equal("(  )", string.Concat(pieces.Where(p => p.FontWeight < FontWeights.Bold).Select(p => p.Text)));
    }

    [WpfFact]
    public void The_count_shows_while_it_has_text_and_hides_when_it_is_empty()
    {
        var heading = Heading("work");

        heading.Show("( 2 )");
        Assert.Equal(Visibility.Visible, CountOf(heading).Visibility);

        heading.Show("");
        Assert.Equal(Visibility.Collapsed, CountOf(heading).Visibility);
    }

    [WpfTheory]
    [InlineData(null)]
    [InlineData("work")]
    public void The_heading_takes_the_hand_cursor(string? tag)
    {
        Assert.Equal(Cursors.Hand, Heading(tag).Cursor);
    }
}
