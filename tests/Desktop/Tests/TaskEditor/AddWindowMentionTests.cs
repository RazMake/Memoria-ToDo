using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Mentions in the Add window, on the common paths: typing "@" opens the contact list and choosing inserts a mention, a mention
/// shows as one chip with the contact's live text, and the cursor, Backspace, Save, and copy treat the chip as one character.
/// A test finds the list by the automation id "MentionList". The picking rules and the chip rules are in the unit tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowMentionTests
{
    private const string Ana = "[Ana Pop](contact:ana \"{fullName}\")";
    private const string AnaOld = "[Old Name](contact:ana \"{fullName}\")";

    private static AddScreen Screen()
    {
        var screen = new AddScreen();
        screen.Services.UseContacts(
            ["{fullName} ({title})", "{firstName}"],
            ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana"), ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan", ("firstName", "Bob")));
        screen.Show();
        return screen;
    }

    private static async Task<RenderedField> Look(AddScreen screen, string field, string text)
    {
        screen.Edit(field, text, 0);
        screen.Window.UpdateLayout();
        await RenderedField.Settled();
        return RenderedField.Of(screen.Window, field);
    }

    [WpfTheory]
    [InlineData(EditorFields.Title)]
    [InlineData(EditorFields.Description)]
    public void Typing_an_at_sign_lists_the_contacts_by_full_name_and_leaves_the_at_sign_in_the_text(string field)
    {
        using var screen = Screen();
        screen.Edit(field, "", 0);

        screen.Type("@");

        Assert.Equal(["Ana Pop (Engineer)", "Bob Stan"], screen.ShownMentions);
        Assert.Equal("@", screen.TextOf(field));
    }

    [WpfFact]
    public void Typing_after_the_at_sign_narrows_the_list()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("@bo");

        Assert.Equal(["Bob Stan"], screen.ShownMentions);
    }

    [WpfFact]
    public void Down_then_Enter_replaces_the_at_sign_and_what_was_typed_with_the_mention()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@bo");

        screen.Press(Key.Down);
        screen.Press(Key.Enter);

        Assert.Equal("[Bob Stan](contact:bob \"{fullName} ({title})\")", screen.TextOf(EditorFields.Description));
        Assert.Equal(EditorFields.Description, screen.FocusedField);
        Assert.Empty(screen.ShownMentions);
    }

    [WpfFact]
    public void Clicking_a_contact_inserts_its_mention_and_closes_the_list()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Title, "Call ", 5);
        screen.Type("@");

        screen.ClickMention("Bob Stan");

        Assert.Equal("Call [Bob Stan](contact:bob \"{fullName} ({title})\")", screen.TextOf(EditorFields.Title));
        Assert.Empty(screen.ShownMentions);
    }

    [WpfFact]
    public void Esc_closes_the_list_first_and_keeps_the_typed_text()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@an");

        screen.Press(Key.Escape);

        Assert.Empty(screen.ShownMentions);
        Assert.True(screen.IsShowing);
        Assert.Equal("@an", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Tab_shows_the_contacts_in_the_next_template_and_keeps_the_focus_in_the_field()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@");

        screen.Press(Key.Tab);

        Assert.Equal(["Ana", "Bob"], screen.ShownMentions);
        Assert.Equal(EditorFields.Description, screen.FocusedField);
    }

    [WpfFact]
    public void The_list_shows_the_title_and_the_current_template_in_the_help_color()
    {
        using var screen = Screen();
        screen.UseTheme(dark: false);
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@");
        var help = screen.Window.FindResource("TextFillColorSecondaryBrush");

        var blocks = screen.MentionListBlocks;

        var title = blocks.Single(block => OpenWindows.TextOf(block) == "Engineer");
        var hint = blocks.Single(block => OpenWindows.TextOf(block).Contains("Tab: next template"));
        Assert.Equal(help, title.Foreground);
        Assert.Equal(help, hint.Foreground);
        Assert.Contains("{fullName} ({title})", OpenWindows.TextOf(hint));
    }

    [WpfFact]
    public void A_contact_with_no_title_shows_only_its_text()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "", 0);
        screen.Type("@bo");

        var texts = screen.MentionListBlocks.Select(OpenWindows.TextOf).ToList();

        Assert.DoesNotContain(texts, text => text == "Engineer");
        Assert.Contains("Bob Stan", texts);
    }

    [WpfFact]
    public async Task A_mention_shows_as_a_chip_with_the_live_text_and_no_Markdown()
    {
        using var screen = Screen();

        var live = await Look(screen, EditorFields.Description, AnaOld);
        var textWithChip = screen.TextOf(EditorFields.Description);
        var plain = await Look(screen, EditorFields.Description, "Ana Pop");

        Assert.True(live.ShowsBackdrop(TextLooks.Light.MentionBackground, 1.0));
        Assert.False(plain.ShowsBackdrop(TextLooks.Light.MentionBackground, 1.0));
        Assert.Equal(AnaOld, textWithChip);
    }

    [WpfFact]
    public async Task A_mention_with_no_such_contact_shows_its_stored_text()
    {
        using var screen = Screen();

        var unknown = await Look(screen, EditorFields.Description, "[Zed Old](contact:zed \"{fullName}\")");
        screen.Services.UseContacts(["{fullName}"], ContactFiles.Person("zed", "Zed Old"));
        var stored = await Look(screen, EditorFields.Description, "[Old](contact:zed \"{fullName}\")");

        Assert.Equal(stored.TotalInk(), unknown.TotalInk(), tolerance: stored.TotalInk() / 20);
    }

    [WpfFact]
    public async Task Changing_a_contact_redraws_the_chip_without_changing_the_text()
    {
        using var screen = Screen();
        var before = (await Look(screen, EditorFields.Description, Ana)).TotalInk();

        screen.Services.UseContacts(
            ["{fullName} ({title})", "{firstName}"],
            ContactFiles.Person("ana", "Anastasia Popescu-Ionescu", ("title", "Engineer")));
        await RenderedField.Settled();

        var after = RenderedField.Of(screen.Window, EditorFields.Description).TotalInk();
        Assert.NotEqual(before, after);
        Assert.Equal(Ana, screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Right_arrow_steps_over_a_chip_as_one_character()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, $"a {Ana} b", 2);

        screen.Press(Key.Right);

        Assert.Equal((2 + Ana.Length, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Left_arrow_steps_back_over_a_chip_as_one_character()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, $"a {Ana} b", 2 + Ana.Length);

        screen.Press(Key.Left);

        Assert.Equal((2, 0), screen.SelectionOf(EditorFields.Description));
    }

    [WpfFact]
    public void Backspace_after_a_chip_removes_the_whole_mention_and_one_undo_brings_it_back()
    {
        using var screen = Screen();
        var text = $"a {Ana} b";
        screen.Edit(EditorFields.Description, text, 2 + Ana.Length);

        screen.Press(Key.Back);

        Assert.Equal("a  b", screen.TextOf(EditorFields.Description));
        screen.Undo(EditorFields.Description);
        Assert.Equal(text, screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Delete_before_a_chip_removes_the_whole_mention()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, $"a {Ana} b", 2);

        screen.Press(Key.Delete);

        Assert.Equal("a  b", screen.TextOf(EditorFields.Description));
    }

    [WpfFact]
    public void Save_keeps_each_mentions_Markdown_with_its_stored_text()
    {
        using var screen = Screen();
        screen.Fill($"Call {AnaOld}", $"cc {Ana}");

        screen.Save();

        var saved = Assert.Single(screen.Saved);
        Assert.Equal($"Call {AnaOld}", saved.Title);
        Assert.Equal($"cc {Ana}", saved.Description);
    }

    [WpfFact]
    public void Copying_a_selection_with_a_chip_puts_its_Markdown_on_the_clipboard()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, $"a {Ana} b", 0, 2 + Ana.Length);
        Clipboard.Clear();

        screen.Copy(EditorFields.Description);

        Assert.Equal($"a {Ana}", Clipboard.GetText());
    }

    [WpfFact]
    public void The_list_opens_directly_below_the_Title_when_typing_there()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Title, "", 0);

        screen.Type("@");

        var (list, title, description) = PlacesOf(screen);
        Assert.InRange(list.Top, title.Bottom - 1, description.Top);
    }

    [WpfFact]
    public void The_list_opens_directly_below_the_Description_when_typing_there()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "", 0);

        screen.Type("@");

        var (list, _, description) = PlacesOf(screen);
        Assert.True(list.Top >= description.Bottom - 1, $"The list starts at {list.Top}, above the Description's bottom {description.Bottom}.");
    }

    private static (Rect List, Rect Title, Rect Description) PlacesOf(AddScreen screen)
    {
        screen.Window.UpdateLayout();
        var content = (FrameworkElement)screen.Window.Content;
        Rect Place(string id)
        {
            var element = EditorFields.Field(screen.Window, id);
            return new Rect(element.TranslatePoint(new Point(0, 0), content), new Size(element.ActualWidth, element.ActualHeight));
        }

        return (Place(EditorFields.MentionList), Place(EditorFields.Title), Place(EditorFields.Description));
    }

    [WpfFact]
    public void The_task_reference_list_shows_a_titles_mention_as_the_contacts_live_text()
    {
        using var screen = Screen();
        screen.Services.AddTaskTitled($"Call {AnaOld} today");
        screen.Edit(EditorFields.Description, "", 0);

        screen.PressCtrl(true, Key.R);

        Assert.Equal(["Call Ana Pop today"], screen.ShownTaskReferences);
    }

    [WpfFact]
    public void Choosing_a_task_inserts_its_title_with_each_mention_as_the_contacts_live_text()
    {
        using var screen = Screen();
        var task = screen.Services.AddTaskTitled($"Call {AnaOld} today");
        screen.Edit(EditorFields.Description, "", 0);
        screen.PressCtrl(true, Key.R);

        screen.ClickTaskReference("Call Ana Pop today");

        Assert.Equal($"[Call Ana Pop today](task:{task.Id})", screen.TextOf(EditorFields.Description));
    }
}
