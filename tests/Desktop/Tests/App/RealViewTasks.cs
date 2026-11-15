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
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The real <c>WpfDescriptionWebView</c> in the real Task View window, over fake tasks, files, theme and browser, with
/// its own temporary folders. Shared test setup: the app, its tray and its global shortcuts are not started. Rows are
/// found by their accessible names. Disposing it closes what it opened, stops the browser, and deletes the folders.
/// </summary>
internal sealed class RealViewTasks : IDisposable
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "view-tasks-web-view", Guid.NewGuid().ToString("N"));
    private readonly OpenWindows _windows = new();
    private readonly List<WpfDescriptionWebView> _made = [];
    private readonly TaskCompletionSource<CoreWebView2> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<bool> _pages = Channel.CreateUnbounded<bool>();
    private Process? _browser;

    public RealViewTasks()
    {
        var clone = Path.Combine(_root, "clone");
        Directory.CreateDirectory(clone);
        var files = new FakeFileSystem();
        Tasks = TaskStore.Create(files, new FakeImageEncoder(), new FakeTimeProvider(), Path.Combine(_root, "device"));
        Tasks.UseRepositoryFolder(clone);
        View = ViewTasksWindow.Create(
            Tasks, new FakeTagColors(), Palette.Colors, new FakeTheme(), new FakeTimeProvider(), new FakeClipboard(), new FakeImageFileChooser(),
            files, () => MakeWebView(clone), new FakeDefaultBrowser(), new FakeSyncService(), new Platform.CloseWindowKeys(),
            new FakeUpdateStatus());
    }

    public ITasks Tasks { get; }

    public IViewTasksWindow View { get; }

    /// <summary>Every web view Task View made.</summary>
    public IReadOnlyList<WpfDescriptionWebView> WebViews => _made;

    /// <summary>The browser process; set once <see cref="ShowAsync"/> returned.</summary>
    public Process Browser => _browser ?? throw new InvalidOperationException("Task View isn't shown yet.");

    /// <summary>The showing Task View window.</summary>
    public Window Window => _windows.Visible().Single();

    public void Add(string title, string description) =>
        Tasks.Add(Tasks.StartDraft() with { Title = title, Description = description });

    /// <summary>Shows Task View and waits until its browser has started, with no sleeps.</summary>
    public async Task<CoreWebView2> ShowAsync()
    {
        View.Show();
        var core = await _started.Task.WaitAsync(Limit);
        _browser = Process.GetProcessById((int)core.BrowserProcessId);
        return core;
    }

    /// <summary>Clicks the row's title, which selects the task and shows its description on the right.</summary>
    public void Expand(string title)
    {
        var window = Window;
        window.UpdateLayout();
        var row = Visuals(window).OfType<FrameworkElement>()
            .First(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "Row" && AutomationProperties.GetName(e) == title);
        row.Focus();
        Keyboard.Focus(row);
        foreach (var routed in new[] { Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent, Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent })
        {
            row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = routed,
                Source = row,
            });
        }
    }

    /// <summary>Waits on each finished page load until the page's text holds the words, with no sleeps.</summary>
    public async Task WaitForPageText(CoreWebView2 core, string words)
    {
        while (true)
        {
            var text = await core.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
            if (text.Contains(words, StringComparison.Ordinal))
            {
                return;
            }

            await _pages.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        }
    }

    public void Dispose()
    {
        // The test's own cleanup of what it made, whatever the window did on close.
        _windows.Dispose();
        foreach (var webView in _made)
        {
            webView.Dispose();
            ((WebView2CompositionControl)webView.Element).Dispose();
        }

        if (_browser is not null)
        {
            var exited = _browser.WaitForExit(Limit);
            _browser.Dispose();
            if (!exited)
            {
                throw new TimeoutException($"The browser using {_root} did not exit, so its folder was left in place.");
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WpfDescriptionWebView MakeWebView(string clone)
    {
        var webView = new WpfDescriptionWebView(clone, Path.Combine(_root, "browser-data"));
        _made.Add(webView);
        var control = (WebView2CompositionControl)webView.Element;
        control.CoreWebView2InitializationCompleted += (_, e) =>
        {
            if (!e.IsSuccess)
            {
                _started.TrySetException(e.InitializationException ?? new InvalidOperationException("WebView2 did not start."));
                return;
            }

            control.CoreWebView2.NavigationCompleted += (_, _) => _pages.Writer.TryWrite(true);
            _started.TrySetResult(control.CoreWebView2);
        };
        webView.Failed += (_, reason) => _started.TrySetException(new InvalidOperationException($"The browser did not start: {reason}"));
        return webView;
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
