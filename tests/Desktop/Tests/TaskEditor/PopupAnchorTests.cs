using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Where the "@" list's popup sits: under the line that holds the "@", lined up with the "@"'s left edge, while any of the "@" is in
/// the field's view, and nowhere (the list closes) once the field scrolls it out of view. The popup places itself under the
/// rectangle the rule gives, so WPF's popup placement keeps it on the screen. Both rectangles are in the field's coordinates.
/// </summary>
[Unit]
public class PopupAnchorTests
{
    private static readonly Rect View = new(0, 0, 400, 100);

    [Fact]
    public void An_at_sign_in_view_gives_its_own_box_so_the_popup_opens_under_its_line_at_its_left_edge()
    {
        var at = new Rect(40, 30, 9, 18);

        Assert.Equal(at, PopupAnchor.At(at, View));
    }

    [Fact]
    public void An_at_sign_partly_in_view_still_gives_its_box()
    {
        var at = new Rect(40, 90, 9, 18);

        Assert.Equal(at, PopupAnchor.At(at, View));
    }

    [Fact]
    public void An_at_sign_scrolled_out_above_the_view_gives_nothing()
    {
        Assert.Null(PopupAnchor.At(new Rect(40, -30, 9, 18), View));
    }

    [Fact]
    public void An_at_sign_scrolled_out_below_the_view_gives_nothing()
    {
        Assert.Null(PopupAnchor.At(new Rect(40, 130, 9, 18), View));
    }

    [Fact]
    public void An_at_sign_scrolled_out_to_the_side_of_the_view_gives_nothing()
    {
        Assert.Null(PopupAnchor.At(new Rect(-50, 30, 9, 18), View));
        Assert.Null(PopupAnchor.At(new Rect(450, 30, 9, 18), View));
    }

    [Fact]
    public void An_at_sign_that_only_touches_the_views_edge_counts_as_out_of_view()
    {
        Assert.Null(PopupAnchor.At(new Rect(40, -18, 9, 18), View));
        Assert.Null(PopupAnchor.At(new Rect(40, 100, 9, 18), View));
    }
}
