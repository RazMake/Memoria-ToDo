using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.SearchBand;

using SearchBand = Memoria.ToDo.Desktop.Platform.SearchBand;

/// <summary>
/// The search band's text and the event that reports it changing. A user's edit is made on the box itself, with the text
/// box's own editing call, since the band shows no box to the caller.
/// </summary>
[Unit]
public class SearchBandTextTests
{
    private readonly SearchBand _band = new();
    private int _changes;

    public SearchBandTextTests()
    {
        _band.TextChanged += (_, _) => _changes++;
    }

    private TextBox Box => OpenWindows.Descendants(_band).OfType<TextBox>().Single();

    [WpfFact]
    public void The_text_starts_empty()
    {
        Assert.Equal(string.Empty, _band.Text);
    }

    [WpfFact]
    public void A_change_the_user_types_raises_TextChanged_and_shows_in_Text()
    {
        Box.AppendText("milk");

        Assert.Equal("milk", _band.Text);
        Assert.Equal(1, _changes);
    }

    [WpfFact]
    public void Setting_Text_changes_the_box_and_raises_TextChanged()
    {
        _band.Text = "milk";

        Assert.Equal("milk", Box.Text);
        Assert.Equal("milk", _band.Text);
        Assert.Equal(1, _changes);
    }

    [WpfFact]
    public void Setting_Text_to_empty_clears_the_box_and_raises_TextChanged()
    {
        _band.Text = "milk";

        _band.Text = string.Empty;

        Assert.Equal(string.Empty, Box.Text);
        Assert.Equal(2, _changes);
    }

    [WpfFact]
    public void Setting_Text_to_null_is_rejected_and_leaves_the_text_alone()
    {
        _band.Text = "milk";

        Assert.Throws<ArgumentNullException>(() => _band.Text = null!);

        Assert.Equal("milk", _band.Text);
        Assert.Equal(1, _changes);
    }
}
