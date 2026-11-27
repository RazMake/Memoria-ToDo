using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 7, tagging: the pills show the task's tags in order, adding, removing, replacing a tag saves at once with an Undo bar that
/// says which, the task moves to its new first tag's group and stays selected in view, a save that fails puts the pills back, and
/// a tag added during a description edit only changes the pills until the edit's one save, and that save stops at 15 tags, keeping
/// the edit and the text typed in Custom. What Tasks saves, what each bar says,
/// and what Undo restores is in <see cref="TagChangesTests"/>.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewTagPathTests
{
    [WpfFact]
    public void Changing_tags_moves_the_task_by_its_first_tag_a_failed_save_restores_the_pills_and_an_edit_saves_the_tags_with_its_text()
    {
        var services = new ListServices();
        services.Add("Call Ana", "", "work");
        for (var i = 0; i < 40; i++)
        {
            services.Add($"Filler {i}", "", "b-fill");
        }

        services.Add("Buy milk", "Oat milk", "a-home", "errand");
        services.Add("Plan trip", "", ["b-fill", .. Enumerable.Range(2, 13).Select(number => $"z-leg-{number}")]);
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");
        Assert.Equal(["a-home", "errand"], screen.PillTags("Buy milk"));
        Assert.Equal(["a-home", "b-fill", "work"], screen.GroupNames);

        screen.OpenAddTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        Assert.Equal(["a-home", "errand", "work"], services.Stored("Buy milk").Tags);
        Assert.Equal(["a-home", "errand", "work"], screen.PillTags("Buy milk"));
        Assert.True(screen.ShowsText("Tag added"));
        Assert.Equal(["a-home", "b-fill", "work"], screen.GroupNames);
        screen.Click("Undo");
        Assert.Equal(["a-home", "errand"], services.Stored("Buy milk").Tags);
        Assert.False(screen.HasButton("Undo"));

        screen.ClearTag("Buy milk", "errand");
        Assert.Equal(["a-home"], screen.PillTags("Buy milk"));
        Assert.True(screen.ShowsText("Tag removed"));
        screen.Click("Undo");
        Assert.Equal(["a-home", "errand"], screen.PillTags("Buy milk"));

        screen.OpenTagMenu("Buy milk", "a-home");
        screen.ChooseMenuTag("work");
        Assert.Equal(["work", "errand"], services.Stored("Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag changed"));
        Assert.Equal(["b-fill", "work"], screen.GroupNames);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);
        Assert.Contains("Buy milk", screen.RowTitlesIn("work"));
        Assert.True(screen.RowIsInView("Buy milk"));
        screen.Click("Undo");
        Assert.Equal(["a-home", "errand"], services.Stored("Buy milk").Tags);
        Assert.Equal(["a-home", "b-fill", "work"], screen.GroupNames);
        Assert.Equal(["Buy milk"], screen.SelectedTitles);

        services.Files.FailWritesUnder(services.TaskFolder);
        screen.ClearTag("Buy milk", "errand");
        Assert.Equal(["a-home", "errand"], screen.PillTags("Buy milk"));
        Assert.Equal(["a-home", "errand"], services.Stored("Buy milk").Tags);
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        services.Files.StopFailing();

        services.NoticesFail = true;
        screen.ClearTag("Buy milk", "errand");
        services.NoticesFail = false;
        Assert.True(screen.ShowsText("Saved, but the app couldn't finish updating: Changed failed."));
        Assert.Equal(["a-home"], services.Stored("Buy milk").Tags);
        Assert.Equal(["a-home"], screen.PillTags("Buy milk"));
        Assert.True(screen.ShowsText("Tag removed"));

        services.NoticesFail = true;
        screen.Click("Undo");
        services.NoticesFail = false;
        Assert.Equal(["a-home", "errand"], services.Stored("Buy milk").Tags);
        Assert.Equal(["a-home", "errand"], screen.PillTags("Buy milk"));
        Assert.False(screen.HasButton("Undo"));

        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.OpenAddTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        Assert.Equal(["a-home", "errand"], services.Stored("Buy milk").Tags);
        Assert.False(screen.HasButton("Undo"));
        var saves = services.CountSaves();
        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = services.Stored("Buy milk");
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Equal(["a-home", "errand", "work"], saved.Tags);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);

        screen.ClickRow("Plan trip");
        screen.PressOn("Plan trip", Key.F2);
        screen.OpenAddTagMenu("Plan trip");
        screen.ChooseCustom();
        screen.TypeTag("errand");
        screen.OpenAddTagMenu("Plan trip");
        screen.ChooseMenuTag("work");
        Assert.Equal(15, screen.PillTags("Plan trip").Count);
        saves = services.CountSaves();
        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.True(screen.IsEditing("Plan trip"));
        Assert.True(screen.CustomIsOpen);
        Assert.Equal("errand", screen.TypedTag);
        Assert.Equal(0, saves());
        Assert.Equal(14, services.Stored("Plan trip").Tags.Count);

        screen.ClearTag("Plan trip", "work");
        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.False(screen.IsEditingAny);
        Assert.Equal(["b-fill", .. Enumerable.Range(2, 13).Select(number => $"z-leg-{number}"), "errand"], services.Stored("Plan trip").Tags);
        Assert.Equal(1, saves());
    }
}
