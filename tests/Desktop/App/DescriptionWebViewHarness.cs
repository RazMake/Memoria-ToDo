using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.FormattedView;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// A <c>WpfDescriptionWebView</c> on its own scratch clone folder and browser data folder, shown in a window,
/// with the WebView2 control it exposes watched from outside through the WebView2 SDK. Created on the UI thread.
/// Events are turned into awaitable signals, so tests never sleep or poll.
/// </summary>
internal sealed class DescriptionWebViewHarness : IDisposable
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "description-web-view", Guid.NewGuid().ToString("N"));
    private readonly Window _window;
    private readonly TaskCompletionSource<CoreWebView2> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<string> _pages = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _images = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _links = Channel.CreateUnbounded<string>();
    private readonly Channel<(string, bool)> _navigations = Channel.CreateUnbounded<(string, bool)>();
    private readonly Channel<(string, bool)> _windows = Channel.CreateUnbounded<(string, bool)>();
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _failures = new();
    private Process? _browser;
    private Queue<string>? _chain;
    private TaskCompletionSource? _chainDone;
    private int _widthChanges;
    private readonly Channel<string> _failureSignals = Channel.CreateUnbounded<string>();
    private readonly System.Collections.Concurrent.ConcurrentQueue<Exception> _unhandled = new();

    /// <summary>Exceptions that escaped to the UI thread's dispatcher, which would end the program. They are caught here so a test can fail on them.</summary>
    public IReadOnlyCollection<Exception> Unhandled => _unhandled;

    /// <summary>The reason of the next <c>Failed</c> report, which unlike <see cref="Started"/> doesn't expect the browser to be ready.</summary>
    public Task<string> NextFailure() => _failureSignals.Reader.ReadAsync().AsTask().WaitAsync(Limit);

    private void OnUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _unhandled.Enqueue(e.Exception);
        e.Handled = true;
    }

    /// <summary>Every reason the adapter reported through <c>Failed</c>, at startup or later.</summary>
    public IReadOnlyCollection<string> Failures => _failures;

    /// <summary>
    /// Shows the first page, then each next page as soon as the page before it finishes loading. The window width
    /// changes when each page is asked for, and again when its navigation starts, while the previous page is still
    /// the document. So the adapter may be measuring, or be asked by a reflow to measure, a page that is being replaced.
    /// Completes once the last page has loaded.
    /// </summary>
    public Task ShowEachOnceThePreviousLoaded(IEnumerable<string> pages)
    {
        _chain = new Queue<string>(pages);
        _chainDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Port.Show(_chain.Dequeue());
        return _chainDone.Task.WaitAsync(Limit);
    }

    // Runs on the UI thread in the browser's page-loaded event, with the adapter's own measurement of that page under way.
    private void ShowNextOfChain()
    {
        if (_chain is null || _chainDone is null)
        {
            return;
        }

        if (_chain.Count == 0)
        {
            _chainDone.TrySetResult();
            return;
        }

        ChangeWidth();
        Port.Show(_chain.Dequeue());
    }

    // A width never used before, so every call is a size change that reflows the page.
    private void ChangeWidth() => _window.Width = 300 + _widthChanges++;

    public DescriptionWebViewHarness()
    {
        CloneFolder = Path.Combine(_root, "clone");
        BrowserDataFolder = Path.Combine(_root, "browser-data");
        Directory.CreateDirectory(CloneFolder);

        WebView = new WpfDescriptionWebView(CloneFolder, BrowserDataFolder);
        Port = WebView;
        Control = (WebView2CompositionControl)Port.Element;
        Control.CoreWebView2InitializationCompleted += OnInitialized;
        Port.LinkClicked += (_, address) => _links.Writer.TryWrite(address);
        Port.Failed += (_, reason) =>
        {
            _failures.Enqueue(reason);
            _failureSignals.Writer.TryWrite(reason);
            _started.TrySetException(new InvalidOperationException($"The browser did not start: {reason}"));
        };
        Control.Dispatcher.UnhandledException += OnUnhandled;

        // A stack gives the element all the height it asks for, as a scrolling list does, in a window shorter than long content.
        _window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { Control } } };
        _window.Show();
    }

    /// <summary>Sets the width the page is shown in.</summary>
    public void ResizeWindowTo(double width) => _window.Width = width;

    /// <summary>
    /// Completes once the element's height, checked now and after each of its size changes, is accepted. It is
    /// told the height and the page body's own height, both in device-independent pixels.
    /// </summary>
    public async Task WaitForHeight(Func<double, double, bool> accepted)
    {
        var changes = Channel.CreateUnbounded<bool>();
        void OnSizeChanged(object? sender, SizeChangedEventArgs e) => changes.Writer.TryWrite(true);
        Control.SizeChanged += OnSizeChanged;
        try
        {
            while (true)
            {
                if (accepted(Control.ActualHeight, await ContentHeight()))
                {
                    return;
                }

                await changes.Reader.ReadAsync().AsTask().WaitAsync(Limit);
            }
        }
        finally
        {
            Control.SizeChanged -= OnSizeChanged;
        }
    }

    /// <summary>
    /// The height the page's content really takes, read by the test itself with a script. The adapter turns the
    /// page's own scripts off, but a script run through the SDK still runs.
    /// </summary>
    private async Task<double> ContentHeight()
    {
        var core = await Started;
        var json = await core.ExecuteScriptAsync("document.body.getBoundingClientRect().height");
        return double.Parse(json, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The adapter, through its port.</summary>
    public IDescriptionWebView Port { get; }

    public WebView2CompositionControl Control { get; }

    public string CloneFolder { get; }

    public string BrowserDataFolder { get; }

    /// <summary>The next user navigation the browser started, as (address, whether it ended cancelled), read once every handler has run.</summary>
    public Task<(string Address, bool Cancelled)> NextUserNavigation() => _navigations.Reader.ReadAsync().AsTask().WaitAsync(Limit);

    /// <summary>The next new window a user asked for, as (address, whether it ended handled), read once every handler has run.</summary>
    public Task<(string Address, bool Handled)> NextNewWindow() => _windows.Reader.ReadAsync().AsTask().WaitAsync(Limit);

    private WpfDescriptionWebView WebView { get; }

    /// <summary>Completes with the browser's core once it has started, or fails when the adapter reports it can't start.</summary>
    public Task<CoreWebView2> Started => _started.Task.WaitAsync(Limit);

    /// <summary>The title of each page the browser finished loading, in order.</summary>
    public Task<string> NextLoadedPageTitle() => _pages.Reader.ReadAsync().AsTask().WaitAsync(Limit);

    /// <summary>The address of each file the browser got a response for from the clone address, in order.</summary>
    public Task<string> NextClonedFileAddress() => _images.Reader.ReadAsync().AsTask().WaitAsync(Limit);

    /// <summary>The address of each link the adapter reported, in order.</summary>
    public Task<string> NextReportedLink() => _links.Reader.ReadAsync().AsTask().WaitAsync(Limit);

    public Process Browser => _browser ?? throw new InvalidOperationException("The browser has not started.");

    /// <summary>
    /// Clicks the centre of the page's link at the index, with a mouse press and release sent to this browser alone
    /// through the DevTools protocol. The browser takes them as the user's own input, as it would a mouse's, with no
    /// effect on the Windows keyboard focus or on other programs.
    /// </summary>
    public async Task ClickLink(int index)
    {
        var core = await Started;
        var centre = await core.ExecuteScriptAsync(
            $"(() => {{ const r = document.querySelectorAll('a')[{index}].getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; }})()");
        var point = System.Text.Json.JsonSerializer.Deserialize<double[]>(centre)
            ?? throw new InvalidOperationException($"Link {index} has no position.");
        foreach (var type in new[] { "mousePressed", "mouseReleased" })
        {
            var mouse = FormattableString.Invariant(
                $"{{\"type\":\"{type}\",\"x\":{point[0]},\"y\":{point[1]},\"button\":\"left\",\"buttons\":{(type == "mousePressed" ? 1 : 0)},\"clickCount\":1}}");
            await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", mouse);
        }
    }

    public void Dispose()
    {
        // The test's own cleanup of the real control, whatever the adapter's Dispose did (both are safe to repeat).
        Control.Dispatcher.UnhandledException -= OnUnhandled;
        Port.Dispose();
        Control.Dispose();
        _window.Close();
        var exited = true;
        if (_browser is not null)
        {
            // WebView2 holds files in its data folder until its browser has exited.
            exited = _browser.WaitForExit(Limit);
            _browser.Dispose();
        }

        if (!exited)
        {
            throw new TimeoutException($"The browser using {BrowserDataFolder} did not exit, so its folder was left in place.");
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // Reads a new window request's final state once the UI thread is idle, after every handler has run.
    private void AfterOtherHandlers(CoreWebView2Deferral deferral, Action read) =>
        Control.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                read();
            }
            finally
            {
                deferral.Complete();
            }
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    private void OnInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _started.TrySetException(e.InitializationException ?? new InvalidOperationException("WebView2 did not start."));
            return;
        }

        var core = Control.CoreWebView2;
        _browser = Process.GetProcessById((int)core.BrowserProcessId);
        // A user navigation's outcome is how the browser ended it, which doesn't depend on handler order.
        var userNavigations = new Dictionary<ulong, string>();
        core.NavigationStarting += (_, args) =>
        {
            if (args.IsUserInitiated)
            {
                userNavigations[args.NavigationId] = args.Uri;
            }
            else if (_chain is not null)
            {
                // The page asked for is now loading and the old one is still the document: reflow the old one.
                ChangeWidth();
            }
        };
        core.NavigationCompleted += (_, args) =>
        {
            if (userNavigations.Remove(args.NavigationId, out var address))
            {
                _navigations.Writer.TryWrite((address, args.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled));
            }
            else
            {
                _pages.Writer.TryWrite(core.DocumentTitle);
                ShowNextOfChain();
            }
        };
        core.NewWindowRequested += (_, args) => AfterOtherHandlers(args.GetDeferral(), () => _windows.Writer.TryWrite((args.Uri, args.Handled)));
        core.WebResourceResponseReceived += (_, args) =>
        {
            if (args.Request.Uri.StartsWith(Port.CloneAddress.AbsoluteUri, StringComparison.Ordinal) && args.Response.StatusCode == 200)
            {
                _images.Writer.TryWrite(args.Request.Uri);
            }
        };
        _started.TrySetResult(core);
    }
}
