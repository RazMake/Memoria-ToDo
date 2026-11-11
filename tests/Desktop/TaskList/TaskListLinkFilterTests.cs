using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// A link to a task the search or the tag filter hides clears only the filter that hides it, keeps the one that doesn't,
/// and expands the task. A link to a missing task, or any other link, clears nothing. In every case the link is clicked in
/// the open task "Source note", which the search "note" and the tag "work" both show.
/// </summary>
[UX]
public class TaskListLinkFilterTests
{
    private static TaskListScreen OpenSource(ListServices services)
    {
        var screen = new TaskListScreen(services);
        screen.ClickChip("work");
        screen.Search("note");
        screen.PressOn("Source note", Key.Enter);
        return screen;
    }

    private static ListServices Services(string targetTitle, string targetTag, bool targetResolved = false)
    {
        var services = new ListServices();
        services.Add("Unrelated", "", "home");
        services.Add("Other note", "", "work");
        if (targetResolved)
        {
            services.AddResolved(targetTitle, "", targetTag);
        }
        else
        {
            services.Add(targetTitle, "", targetTag);
        }

        services.Add("Source note", "See the target", "work");
        services.Add("Plain work", "", "work");
        return services;
    }

    private static string Link(ListServices services, string title) => services.Tasks.LinkAddress(services.Tasks.All.Single(task => task.Title == title).Id);

    [WpfFact]
    public void A_link_to_a_task_only_the_search_hides_clears_the_search_and_keeps_the_tag_filter()
    {
        var services = Services("Call Ana", "work");
        using var screen = OpenSource(services);

        screen.Web.ClickLink(Link(services, "Call Ana"));

        Assert.Equal("", screen.SearchText);
        Assert.Equal(new[] { "Plain work", "Source note", "Call Ana", "Other note" }.Order(), screen.RowTitles.Order());
        Assert.DoesNotContain("Unrelated", screen.RowTitles);
        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_link_to_a_task_only_the_tag_filter_hides_clears_the_tag_filter_and_keeps_the_search()
    {
        var services = Services("Home note", "home");
        using var screen = OpenSource(services);

        screen.Web.ClickLink(Link(services, "Home note"));

        Assert.Equal("note", screen.SearchText);
        Assert.Equal(new[] { "Source note", "Home note", "Other note" }.Order(), screen.RowTitles.Order());
        Assert.DoesNotContain("Plain work", screen.RowTitles);
        Assert.Equal(["Home note"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_link_to_a_task_both_hide_clears_the_search_and_the_tag_filter()
    {
        var services = Services("Call Ana", "home");
        using var screen = OpenSource(services);

        screen.Web.ClickLink(Link(services, "Call Ana"));

        Assert.Equal("", screen.SearchText);
        Assert.Contains("Unrelated", screen.RowTitles);
        Assert.Contains("Plain work", screen.RowTitles);
        Assert.Equal(["Call Ana"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_link_to_a_task_both_show_clears_nothing()
    {
        var services = Services("Work note", "work");
        using var screen = OpenSource(services);

        screen.Web.ClickLink(Link(services, "Work note"));

        Assert.Equal("note", screen.SearchText);
        Assert.Equal(new[] { "Source note", "Work note", "Other note" }.Order(), screen.RowTitles.Order());
        Assert.Equal(["Work note"], screen.ExpandedTitles);
    }

    [WpfFact]
    public void A_link_to_a_resolved_task_the_search_hides_clears_the_search_and_shows_it_expanded()
    {
        var services = Services("Old plan", "work", targetResolved: true);
        using var screen = OpenSource(services);

        screen.Web.ClickLink(Link(services, "Old plan"));

        Assert.Equal("", screen.SearchText);
        Assert.True(screen.HasRow("Old plan"));
        Assert.Equal(["Old plan"], screen.ExpandedTitles);
        Assert.DoesNotContain("Unrelated", screen.RowTitles);
    }

    [WpfFact]
    public void A_link_to_a_missing_task_clears_no_filter()
    {
        var services = Services("Gone", "home");
        var address = Link(services, "Gone");
        services.Tasks.Delete(services.Tasks.All.Single(task => task.Title == "Gone").Id);
        using var screen = OpenSource(services);

        screen.Web.ClickLink(address);

        Assert.Equal("note", screen.SearchText);
        Assert.Equal(new[] { "Source note", "Other note" }.Order(), screen.RowTitles.Order());
        Assert.True(screen.ShowsText("no longer exists"));
    }

    [WpfTheory]
    [InlineData("file:///C:/secrets.txt")]
    [InlineData("https://example.com/shop")]
    public void A_link_that_is_not_a_task_clears_no_filter(string address)
    {
        var services = Services("Call Ana", "home");
        using var screen = OpenSource(services);

        screen.Web.ClickLink(address);

        Assert.Equal("note", screen.SearchText);
        Assert.Equal(new[] { "Source note", "Other note" }.Order(), screen.RowTitles.Order());
    }
}
