using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests.WhatIsNew;

/// <summary>
/// The What is new dialog on screen, opened from a window, in the paths a user takes: reading the notes and closing it, opening it
/// while an update is under way and closing it by keys, following the link to older notes, and reading the markdown when the web
/// view fails. It's a modal dialog titled "What is new" with each newer release's notes, newest first.
/// </summary>
[UX("WhatIsNew")]
[Collection(ScreenTests.Collection)]
public class WhatIsNewPathTests
{
    private const string ReleasesPage = "https://github.com/RazMake/Memoria-ToDo/releases";

    private static readonly NewerVersion ThreeReleases = new("2.2.0",
    [
        new ReleaseNotes("2.2.0", "You can sort tasks."),
        new ReleaseNotes("2.1.5", null),
        new ReleaseNotes("2.1.1", "Sync no longer stops early."),
    ]);

    private static async Task<UpdatesHarness> AppFoundAsync(NewerVersion found, UpdateStage stage = UpdateStage.Available)
    {
        var app = new UpdatesHarness();
        app.Install.Found = found;
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
    public async Task Reading_the_notes_and_closing_with_the_Close_button_then_opening_again_in_the_dark_theme()
    {
        var app = await AppFoundAsync(ThreeReleases);
        using var screen = new WhatIsNewScreen(app);
        app.Theme.IsDark = false;

        screen.ShowWhatIsNew(dialog => OpenWindows.Click(dialog, "Close"));

        Assert.NotNull(screen.Dialog);
        Assert.False(screen.ReturnedWhileShowing, "What is new returned while its dialog showed, so it isn't modal.");
        Assert.Equal("What is new", screen.Dialog.Title);
        Assert.Same(screen.Owner, screen.Dialog.Owner);
        Assert.True(screen.Centered, "The dialog wasn't centered on its owner.");
        var page = Assert.Single(app.WebViews).Page!;
        var positions = new[]
        {
            "2.2.0", "You can sort tasks.", "2.1.5", "No notes for this version.", "2.1.1", "Sync no longer stops early.",
        }.Select(text => page.IndexOf(text, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        Assert.DoesNotContain("/releases", page, StringComparison.Ordinal);
        Assert.False(screen.Dialog.IsVisible);
        Assert.True(screen.Owner.IsVisible);

        app.Theme.IsDark = true;
        screen.ShowWhatIsNew(dialog => { });

        Assert.Equal(2, app.WebViews.Count);
        Assert.NotEqual(app.WebViews[0].Page, app.WebViews[1].Page);
        Assert.All(app.WebViews, view => Assert.True(view.IsDisposed));
    }

    [WpfFact]
    public async Task Opening_while_an_update_is_under_way_and_closing_with_Esc_then_with_the_Close_window_shortcut()
    {
        var app = await AppFoundAsync(ThreeReleases, UpdateStage.Updating);
        app.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(OpenWindows.PressEscape);
        Assert.NotNull(screen.Dialog);
        Assert.False(screen.Dialog.IsVisible);
        Assert.True(screen.Owner.IsVisible);

        screen.ShowWhatIsNew(dialog => SimulatedKey.Chord(dialog, Key.LeftShift, Key.F9));
        Assert.False(screen.Dialog.IsVisible);
        Assert.True(screen.Owner.IsVisible);
    }

    [WpfFact]
    public async Task Notes_that_stop_short_of_the_running_version_end_with_the_Releases_link_and_a_browser_that_cannot_open_it_logs_a_Warning()
    {
        var app = await AppFoundAsync(ThreeReleases with { OlderNotesPage = new Uri(ReleasesPage) });
        var failure = new InvalidOperationException("There is no default browser.");
        app.Browser.Failure = failure;
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => app.WebViews[0].ClickLink(ReleasesPage));

        var page = Assert.Single(app.WebViews).Page!;
        var lastNotes = page.IndexOf("Sync no longer stops early.", StringComparison.Ordinal);
        Assert.True(lastNotes >= 0);
        Assert.True(page.IndexOf(ReleasesPage, lastNotes, StringComparison.Ordinal) >= 0,
            "The page has no link to the Releases page after the last release's notes.");
        var entry = Assert.Single(app.Log.Events, e => e.Level >= LogEventLevel.Warning);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.True(entry.HasException(failure));
    }

    [WpfFact]
    public async Task When_the_web_view_fails_the_dialog_shows_the_markdown_source_with_the_failure_and_the_Releases_link()
    {
        var app = await AppFoundAsync(ThreeReleases with { OlderNotesPage = new Uri(ReleasesPage) });
        app.WebViewFailure = "The WebView2 Runtime is missing.";
        using var screen = new WhatIsNewScreen(app);

        screen.ShowWhatIsNew(dialog => { });

        Assert.Contains(screen.Texts, text => text.Contains("You can sort tasks."));
        Assert.Contains(screen.Texts, text => text.Contains("The WebView2 Runtime is missing."));
        Assert.Contains(screen.Texts, text => text.Contains(ReleasesPage));
    }
}
