using System.Windows;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Read-only tag badges: which tags show, in what order, and in which color for the theme.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagBadgesTests
{
    [WpfFact]
    public void Shows_each_tag_as_a_badge_in_the_order_given()
    {
        using var host = new BadgesHost("work", "home", "errand");

        Assert.Equal(["work", "home", "errand"], TagControls.BadgeTags(host.Badges));
    }

    [WpfFact]
    public void Setting_the_tags_replaces_the_badges()
    {
        using var host = new BadgesHost("work", "home");

        host.Badges.Tags = ["errand"];

        Assert.Equal(["errand"], TagControls.BadgeTags(host.Badges));
    }

    [WpfFact]
    public void Shows_no_remove_buttons()
    {
        using var host = new BadgesHost("work");

        Assert.Empty(TagControls.RemovableTags(host.Badges));
    }

    [WpfFact]
    public void Shows_a_tag_in_its_kept_color_for_the_light_theme()
    {
        using var host = new BadgesHost("home");
        host.Services.TagColors.Seed("home", "blue-2");

        host.Badges.Tags = ["home"];

        Assert.Equal(TagControls.Expected(TagColors.Find("blue-2")!, dark: false), TagControls.FillOf(host.Badges, "home"));
    }

    [WpfFact]
    public void Shows_a_tag_with_no_kept_color_in_its_default_color()
    {
        using var host = new BadgesHost("home");

        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Badges, "home"));
    }

    [WpfFact]
    public void Shows_the_default_color_for_a_color_name_this_app_does_not_know()
    {
        using var host = new BadgesHost("home");
        host.Services.TagColors.Seed("home", "mauve-9");

        host.Badges.Tags = ["home"];

        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Badges, "home"));
    }

    [WpfFact]
    public void Switches_to_the_dark_fill_when_the_theme_changes()
    {
        using var host = new BadgesHost("home");
        host.Services.TagColors.Seed("home", "blue-2");
        host.Badges.Tags = ["home"];

        host.Services.Theme.Switch(true);

        Assert.Equal(TagControls.Expected(TagColors.Find("blue-2")!, dark: true), TagControls.FillOf(host.Badges, "home"));
    }

    [WpfFact]
    public void Starts_with_the_dark_fill_when_the_theme_is_dark()
    {
        using var host = new BadgesHost(dark: true, "home");
        host.Services.TagColors.Seed("home", "red-3");

        host.Badges.Tags = ["home"];

        Assert.Equal(TagControls.Expected(TagColors.Find("red-3")!, dark: true), TagControls.FillOf(host.Badges, "home"));
    }

    [WpfFact]
    public void Shows_a_new_color_when_the_tag_colors_change()
    {
        using var host = new BadgesHost("home");

        host.Services.TagColors.SetColor("home", "green-1");

        Assert.Equal(TagControls.Expected(TagColors.Find("green-1")!, dark: false), TagControls.FillOf(host.Badges, "home"));
    }

    [WpfFact]
    public async Task Shows_a_new_color_when_the_tag_colors_change_on_a_worker_thread()
    {
        using var host = new BadgesHost("home");
        host.Services.TagColors.Seed("home", "green-1");

        await Task.Run(host.Services.TagColors.RaiseChanged);
        await RenderedField.Settled();

        Assert.Equal(TagControls.Expected(TagColors.Find("green-1")!, dark: false), TagControls.FillOf(host.Badges, "home"));
    }
}

/// <summary>Hosts a <see cref="TagBadges"/> in a showing window over the fakes, with the tags shown. Shared test setup.</summary>
internal sealed class BadgesHost : IDisposable
{
    private readonly Window window;

    public BadgesHost(params string[] tags)
        : this(false, tags)
    {
    }

    public BadgesHost(bool dark, params string[] tags)
    {
        Services = new EditorServices();
        Services.Theme.IsDark = dark;
        Badges = Services.CreateTagBadges();
        Badges.Tags = tags;
        window = new Window { Content = Badges, Width = 480, Height = 200 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagBadges Badges { get; }

    public void Dispose() => window.Close();
}
