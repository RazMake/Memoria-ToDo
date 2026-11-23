using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>
/// Closing the Contacts window with a changed form, along the paths a user and the system take: the close button and the Close window keys ask,
/// and a plain close from the system doesn't. Real Contacts over an in-memory clone. What each answer does is in the unit tests.
/// </summary>
[UX("ContactsWindow")]
[Collection(ScreenTests.Collection)]
public class ContactsWindowClosePathTests
{
    private readonly ContactServices _services = ContactServices.Typical();

    private static ContactsScreen ChangedForm(ContactServices services)
    {
        var screen = new ContactsScreen(services);
        screen.ClickRow("Bob Stan");
        screen.ClickButton("Edit");
        screen.Type("title", "Lead");
        return screen;
    }

    [WpfFact]
    public void Closing_with_a_changed_form_asks_and_Cancel_keeps_Contacts_open_until_a_plain_close_ends_it()
    {
        _services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = ChangedForm(_services);
        var window = screen.Window;

        // The close button and Alt+F4 ask, and Cancel keeps Contacts open with the form.
        var askedByButton = screen.QuestionsAskedWhile(() => ClosePrompt.PressCloseButton(window), "Cancel");
        Assert.Equal(1, askedByButton);
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal("Designer", _services.Member("team", "bob").Title);

        // The Close window keys ask the same prompt.
        var askedByKeys = screen.QuestionsAskedWhile(() => SimulatedKey.Chord(window, Key.LeftShift, Key.F9), "Cancel");
        Assert.Equal(1, askedByKeys);
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Lead", screen.FieldText("title"));
        Assert.Equal("Designer", _services.Member("team", "bob").Title);

        // A plain close while the prompt shows closes the prompt as Cancel, then closes Contacts and discards the form.
        var prompt = new ClosePrompt("Contacts");
        prompt.Run(() =>
        {
            ClosePrompt.PressCloseButton(window);
            return 0;
        }, _ => ClosePrompt.SendPlainClose(window));
        Assert.Equal(1, prompt.Shown);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }

    [WpfFact]
    public void A_plain_close_from_the_system_with_a_changed_form_closes_without_asking_and_discards_the_form()
    {
        using var screen = ChangedForm(_services);
        var window = screen.Window;
        var prompt = new ClosePrompt("Contacts");

        prompt.Run(() =>
        {
            ClosePrompt.SendPlainClose(window);
            return 0;
        }, ClosePrompt.Click("Cancel"));

        Assert.Equal(0, prompt.Shown);
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Designer", _services.Member("team", "bob").Title);
    }
}
