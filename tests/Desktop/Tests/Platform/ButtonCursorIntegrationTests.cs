using Xunit;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The hand pointer over buttons, in a run of the platform's test program after Run the app's setup.</summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class ButtonCursorIntegrationTests
{
    [Fact]
    public void A_push_button_shows_the_hand_pointer()
    {
        Assert.Contains("cursor Button hand=True", RunScenario());
    }

    [Fact]
    public void A_toggle_button_shows_the_hand_pointer()
    {
        Assert.Contains("cursor ToggleButton hand=True", RunScenario());
    }

    [Fact]
    public void A_check_box_does_not_show_the_hand_pointer()
    {
        Assert.Contains("cursor CheckBox hand=False", RunScenario());
    }

    [Fact]
    public void A_radio_button_does_not_show_the_hand_pointer()
    {
        Assert.Contains("cursor RadioButton hand=False", RunScenario());
    }

    [Fact]
    public void A_scroll_bars_arrows_do_not_show_the_hand_pointer()
    {
        var lines = RunScenario();

        var line = Assert.Single(lines, l => l.StartsWith("cursor ScrollBarArrows "));
        Assert.DoesNotContain("count=0 ", line + " "); // The arrows were found, so the check is not vacuous.
        Assert.EndsWith("hand=0", line);
    }

    private static IReadOnlyList<string> RunScenario()
    {
        using var host = HostProcess.Start("cursor-button-kinds");
        host.WaitForExit();
        return host.Lines;
    }
}
