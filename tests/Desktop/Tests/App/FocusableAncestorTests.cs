using System.Windows.Controls;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class FocusableAncestorTests
{
    [WpfFact]
    public void Chooses_the_nearest_focusable_ancestor_rather_than_the_browser_or_an_outer_host()
    {
        var browser = new Border { Focusable = true };
        var nearest = new Border { Focusable = true, Child = browser };
        _ = new Border { Focusable = true, Child = nearest };

        Assert.Same(nearest, FocusableAncestor.Of(browser));
    }

    [WpfFact]
    public void Passes_over_nonfocusable_layout_containers()
    {
        var browser = new Border();
        var layout = new Border { Child = browser };
        var host = new Border { Focusable = true, Child = layout };

        Assert.Same(host, FocusableAncestor.Of(browser));
    }

    [WpfFact]
    public void Passes_over_disabled_ancestors()
    {
        var browser = new Border();
        var disabled = new Border { Focusable = true, IsEnabled = false, Child = browser };
        var host = new Border { Focusable = true, Child = disabled };

        Assert.Same(host, FocusableAncestor.Of(browser));
    }

    [WpfFact]
    public void Reports_when_no_ancestor_can_accept_focus()
    {
        var browser = new Border();
        _ = new Border { Child = browser };

        Assert.Null(FocusableAncestor.Of(browser));
    }
}
