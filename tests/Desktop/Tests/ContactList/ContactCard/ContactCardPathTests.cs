using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactCard;

/// <summary>
/// The contact card hosted in a plain window, as the hover card hosts it for a mention, along the paths a user takes: reading it, holding Ctrl for
/// every field, and changing the theme. The fields and their wording are in the unit tests of the card's content.
/// </summary>
[UX("ContactCard")]
[Collection(ScreenTests.Collection)]
public class ContactCardPathTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    [WpfFact]
    public void Reading_the_card_then_holding_Ctrl_for_every_field_and_letting_go()
    {
        using var screen = new ContactCardScreen(_services, "ana");

        // The card of the mention's contact: the full name, the group's heading and the common fields.
        var texts = screen.Texts;
        Assert.Equal("Ana Pop", texts[0]);
        Assert.Equal("Team", texts[1]);
        Assert.Contains("Engineer", texts);
        Assert.Contains("Software Engineer", texts);
        Assert.DoesNotContain("Ctrl: all details", texts);
        Assert.DoesNotContain("Cluj", texts);
        Assert.DoesNotContain("Bob Stan", texts);

        // Ctrl down shows every field, and the card grows in place.
        var before = screen.Height;
        screen.PressCtrl();
        Assert.Contains("Cluj", screen.Texts);
        Assert.Contains("Bob Stan", screen.Texts);
        Assert.True(screen.Height > before);

        // Ctrl up goes back to the common fields.
        screen.ReleaseCtrl();
        Assert.DoesNotContain("Cluj", screen.Texts);
    }

    [WpfFact]
    public void A_card_opened_with_Ctrl_already_held_shows_every_field_and_takes_no_click_or_focus()
    {
        using var screen = new ContactCardScreen(_services, "ana", ctrlHeld: true);

        Assert.Contains("Cluj", screen.Texts);
        Assert.False(screen.TakesAClickOrTheFocus);
    }

    [WpfFact]
    public void The_full_names_heading_color_follows_the_theme_also_while_the_card_shows()
    {
        _services.Theme.IsDark = false;
        using var screen = new ContactCardScreen(_services, "ana");
        var name = screen.Block("Ana Pop");
        var size = name.FontSize;
        Assert.Equal(Heading(dark: false), ContactCardScreen.ColorOf(name));

        _services.Theme.Switch(true);

        name = screen.Block("Ana Pop");
        Assert.Equal(Heading(dark: true), ContactCardScreen.ColorOf(name));
        Assert.Equal(size, name.FontSize);
    }

    private static System.Windows.Media.Color Heading(bool dark) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Memoria.ToDo.Shared.Rendering.TextLooks.Of(dark).HeadingColor(1));
}
