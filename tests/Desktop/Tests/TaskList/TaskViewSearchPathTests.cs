using System.Windows.Input;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 2, searching: typing narrows the list at once, "@" offers the contacts and choosing one narrows the list to its mentions,
/// and Esc steps back: it empties the search, then closes Task View. Which words name a contact is in <see cref="ListFilterContactTests"/>.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewSearchPathTests
{
    [WpfFact]
    public void Typing_narrows_the_list_a_chosen_contact_narrows_it_to_its_mentions_and_Esc_empties_the_search_then_closes()
    {
        var services = new ListServices();
        services.UseContacts(
            ["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap"), ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan"));
        services.Add("Call [Old Name](contact:ana \"{fullName}\") today");
        services.Add("Call [Bob Stan](contact:bob \"{fullName}\") today");
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.TypeSearch("milk");
        Assert.Equal(["Buy milk"], screen.RowTitles);
        screen.TypeSearch("");
        Assert.Equal(3, screen.RowTitles.Count);

        screen.TypeInSearch("@ana");
        Assert.Equal(["Ana Pop (anap)"], screen.ShownAliases);
        screen.Press(Key.Down);
        Assert.Empty(screen.SelectedTitles);
        screen.Press(Key.Enter);
        Assert.Equal("@anap", screen.SearchText);
        Assert.Empty(screen.ShownAliases);
        Assert.Equal(["Call Ana Pop today"], screen.RowTitles);

        screen.Press(Key.Escape);
        Assert.Equal("", screen.SearchText);
        Assert.Equal(3, screen.RowTitles.Count);
        Assert.True(screen.View.IsOpen);

        screen.PressToClose(Key.Escape);
        Assert.False(screen.View.IsOpen);
    }
}
