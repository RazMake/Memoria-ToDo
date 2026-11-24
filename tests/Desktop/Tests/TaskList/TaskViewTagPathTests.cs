using System.Windows.Input;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 7, tagging: the pill shows the tag, a choice from the menu or a clear with the x saves at once with an Undo bar, and a tag
/// chosen during a description edit only changes the pill until the edit's one save. What Tasks saves and Undo restores is in
/// <see cref="TagChangesTests"/>.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewTagPathTests
{
    [WpfFact]
    public void Choosing_clearing_and_undoing_a_tag_and_choosing_one_during_an_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");
        Assert.Equal("home", screen.PillTag("Buy milk"));
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        Assert.Equal(["work"], services.Stored("Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag changed"));
        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.Equal(["work"], screen.GroupNames);
        screen.Click("Undo");
        Assert.Equal(["home"], services.Stored("Buy milk").Tags);
        Assert.Equal("home", screen.PillTag("Buy milk"));
        Assert.False(screen.HasButton("Undo"));

        screen.ClearTag("Buy milk");
        Assert.Empty(services.Stored("Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag cleared"));
        Assert.Equal(TagEditor.NoTagText, screen.PillText("Buy milk"));
        screen.Click("Undo");
        Assert.Equal(["home"], services.Stored("Buy milk").Tags);

        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        Assert.Equal(["home"], services.Stored("Buy milk").Tags);
        Assert.False(screen.HasButton("Undo"));
        var saves = services.CountSaves();
        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Stored("Buy milk");
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Equal(["work"], saved.Tags);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);
        Assert.Equal(["work"], screen.GroupNames);
    }
}
