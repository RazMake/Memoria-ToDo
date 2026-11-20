using System.Windows;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.ContactList;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Platform.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Resting the pointer on a chip in the Add window's Title or Description shows the card of the contact the chip names, as the platform's
/// hover card shows it. The editor's <c>PartAt</c> answers what the hover card asks: the chip under a point, boxed, with its card.
/// A mention shown as Markdown, across a line break, is no chip and has no card; nor has a chip whose contact Contacts can't find.
/// When the hover card opens, and where, are the platform's and tested there.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowMentionCardTests
{
    private const string Ana = "[Ana Pop](contact:ana \"{fullName}\")";

    private static readonly List<FrameworkElement> LoadedCards = [];

    private static Action? CardLoaded;

    static AddWindowMentionCardTests() =>
        EventManager.RegisterClassHandler(typeof(ContactCardView), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            LoadedCards.Add((FrameworkElement)sender);
            CardLoaded?.Invoke();
        }));

    private static AddScreen Screen()
    {
        var screen = new AddScreen();
        screen.Services.UseContacts(
            ["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop", ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan"));
        screen.Show();
        return screen;
    }

    private static StyledTextEditor EditorOf(AddScreen screen, string field) => (StyledTextEditor)EditorFields.Field(screen.Window, field);

    // A point in the editor's coordinates just inside the text at the offset, halfway down its line.
    private static Point InsideAt(AddScreen screen, string field, int offset)
    {
        screen.Window.UpdateLayout();
        return EditorFields.InsideAt(EditorOf(screen, field), offset);
    }

    private static IReadOnlyList<string> TextsOf(FrameworkElement card) => EditorFields.TextsOfCard(card);

    [WpfTheory]
    [InlineData(EditorFields.Title)]
    [InlineData(EditorFields.Description)]
    public async Task A_chip_has_the_card_of_the_contact_it_names_boxed_around_the_chip(string field)
    {
        using var screen = Screen();
        screen.Edit(field, $"Ask {Ana} now", 0);
        // Real layouts put the text away from the editor's corner; the box must be in the editor's coordinates all the same.
        EditorOf(screen, field).Padding = new Thickness(40, 30, 0, 0);
        var point = InsideAt(screen, field, 5);

        var part = await EditorOf(screen, field).PartAt(point);

        Assert.NotNull(part);
        Assert.True(part.Box.Contains(point), "The box doesn't hold the chip.");
        Assert.Contains("Ana Pop", TextsOf(part.Card));
    }

    [WpfFact]
    public async Task A_point_on_text_that_is_no_chip_has_no_card()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, $"Ask {Ana} now", 0);

        Assert.Null(await EditorOf(screen, EditorFields.Description).PartAt(InsideAt(screen, EditorFields.Description, 1)));
    }

    [WpfFact]
    public async Task A_mention_shown_as_Markdown_across_a_line_break_has_no_card()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "Ask [Ana\nPop](contact:ana \"{fullName}\") now", 0);

        Assert.Null(await EditorOf(screen, EditorFields.Description).PartAt(InsideAt(screen, EditorFields.Description, 6)));
    }

    [WpfFact]
    public async Task A_chip_whose_contact_Contacts_cannot_find_has_no_card()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, "Ask [Zed Old](contact:zed \"{fullName}\") now", 0);

        Assert.Null(await EditorOf(screen, EditorFields.Description).PartAt(InsideAt(screen, EditorFields.Description, 5)));
    }

    [WpfFact]
    public void Resting_the_pointer_on_a_chip_shows_the_card()
    {
        using var screen = Screen();
        screen.Edit(EditorFields.Description, $"Ask {Ana} now", 0);
        var editor = EditorOf(screen, EditorFields.Description);
        LoadedCards.Clear();
        RealPointer.MoveOver(screen.Window, editor, InsideAt(screen, EditorFields.Description, 5));

        Assert.True(WaitForACard(editor.Dispatcher), "The card didn't show.");
    }

    // Lets the dispatcher work until a contact card is on screen, or ten seconds pass.
    private static bool WaitForACard(Dispatcher dispatcher)
    {
        if (LoadedCards.Count > 0)
        {
            return true;
        }

        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Normal, (_, _) => frame.Continue = false, dispatcher);
        CardLoaded = () => frame.Continue = false;
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            CardLoaded = null;
            timeout.Stop();
        }

        return LoadedCards.Count > 0;
    }
}
