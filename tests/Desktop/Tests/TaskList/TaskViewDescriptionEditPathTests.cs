using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 6, editing a description on the right side: F2 opens the editor with the text, the Preview tab shows the draft formatted and
/// the Edit tab brings the editor back, the Save keys save, Esc discards a changed edit with the Undo bar, Undo reopens it, and
/// selecting another task leaves the edit, and a changed tag list alone counts as a change that Esc discards with the Undo bar.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewDescriptionEditPathTests
{
    [WpfFact]
    public void Editing_previewing_saving_discarding_and_undoing_a_description()
    {
        var services = new ListServices();
        services.Add("Call Ana", "About the invoice");
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.F2);
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk", screen.EditedDescriptionText);
        Assert.Equal(["Edit", "Preview"], screen.TabHeaders);
        screen.TypeDescription("Oat milk, two litres");
        screen.ClickTab("Preview");
        Assert.Contains("Oat milk, two litres", screen.Web.Page);
        screen.ClickTab("Edit");
        Assert.Equal("Oat milk, two litres", screen.EditedDescriptionText);

        screen.Press(Key.LeftCtrl, Key.Enter);
        Assert.Equal("Oat milk, two litres", services.Stored("Buy milk").Description);
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.False(screen.HasButton("Undo"));

        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Changed");
        screen.Press(Key.Escape);
        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal("Oat milk, two litres", services.Stored("Buy milk").Description);

        screen.Click("Undo");
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Changed", screen.EditedDescriptionText);

        screen.ClickRow("Call Ana");
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["Call Ana"], screen.SelectedTitles);
        Assert.Contains("About the invoice", screen.Web.Page);

        screen.PressOn("Call Ana", Key.F2);
        screen.OpenAddTagMenu("Call Ana");
        screen.ChooseMenuTag("home");
        Assert.Equal(["home"], screen.PillTags("Call Ana"));
        screen.Press(Key.Escape);
        Assert.False(screen.IsEditingAny);
        Assert.Empty(services.Stored("Call Ana").Tags);
        screen.Click("Undo");
        Assert.True(screen.IsEditing("Call Ana"));
        Assert.Equal(["home"], screen.PillTags("Call Ana"));
        Assert.Empty(services.Stored("Call Ana").Tags);
    }
}
