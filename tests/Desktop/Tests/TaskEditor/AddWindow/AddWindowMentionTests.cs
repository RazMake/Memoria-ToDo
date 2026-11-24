using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>
/// Mentions in the Add window: typing "@" lists the contacts, choosing inserts a mention, a mention shows as one chip, and
/// the cursor, Backspace, Save, and copy treat the chip as one character. The picking rules, the chip rules and the hover
/// card's lookup are in the unit tests.
/// </summary>
[UX("AddWindow")]
[Collection(ScreenTests.Collection)]
public class AddWindowMentionTests
{
    private const string Ana = "[Ana Pop](contact:ana \"{fullName}\")";
    private const string AnaOld = "[Old Name](contact:ana \"{fullName}\")";

    [WpfFact]
    public async Task Path_8_the_at_sign_lists_contacts_to_choose_and_a_mention_is_one_chip_that_saves_as_Markdown()
    {
        using var screen = new AddScreen();
        screen.Services.UseContacts(
            ["{fullName} ({title})", "{firstName}"],
            ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana"), ("title", "Engineer"), ("alias", "anap")),
            ContactFiles.Person("bob", "Bob Stan", ("firstName", "Bob")));
        var task = screen.Services.AddTaskTitled($"Call {AnaOld} today");
        screen.Show();

        // Typing narrows the list; Down and Enter choose.
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@");
        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], screen.ShownMentions);
        Assert.Equal("@", screen.TextOf(EditorFields.Description));
        screen.Type("bo");
        Assert.Equal(["Bob Stan (bob)"], screen.ShownMentions);
        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal("[Bob Stan](contact:bob \"{fullName} ({title})\")", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Empty(screen.ShownMentions);

        // A click chooses in the Title.
        screen.Edit(EditorFields.Title, "Call ", 5);
        screen.Type("@");
        screen.ClickMention("Bob Stan (bob)");

        Assert.Equal("Call [Bob Stan](contact:bob \"{fullName} ({title})\")", screen.TextOf(EditorFields.Title));
        Assert.Empty(screen.ShownMentions);

        // Esc closes the list first and keeps the typed text.
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@an");
        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownMentions);
        Assert.True(screen.IsShowing);
        Assert.Equal("@an", screen.TextOf(EditorFields.Description));

        // Tab and a click on the header show the highlighted contact in the next template.
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@");
        Assert.Equal("Ana Pop (Engineer)", screen.MentionHeaderText);
        screen.Press(Key.Tab);

        Assert.Equal("Ana", screen.MentionHeaderText);
        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], screen.ShownMentions);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        screen.ClickMentionHeader();
        Assert.Equal("Ana Pop (Engineer)", screen.MentionHeaderText);
        screen.Press(Key.Escape);

        // The list opens in a popup under the at sign.
        screen.Edit(EditorFields.Description, "first line\n\n\n\n\n\n\n\nlast", 0);
        screen.Type("@");

        Assert.True(screen.MentionListIsInAPopup, "The list is inside the window, not in a popup.");
        var (left, lineBottom) = screen.PlaceOnScreen(EditorFields.Description, 0);
        var line = screen.LineHeightOnScreen(EditorFields.Description);
        var list = screen.MentionListOnScreen;
        Assert.InRange(list.Top, lineBottom - 2, lineBottom + line);
        Assert.InRange(list.Left, left - 24, left + 24);
        screen.Press(Key.Escape);

        // A mention shows as a chip with the contact's live text, and Markdown stays in the text.
        screen.Edit(EditorFields.Description, AnaOld, 0);
        screen.Window.UpdateLayout();
        await RenderedField.Settled();
        var live = RenderedField.Of(screen.Window, EditorFields.Description);
        Assert.True(live.ShowsBackdrop(TextLooks.Light.MentionBackground, 1.0));
        Assert.Equal(AnaOld, screen.TextOf(EditorFields.Description));

        // The cursor, Backspace, Delete, undo and copy treat the chip as one character.
        var text = $"a {Ana} b";
        screen.Edit(EditorFields.Description, text, 2);
        screen.Press(Key.Right);
        Assert.Equal((2 + Ana.Length, 0), screen.SelectionOf(EditorFields.Description));
        screen.Press(Key.Left);
        Assert.Equal((2, 0), screen.SelectionOf(EditorFields.Description));
        screen.Press(Key.Delete);
        Assert.Equal("a  b", screen.TextOf(EditorFields.Description));
        screen.Undo(EditorFields.Description);
        Assert.Equal(text, screen.TextOf(EditorFields.Description));
        screen.Edit(EditorFields.Description, text, 2 + Ana.Length);
        screen.Press(Key.Back);
        Assert.Equal("a  b", screen.TextOf(EditorFields.Description));
        screen.Undo(EditorFields.Description);
        Assert.Equal(text, screen.TextOf(EditorFields.Description));
        screen.Edit(EditorFields.Description, text, 0, 2 + Ana.Length);
        Clipboard.Clear();
        screen.Copy(EditorFields.Description);
        Assert.Equal($"a {Ana}", Clipboard.GetText());

        // A task reference lists a title's mention as the contact's live text.
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);
        Assert.Equal(["Call Ana Pop today"], screen.ShownTaskReferences);
        screen.ClickTaskReference("Call Ana Pop today");
        Assert.Equal($"[Call Ana Pop today](task:{task.Id})", screen.TextOf(EditorFields.Description));

        // Save keeps each mention's Markdown with its stored text.
        screen.Fill($"Call {AnaOld}", $"cc {Ana}");
        screen.Save();

        var saved = Assert.Single(screen.Saved, item => item.Id != task.Id);
        Assert.Equal($"Call {AnaOld}", saved.Title);
        Assert.Equal($"cc {Ana}", saved.Description);
    }
}
