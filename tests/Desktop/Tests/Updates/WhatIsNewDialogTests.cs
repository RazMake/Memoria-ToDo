using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>
/// The What is new dialog on screen, opened from a window: a modal dialog titled "What is new" with each newer release's
/// notes, newest first, that its Close button, Esc, and the Close window shortcut close.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class WhatIsNewDialogTests
{
    private static readonly NewerVersion ThreeReleases = new("2.2.0",
    [
        new ReleaseNotes("2.2.0", "You can sort tasks."),
        new ReleaseNotes("2.1.5", null),
        new ReleaseNotes("2.1.1", "Sync no longer stops early."),
    ]);

    private static async Task<UpdatesHarness> AppWithUpdateAsync(UpdateStage stage = UpdateStage.Available)
    {
        var app = new UpdatesHarness();
        app.Install.Found = ThreeReleases;
        app.Start();
        await app.WaitForStateAsync(state => state.Stage == UpdateStage.Available);
        if (stage == UpdateStage.Updating)
        {
            app.Install.HoldDownloads();
            app.Updates.UpdateNow();
            await app.WaitForStateAsync(state => state.Stage == UpdateStage.Updating);
        }

        return app;
    }

    [WpfFact]
    public async Task Opens_modal_titled_What_is_new_owned_by_and_centered_on_the_window()
    {
        var app = await AppWithUpdateAsync();
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.NotNull(screen.Dialog);
        Assert.False(screen.ReturnedWhileShowing, "What is new returned while its dialog showed, so it isn't modal.");
        Assert.Equal("What is new", screen.Dialog.Title);
        Assert.Same(screen.Owner, screen.Dialog.Owner);
        Assert.True(screen.Centered, "The dialog wasn't centered on its owner.");
    }

    [WpfFact]
    public async Task Shows_each_release_under_its_version_newest_first()
    {
        var app = await AppWithUpdateAsync();
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        var page = Assert.Single(app.WebViews).Page!;
        var positions = new[]
        {
            "2.2.0", "You can sort tasks.", "2.1.5", "No notes for this version.", "2.1.1", "Sync no longer stops early.",
        }.Select(text => page.IndexOf(text, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
    }

    private const string ReleasesPage = "https://github.com/RazMake/Memoria-ToDo/releases";

    private static async Task<UpdatesHarness> AppFoundAsync(NewerVersion found)
    {
        var app = new UpdatesHarness();
        app.Install.Found = found;
        app.Start();
        await app.WaitForStateAsync(state => state.Stage == UpdateStage.Available);
        return app;
    }

    [WpfFact]
    public async Task Ends_with_a_link_to_the_GitHub_Releases_page_when_the_notes_do_not_reach_the_running_version()
    {
        var app = await AppFoundAsync(ThreeReleases with { OlderNotesPage = new Uri(ReleasesPage) });
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        var page = Assert.Single(app.WebViews).Page!;
        var lastNotes = page.IndexOf("Sync no longer stops early.", StringComparison.Ordinal);
        Assert.True(lastNotes >= 0);
        Assert.True(page.IndexOf(ReleasesPage, lastNotes, StringComparison.Ordinal) >= 0,
            "The page has no link to the Releases page after the last release's notes.");
    }

    [WpfFact]
    public async Task Shows_the_link_in_the_markdown_source_when_the_web_view_fails()
    {
        var app = await AppFoundAsync(ThreeReleases with { OlderNotesPage = new Uri(ReleasesPage) });
        app.WebViewFailure = "The WebView2 Runtime is missing.";
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.Contains(screen.Texts, text => text.Contains(ReleasesPage));
    }

    [WpfFact]
    public async Task Has_no_link_to_the_Releases_page_when_the_notes_reach_the_running_version()
    {
        var app = await AppFoundAsync(ThreeReleases);
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.DoesNotContain("/releases", Assert.Single(app.WebViews).Page!, StringComparison.Ordinal);
    }

    [WpfFact]
    public async Task Shows_the_page_in_the_theme_the_app_has()
    {
        var app = await AppWithUpdateAsync();
        using var screen = new WhatIsNewScreen(app);
        app.Theme.IsDark = false;
        screen.ShowWhatIsNew(dialog => { });
        app.Theme.IsDark = true;

        screen.ShowWhatIsNew(dialog => { });

        Assert.Equal(2, app.WebViews.Count);
        Assert.NotEqual(app.WebViews[0].Page, app.WebViews[1].Page);
    }

    [WpfFact]
    public async Task Shows_the_markdown_source_when_the_web_view_fails()
    {
        var app = await AppWithUpdateAsync();
        app.WebViewFailure = "The WebView2 Runtime is missing.";
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.Contains(screen.Texts, text => text.Contains("You can sort tasks."));
        Assert.Contains(screen.Texts, text => text.Contains("The WebView2 Runtime is missing."));
    }

    [WpfFact]
    public async Task Opens_while_an_update_is_under_way()
    {
        var app = await AppWithUpdateAsync(UpdateStage.Updating);
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.NotNull(screen.Dialog);
    }

    [WpfFact]
    public void Does_not_open_when_there_is_no_update()
    {
        var app = new UpdatesHarness();
        app.Start();
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.Null(screen.Dialog);
        Assert.Empty(app.WebViews);
    }

    [WpfFact]
    public async Task The_Close_button_closes_it()
    {
        var app = await AppWithUpdateAsync();
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => OpenWindows.Click(dialog, "Close"));

        Assert.False(screen.Dialog!.IsVisible);
        Assert.True(screen.Owner.IsVisible);
    }

    [WpfFact]
    public async Task Esc_closes_it()
    {
        var app = await AppWithUpdateAsync();
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(OpenWindows.PressEscape);

        Assert.False(screen.Dialog!.IsVisible);
        Assert.True(screen.Owner.IsVisible);
    }

    [WpfFact]
    public async Task The_Close_window_shortcut_closes_it()
    {
        var app = await AppWithUpdateAsync();
        app.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => SimulatedKey.Chord(dialog, Key.LeftShift, Key.F9));

        Assert.False(screen.Dialog!.IsVisible);
        Assert.True(screen.Owner.IsVisible);
    }

    [WpfFact]
    public async Task Each_opening_makes_a_new_web_view_and_releases_it_when_the_dialog_closes()
    {
        var app = await AppWithUpdateAsync();
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });
        screen.ShowWhatIsNew(dialog => { });

        Assert.Equal(2, app.WebViews.Count);
        Assert.All(app.WebViews, view => Assert.True(view.IsDisposed));
    }
}
