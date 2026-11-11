using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The wiring View Tasks needs from the app: the real <c>WpfDescriptionWebView</c> in the real View Tasks window,
/// over fake tasks, files, theme and browser. The UX tests of View Tasks use a fake web view, and the adapter's own
/// tests host it in a plain panel, so neither shows whether the window really starts the browser and keeps it.
/// The window is driven by routed key events on its rows, found by their accessible names, and no input goes to
/// Windows. The app, its tray and its global shortcuts are not started.
/// </summary>
[Integration]
public class ViewTasksWithRealWebViewTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    [WpfFact]
    public async Task Starts_one_real_browser_when_it_opens_shows_each_expanded_description_in_it_and_releases_it_on_close()
    {
        var root = Path.Combine(Path.GetTempPath(), "view-tasks-web-view", Guid.NewGuid().ToString("N"));
        var clone = Path.Combine(root, "clone");
        Directory.CreateDirectory(clone);
        var files = new FakeFileSystem();
        var tasks = TaskStore.Create(files, new FakeImageEncoder(), new FakeTimeProvider(), Path.Combine(root, "device"));
        tasks.UseRepositoryFolder(clone);
        Add(tasks, "First", "alphadescription");
        Add(tasks, "Second", "betadescription");

        using var windows = new OpenWindows();
        var made = new List<WpfDescriptionWebView>();
        var started = new TaskCompletionSource<CoreWebView2>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pages = Channel.CreateUnbounded<bool>();
        Process? browser = null;
        try
        {
            var viewTasks = ViewTasksWindow.Create(
                tasks, Palette.Colors, new FakeTheme(), new FakeTimeProvider(), new FakeClipboard(), new FakeImageFileChooser(),
                files,
                () =>
                {
                    var webView = new WpfDescriptionWebView(clone, Path.Combine(root, "browser-data"));
                    made.Add(webView);
                    var control = (WebView2CompositionControl)webView.Element;
                    control.CoreWebView2InitializationCompleted += (_, e) =>
                    {
                        if (!e.IsSuccess)
                        {
                            started.TrySetException(e.InitializationException ?? new InvalidOperationException("WebView2 did not start."));
                            return;
                        }

                        control.CoreWebView2.NavigationCompleted += (_, _) => pages.Writer.TryWrite(true);
                        started.TrySetResult(control.CoreWebView2);
                    };
                    webView.Failed += (_, reason) => started.TrySetException(new InvalidOperationException($"The browser did not start: {reason}"));
                    return webView;
                },
                new FakeDefaultBrowser());

            viewTasks.Show();

            // The browser starts with the window, before any task is expanded.
            var core = await started.Task.WaitAsync(Limit);
            browser = Process.GetProcessById((int)core.BrowserProcessId);
            var window = windows.Visible().Single();

            Expand(window, "First");
            await WaitForPageText(core, pages, "alphadescription");
            Expand(window, "Second");
            await WaitForPageText(core, pages, "betadescription");

            var web = Assert.Single(made);
            Assert.Same(core, ((WebView2CompositionControl)web.Element).CoreWebView2);
            Assert.Equal(browser.Id, (int)core.BrowserProcessId);

            window.Close();

            await browser.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(Limit);
        }
        finally
        {
            // The test's own cleanup of what it made, whatever the window did on close.
            windows.Dispose();
            foreach (var webView in made)
            {
                webView.Dispose();
                ((WebView2CompositionControl)webView.Element).Dispose();
            }

            if (browser is not null)
            {
                var exited = browser.WaitForExit(Limit);
                browser.Dispose();
                if (!exited)
                {
                    throw new TimeoutException($"The browser using {root} did not exit, so its folder was left in place.");
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void Add(ITasks tasks, string title, string description) =>
        tasks.Add(tasks.StartDraft() with { Title = title, Description = description });

    // Gives the row the keyboard focus and presses Enter on it, as the keyboard does.
    private static void Expand(Window window, string title)
    {
        window.UpdateLayout();
        var row = Visuals(window).OfType<FrameworkElement>()
            .Single(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "Row" && AutomationProperties.GetName(e) == title);
        row.Focus();
        Keyboard.Focus(row);
        SimulatedKey.Chord(row, Key.Enter);
    }

    // Waits on each finished page load until the page's text holds the words, with no sleeps.
    private static async Task WaitForPageText(CoreWebView2 core, Channel<bool> pages, string words)
    {
        while (true)
        {
            var text = await core.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
            if (text.Contains(words, StringComparison.Ordinal))
            {
                return;
            }

            await pages.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        }
    }

    private static IEnumerable<DependencyObject> Visuals(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var inner in Visuals(child))
            {
                yield return inner;
            }
        }
    }
}
