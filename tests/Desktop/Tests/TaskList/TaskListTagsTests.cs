using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>Tags in Task View: the group headings, the selected task's tag control, the Undo bar, and changes from Sync.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListTagsTests
{
    private static TaskItem Only(ListServices services, string title) => services.Tasks.All.Single(task => task.Title == title);

    private static void ChangeTagsElsewhere(ListServices services, string title, params string[] tags)
    {
        var started = Only(services, title);
        services.Tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, started.Description, tags));
    }

    [WpfFact]
    public void The_selected_task_shows_its_tags_as_badges_in_order()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home", "errand");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");

        Assert.Equal(["home", "errand"], screen.BadgeTags("Buy milk"));
    }

    [WpfFact]
    public void The_filter_offers_the_top_tags_and_the_all_tags_list_offers_every_tag()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work", "rare");
        using var screen = new TaskListScreen(services);

        Assert.Contains("home", screen.ChipTags);
        Assert.Contains("work", screen.ChipTags);
        Assert.Equal(["home", "rare", "work"], screen.AllTags.Order());
    }

    [WpfFact]
    public void Choosing_a_filter_tag_filters_and_choosing_it_again_clears_the_filter()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);

        screen.ClickChip("work");
        Assert.Equal(["Call Ana"], screen.RowTitles);
        screen.ClickChip("work");

        Assert.Equal(2, screen.RowTitles.Count);
    }

    [WpfFact]
    public void The_selected_open_task_shows_the_tag_control()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        Assert.False(screen.ShowsTagControl("Buy milk"));

        screen.ClickRow("Buy milk");

        Assert.True(screen.ShowsTagControl("Buy milk"));
        Assert.Equal(["home"], screen.ControlTags("Buy milk"));
    }

    [WpfFact]
    public void The_selected_resolved_task_shows_the_tag_control()
    {
        var services = new ListServices();
        services.AddResolved("Old job", "", "home");
        using var screen = new TaskListScreen(services);
        screen.Click(screen.ResolvedLabel!);

        screen.ClickRow("Old job");

        Assert.True(screen.ShowsTagControl("Old job"));
    }

    [WpfFact]
    public void During_an_edit_only_the_editors_own_tag_control_shows()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Press(Key.F2);

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal(1, screen.TagControlCount);
        Assert.Equal(["home"], screen.ControlTags("Buy milk"));
    }

    [WpfFact]
    public void Removing_a_tag_in_the_tag_control_saves_it_and_offers_Undo_which_restores_it_at_the_end()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home", "errand");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.RemoveTag("Buy milk", "home");

        Assert.Equal(["errand"], Only(services, "Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag removed"));
        screen.Click("Undo");
        Assert.Equal(["errand", "home"], Only(services, "Buy milk").Tags);
        Assert.Equal(["errand", "home"], screen.ControlTags("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Adding_a_tag_from_the_menu_saves_it_and_shows_the_Tag_added_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenPlusMenu("Buy milk");

        screen.ChooseMenuTag("work");

        Assert.Equal(["home", "work"], Only(services, "Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag added"));
    }

    [WpfFact]
    public void A_failed_tag_change_reverts_the_badges_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.RemoveTag("Buy milk", "home");

        Assert.Equal(["home"], screen.ControlTags("Buy milk"));
        Assert.Equal(["home"], screen.BadgeTags("Buy milk").Distinct());
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }

    [WpfFact]
    public void A_sync_that_changes_the_tags_updates_the_badges()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        ChangeTagsElsewhere(services, "Buy milk", "home", "errand");

        Assert.Equal(["home", "errand"], screen.BadgeTags("Buy milk"));
    }

    [WpfFact]
    public void A_sync_that_changes_the_tags_keeps_an_open_menu_open()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenPlusMenu("Buy milk");

        ChangeTagsElsewhere(services, "Buy milk", "home", "errand");
        screen.Settle();

        Assert.True(screen.PlusMenuIsOpen);
        Assert.Equal(["home", "errand"], screen.ControlTags("Buy milk"));
    }

    [WpfFact]
    public void A_sync_that_changes_the_tags_keeps_Custom_and_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenPlusMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");

        ChangeTagsElsewhere(services, "Buy milk", "home", "errand");
        screen.Settle();

        Assert.True(screen.CustomIsOpen);
        Assert.Equal("pend", screen.EditedTagsText);
    }

    [WpfFact]
    public void Selecting_another_task_drops_Custom_and_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenPlusMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");

        screen.ClickRow("Call Ana");

        Assert.False(screen.CustomIsOpen);
    }
}
