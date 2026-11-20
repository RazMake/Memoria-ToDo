using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>The contact card on screen: what it shows by default and with Ctrl, how it switches, and that nothing on it takes a click.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ContactCardScreenTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    [WpfFact]
    public void Shows_the_full_name_the_groups_heading_and_the_common_fields()
    {
        using var screen = new ContactCardScreen(_services, "ana");

        var texts = screen.Texts;

        Assert.Equal("Ana Pop", texts[0]);
        Assert.Equal("Team", texts[1]);
        Assert.Contains("Engineer", texts);
        Assert.Contains("Software Engineer", texts);
        Assert.DoesNotContain("Ctrl: all details", texts);
    }

    [WpfFact]
    public void Leaves_out_the_fields_only_Ctrl_shows()
    {
        using var screen = new ContactCardScreen(_services, "ana");

        Assert.DoesNotContain("Cluj", screen.Texts);
        Assert.DoesNotContain("Bob Stan", screen.Texts);
    }

    [WpfFact]
    public void Pressing_Ctrl_shows_every_field_and_grows_the_card_in_place()
    {
        using var screen = new ContactCardScreen(_services, "ana");
        var before = screen.Height;

        screen.PressCtrl();

        Assert.Contains("Cluj", screen.Texts);
        Assert.Contains("Bob Stan", screen.Texts);
        Assert.True(screen.Height > before);
    }

    [WpfFact]
    public void Releasing_Ctrl_goes_back_to_the_common_fields()
    {
        using var screen = new ContactCardScreen(_services, "ana");
        screen.PressCtrl();

        screen.ReleaseCtrl();

        Assert.DoesNotContain("Cluj", screen.Texts);
    }

    [WpfFact]
    public void Ctrl_already_held_at_open_shows_every_field_at_once()
    {
        using var screen = new ContactCardScreen(_services, "ana", ctrlHeld: true);

        Assert.Contains("Cluj", screen.Texts);
    }

    [WpfFact]
    public void Shows_only_the_heading_for_a_contact_with_no_common_field()
    {
        using var screen = new ContactCardScreen(_services, "cris");

        Assert.Equal(["Cris Ilie", "Ex-Team"], screen.Texts);
    }

    [WpfFact]
    public void Nothing_on_the_card_takes_a_click_or_the_focus_not_even_Reports_to_or_Direct_reports()
    {
        using var screen = new ContactCardScreen(_services, "ana", ctrlHeld: true);

        Assert.False(screen.TakesAClickOrTheFocus);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_the_full_name_in_the_first_heading_color_of_the_current_theme(bool dark)
    {
        _services.Theme.IsDark = dark;
        using var screen = new ContactCardScreen(_services, "ana");

        Assert.Equal(Heading(dark), ContactCardScreen.ColorOf(screen.Block("Ana Pop")));
    }

    [WpfFact]
    public void Switches_the_full_names_heading_color_when_the_theme_changes_while_the_card_shows_and_keeps_its_size()
    {
        using var screen = new ContactCardScreen(_services, "ana");
        var size = screen.Block("Ana Pop").FontSize;

        _services.Theme.Switch(true);

        var name = screen.Block("Ana Pop");
        Assert.Equal(Heading(true), ContactCardScreen.ColorOf(name));
        Assert.Equal(size, name.FontSize);
    }

    private static System.Windows.Media.Color Heading(bool dark) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Memoria.ToDo.Shared.Rendering.TextLooks.Of(dark).HeadingColor(1));
}
