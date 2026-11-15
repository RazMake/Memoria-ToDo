using Memoria.ToDo.Testing;
using Microsoft.Web.WebView2.Wpf;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The wiring View Tasks needs from the app: the real <c>WpfDescriptionWebView</c> in the real View Tasks window,
/// over fake tasks, files, theme and browser (see <see cref="RealViewTasks"/>). The UX tests of View Tasks use a fake web
/// view, and the adapter's own tests host it in a plain panel, so neither shows whether the window really starts the
/// browser and keeps it. The window is driven by routed key events on its rows, and no input goes to Windows.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class ViewTasksWithRealWebViewTests
{
    [WpfFact]
    public async Task Starts_one_real_browser_when_it_opens_shows_each_expanded_description_in_it_and_releases_it_on_close()
    {
        using var view = new RealViewTasks();
        view.Add("First", "alphadescription");
        view.Add("Second", "betadescription");

        // The browser starts with the window, before any task is expanded.
        var core = await view.ShowAsync();
        var browser = view.Browser;
        var window = view.Window;

        view.Expand("First");
        await view.WaitForPageText(core, "alphadescription");
        view.Expand("Second");
        await view.WaitForPageText(core, "betadescription");

        var web = Assert.Single(view.WebViews);
        Assert.Same(core, ((WebView2CompositionControl)web.Element).CoreWebView2);
        Assert.Equal(browser.Id, (int)core.BrowserProcessId);

        window.Close();

        await browser.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(RealViewTasks.Limit);
    }
}
