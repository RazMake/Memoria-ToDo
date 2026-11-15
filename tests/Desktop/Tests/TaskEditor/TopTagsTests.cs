using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>The seven most-used tags as badges: which show, their colors, and what a click reports.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TopTagsTests
{
    [WpfFact]
    public void Shows_every_tag_in_use_when_there_are_fewer_than_seven_most_used_first()
    {
        using var host = new TopTagsHost(("home", 1), ("work", 2));

        Assert.Equal(["work", "home"], host.Tags);
    }

    [WpfFact]
    public void Shows_only_the_seven_most_used_most_used_first_and_ties_alphabetical()
    {
        using var host = new TopTagsHost(
            ("t1", 1), ("t2", 2), ("t3", 3), ("t4", 4), ("t5", 5), ("t6", 6), ("t7", 7), ("t8", 8), ("t9", 8));

        Assert.Equal(["t8", "t9", "t7", "t6", "t5", "t4", "t3"], host.Tags);
    }

    [WpfFact]
    public void Clicking_a_badge_raises_TagChosen_with_its_tag()
    {
        using var host = new TopTagsHost(("home", 2), ("work", 1));

        OpenWindows.Click((System.Windows.Controls.Button)TagControls.Badge(host.Top, "work"));

        Assert.Equal(["work"], host.Chosen);
    }

    [WpfFact]
    public void Shows_each_tag_in_its_kept_color_or_its_default_for_the_theme()
    {
        using var host = new TopTagsHost(("home", 2), ("work", 1));
        host.Services.TagColors.Seed("home", "purple-1");
        host.Services.TagColors.RaiseChanged();

        Assert.Equal(TagControls.Expected(TagColors.Find("purple-1")!, dark: false), TagControls.FillOf(host.Top, "home"));
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("work"), dark: false), TagControls.FillOf(host.Top, "work"));
    }

    [WpfFact]
    public void Switches_to_the_dark_fill_when_the_theme_changes()
    {
        using var host = new TopTagsHost(("home", 1));

        host.Services.Theme.Switch(true);

        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: true), TagControls.FillOf(host.Top, "home"));
    }

    [WpfFact]
    public async Task Refreshes_when_Tasks_changes_on_a_worker_thread()
    {
        using var host = new TopTagsHost(("home", 1));

        await host.Services.UseTagOnWorkerThread("work", 2);
        await RenderedField.Settled();

        Assert.Equal(["work", "home"], host.Tags);
    }

    [WpfFact]
    public void Shows_no_badges_after_the_repository_folder_is_cleared()
    {
        using var host = new TopTagsHost(("home", 1));

        host.Services.Tasks.UseRepositoryFolder(null);

        Assert.Empty(host.Tags);
    }
}

/// <summary>Hosts a <see cref="TopTags"/> in a showing window over a real Tasks with the given tag counts. Shared test setup.</summary>
internal sealed class TopTagsHost : IDisposable
{
    private readonly Window window;
    private readonly List<string> chosen = [];

    public TopTagsHost(params (string Tag, int Count)[] uses)
    {
        Services = new EditorServices();
        Services.UseRepository();
        foreach (var (tag, count) in uses)
        {
            Services.UseTag(tag, count);
        }

        Top = Services.CreateTopTags();
        Top.TagChosen += (_, tag) => chosen.Add(tag);
        window = new Window { Content = Top, Width = 480, Height = 200 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TopTags Top { get; }

    public IReadOnlyList<string> Tags => TagControls.BadgeTags(Top);

    public IReadOnlyList<string> Chosen => chosen;

    public void Dispose() => window.Close();
}
