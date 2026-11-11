using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

[UX]
public class TaskListFilterTests
{
    [WpfFact]
    public void Offers_the_tags_most_used_as_chips()
    {
        var services = new ListServices();
        services.Add("One", "", "work");
        services.Add("Two", "", "work");
        services.Add("Three", "", "home");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["work", "home"], screen.ChipTags);
    }

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

    [WpfFact]
    public void Choosing_the_same_chip_again_clears_the_filter()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickChip("work");

        screen.ClickChip("work");

        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Choosing_another_chip_replaces_the_filter_since_one_tag_filters_at_a_time()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickChip("work");

        screen.ClickChip("home");

        Assert.Equal(["Buy milk"], screen.RowTitles);
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

        Assert.Equal(["Rare one", "Common 5b", "Common 5a", "Common 4b", "Common 4a", "Common 3b", "Common 3a", "Common 2b", "Common 2a", "Common 1b", "Common 1a"], screen.RowTitles);
    }

    [WpfTheory]
    [InlineData(AllTagsInput.Mouse)]
    [InlineData(AllTagsInput.Enter)]
    public void Choosing_the_chosen_tag_again_after_its_spelling_changed_elsewhere_clears_the_filter_once(AllTagsInput input)
    {
        var services = new ListServices();
        var old = services.Add("Old", "", "RARE");
        services.Add("Other", "", "common");
        using var screen = new TaskListScreen(services);
        screen.ChooseFromAllTags("RARE", input);
        Assert.Equal(["Old"], screen.RowTitles);

        // Elsewhere, the last task spelled RARE loses the tag and a new task takes the spelling "rare" (a new tag takes the spelling in use).
        services.Tasks.SaveEdit(old, new TaskDraft(old.Id, old.Title, old.Description, []));
        services.Add("New", "", "rare");
        Assert.Equal(["New"], screen.RowTitles);

        screen.ChooseFromAllTags("rare", input);

        Assert.Equal(["New", "Other", "Old"], screen.RowTitles);
    }

    // Five tags with two tasks each fill the chips; "rare" has one task, so only the all-tags list offers it.
    private static void AddTasksWithTagsOnlyInAllTagsList(ListServices services)
    {
        for (var tag = 1; tag <= 5; tag++)
        {
            services.Add($"Common {tag}a", "", $"common{tag}");
            services.Add($"Common {tag}b", "", $"common{tag}");
        }

        services.Add("Rare one", "", "rare");
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

    [WpfFact]
    public void Search_matches_titles_descriptions_and_tags_ignoring_case()
    {
        var services = new ListServices();
        services.Add("Plan trip", "", "travel");
        services.Add("Call Ana", "Ask about MILK");
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.Search("milk");
        Assert.Equal(["Buy milk", "Call Ana"], screen.RowTitles);

        screen.Search("TRAVEL");
        Assert.Equal(["Plan trip"], screen.RowTitles);
    }

    [WpfFact]
    public void Clearing_the_search_shows_every_task_again()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.Add("Call Ana");
        using var screen = new TaskListScreen(services);
        screen.Search("milk");

        screen.Search("");

        Assert.Equal(["Call Ana", "Buy milk"], screen.RowTitles);
    }

    [WpfFact]
    public void Search_combines_with_the_tag_filter()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Buy milk for work", "", "work");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickChip("work");

        screen.Search("milk");

        Assert.Equal(["Buy milk for work"], screen.RowTitles);
    }

    [WpfFact]
    public void The_resolved_count_counts_only_resolved_tasks_that_match_the_search_and_the_tag()
    {
        var services = new ListServices();
        services.AddResolved("Old milk", "", "home");
        services.AddResolved("Old milk run", "", "work");
        services.AddResolved("Old call", "", "work");
        using var screen = new TaskListScreen(services);
        Assert.Equal("3 resolved", screen.ResolvedLabel);

        screen.ClickChip("work");
        screen.Search("milk");

        Assert.Equal("1 resolved", screen.ResolvedLabel);
    }

    [WpfFact]
    public void Hides_the_resolved_count_when_no_resolved_task_matches()
    {
        var services = new ListServices();
        services.AddResolved("Old call");
        using var screen = new TaskListScreen(services);

        screen.Search("milk");

        Assert.Null(screen.ResolvedLabel);
    }
}
