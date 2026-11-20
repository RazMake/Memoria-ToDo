using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The search band publishes its box, so a part that works on any text box, such as the "@" alias list, can attach to it.
/// The box's text is the band's <see cref="SearchBand.Text"/>.
/// </summary>
[Unit]
public class SearchBandBoxTests
{
    private readonly SearchBand _band = new();
    private int _changes;

    public SearchBandBoxTests()
    {
        _band.TextChanged += (_, _) => _changes++;
    }

    [WpfFact]
    public void The_published_box_is_the_one_the_band_shows()
    {
        Assert.Same(OpenWindows.Descendants(_band).OfType<TextBox>().Single(), _band.Box);
    }

    [WpfFact]
    public void The_published_box_is_the_same_box_each_time()
    {
        Assert.Same(_band.Box, _band.Box);
    }

    [WpfFact]
    public void The_published_box_is_named_Search_for_screen_readers()
    {
        Assert.Same(SearchBandParts.SearchBoxIn(_band), _band.Box);
    }

    [WpfFact]
    public void Setting_Text_shows_in_the_published_box()
    {
        _band.Text = "@anap";

        Assert.Equal("@anap", _band.Box.Text);
    }

    [WpfFact]
    public void A_change_to_the_published_box_shows_in_Text_and_raises_TextChanged()
    {
        _band.Box.Text = "milk";

        Assert.Equal("milk", _band.Text);
        Assert.Equal(1, _changes);
    }
}
