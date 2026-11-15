using System.Windows.Input;
using Memoria.ToDo.Desktop.TaskEditor.Tests;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
[Collection(ScreenTests.Collection)]
public class TaskListFilterTests
{
    [WpfFact]
    public void Choosing_a_chip_shows_only_the_tasks_with_that_tag()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);

        screen.ClickChip("work");

        Assert.Equal(["Call Ana"], screen.RowTitles);
    }

    public static TheoryData<AllTagsInput> EveryWayToPickFromAllTags()
    {
        var inputs = new TheoryData<AllTagsInput>();
        foreach (var input in Enum.GetValues<AllTagsInput>())
        {
            inputs.Add(input);
        }

        return inputs;
    }

    [WpfTheory]
    [MemberData(nameof(EveryWayToPickFromAllTags))]
    public void Choosing_a_tag_only_the_all_tags_list_offers_shows_only_the_tasks_with_that_tag(AllTagsInput input)
    {
        var services = new ListServices();
        AddTasksWithTagsOnlyInAllTagsList(services);
        using var screen = new TaskListScreen(services);
        Assert.DoesNotContain("rare", screen.ChipTags);

        screen.ChooseFromAllTags("rare", input);

        Assert.Equal(["Rare one"], screen.RowTitles);
    }

    [WpfTheory]
    [MemberData(nameof(EveryWayToPickFromAllTags))]
    public void Choosing_the_chosen_tag_again_in_the_all_tags_list_clears_the_filter(AllTagsInput input)
    {
        var services = new ListServices();
        AddTasksWithTagsOnlyInAllTagsList(services);
        using var screen = new TaskListScreen(services);
        screen.ChooseFromAllTags("rare", input);
        Assert.Equal(["Rare one"], screen.RowTitles);

        screen.ChooseFromAllTags("rare", input);

        Assert.Equal(
            ["Common 1b", "Common 1a", "Common 2b", "Common 2a", "Common 3b", "Common 3a", "Common 4b", "Common 4a",
             "Common 5b", "Common 5a", "Common 6b", "Common 6a", "Common 7b", "Common 7a", "Common 8b", "Common 8a", "Rare one"],
            screen.RowTitles);
    }

    // Eight tags with two tasks each fill the seven top badges and leave one over; "rare" has one task, so only the all-tags list offers it.
    internal static void AddTasksWithTagsOnlyInAllTagsList(ListServices services)
    {
        for (var tag = 1; tag <= 8; tag++)
        {
            services.Add($"Common {tag}a", "", $"common{tag}");
            services.Add($"Common {tag}b", "", $"common{tag}");
        }

        services.Add("Rare one", "", "rare");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_all_tags_list_shows_a_tag_outside_the_top_badges_as_a_badge_in_its_kept_color(bool dark)
    {
        var services = new ListServices();
        services.Theme.IsDark = dark;
        AddTasksWithTagsOnlyInAllTagsList(services);
        services.TagColors.Seed("rare", "teal-3");
        using var screen = new TaskListScreen(services);
        screen.OpenAllTagsList();

        var fill = screen.AllTagsBadgeFill("rare");

        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark), fill);
    }

    [WpfFact]
    public void Ctrl_F_gives_the_search_box_the_focus()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.Select("Buy milk");
        Assert.True(screen.RowIsKeyboardTarget("Buy milk"), "Setup: the row should hold the focus before the shortcut.");

        screen.Press(Key.LeftCtrl, Key.F);

        Assert.True(screen.SearchHasFocus);
    }

}
