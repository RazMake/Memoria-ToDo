using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The Save shortcut in a title edit while the keyboard focus is in the tag control, which is on the right side, not in the
/// Title field: it saves the edit as it does in a description edit, with the title and the pill's tag in one save.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListTitleEditTagKeysTests
{
    private static TaskListScreen EditingTitleWithTagChosen(ListServices services)
    {
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        return screen;
    }

    [WpfFact]
    public void The_Save_shortcut_with_the_focus_on_the_pill_saves_the_title_and_the_pills_tag_in_one_save()
    {
        var services = new ListServices();
        using var screen = EditingTitleWithTagChosen(services);
        screen.FocusTagPill("Buy milk");
        var saves = services.CountSaves();

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Stored("Buy oat milk");
        Assert.Equal(["work"], saved.Tags);
        Assert.Equal("Oat milk", saved.Description);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void The_Save_shortcut_with_the_focus_in_Custom_saves_the_title_and_chooses_the_typed_text_as_the_tag_in_one_save()
    {
        var services = new ListServices();
        using var screen = EditingTitleWithTagChosen(services);
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");
        var saves = services.CountSaves();

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Stored("Buy oat milk");
        Assert.Equal(["pend"], saved.Tags);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);
    }
}
