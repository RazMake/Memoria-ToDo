using System.Drawing;
using System.Text.Json;
using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The description web view as a WebView2 control that draws inside WPF, so it scrolls and clips with the list.
/// The browser starts on the UI thread once the control is shown. It serves the clone folder at the clone address,
/// runs no scripts, and cancels every navigation the user starts, reporting its address instead.
/// </summary>
internal sealed class WpfDescriptionWebView : IDescriptionWebView
{
    private const string CloneHost = "clone.memoria-todo.example";

    private readonly string? _cloneFolder;
    private readonly WebView2CompositionControl _control;
    private readonly PageLoads _pages = new();
    private bool _startRequested;
    private bool _disposed;
    private bool _failed;
    private bool _pageLoaded;
    private CoreWebView2DevToolsProtocolEventReceiver? _resized;
    private int _measurement;
    private ulong? _loadingId;

    /// <param name="cloneFolder">The folder the clone address serves, or null when there is no clone yet.</param>
    /// <param name="browserDataFolder">The folder the browser keeps its data in.</param>
    public WpfDescriptionWebView(string? cloneFolder, string browserDataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(browserDataFolder);
        _cloneFolder = cloneFolder;
        _control = new WebView2CompositionControl
        {
            CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = browserDataFolder },
            // The page sets no background, so the WPF theme shows through.
            DefaultBackgroundColor = Color.Transparent,
        };
        _control.CoreWebView2InitializationCompleted += OnInitialized;
        // The control starts its browser by itself only when its Source is set, so start it once it is shown.
        _control.Loaded += OnLoaded;
    }

    public FrameworkElement Element => _control;

    public Uri CloneAddress { get; } = new($"https://{CloneHost}/");

    public event EventHandler<string>? LinkClicked;

    public event EventHandler<string>? Failed;

    public void Show(string page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_disposed)
        {
            return;
        }

        if (_pages.Request(page) is { } start)
        {
            Navigate(start);
        }
    }

    // Starts loading the page. Until its navigation completes, the old page's measurements are out of date and
    // no new one starts, since the page being replaced is about to lose the node ids a measurement holds.
    private void Navigate(string page)
    {
        _measurement++;
        _loadingId = null;
        _pageLoaded = false;
        try
        {
            _control.NavigateToString(page);
        }
        catch (Exception failure)
        {
            // The browser refuses the page, for example one over its size limit. The description can't be shown,
            // so this is the same failure as a browser that can't start, reported once and not thrown at the caller.
            if (!LoadNext())
            {
                Fail(failure);
            }
        }
    }

    // The loading page ended, loaded or not. A page asked for meanwhile starts at once and the ended page's
    // outcome no longer matters. Returns whether one started.
    private bool LoadNext()
    {
        if (_pages.Complete() is not { } next)
        {
            return false;
        }

        Navigate(next);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pages.Clear();
        if (_resized is not null)
        {
            _resized.DevToolsProtocolEventReceived -= OnFrameResized;
        }

        _control.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_startRequested || _disposed)
        {
            return;
        }

        _startRequested = true;
        try
        {
            await _control.EnsureCoreWebView2Async();
            if (_disposed)
            {
                return;
            }

            // The browser says when it has applied a new size to the page, which the WPF size change can't.
            var core = _control.CoreWebView2;
            _resized = core.GetDevToolsProtocolEventReceiver("Page.frameResized");
            _resized.DevToolsProtocolEventReceived += OnFrameResized;
            await core.CallDevToolsProtocolMethodAsync("Page.enable", "{}");
            if (!_disposed && _pages.Ready() is { } page)
            {
                Navigate(page);
            }
        }
        catch (Exception failure) when (!_disposed)
        {
            // The browser can't start, for example when the WebView2 Runtime is missing.
            Fail(failure);
        }
        catch (Exception) when (_disposed)
        {
            // Disposed while starting: the failure has no one left to hear it.
        }
    }

    private void OnInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (_disposed || !e.IsSuccess)
        {
            // A failure is reported by OnLoaded, from the exception the start throws.
            return;
        }

        var core = _control.CoreWebView2;
        core.Settings.IsScriptEnabled = false;
        if (_cloneFolder is not null)
        {
            core.SetVirtualHostNameToFolderMapping(CloneHost, _cloneFolder, CoreWebView2HostResourceAccessKind.DenyCors);
        }

        core.NavigationStarting += (_, args) =>
        {
            if (args.IsUserInitiated)
            {
                args.Cancel = true;
                LinkClicked?.Invoke(this, args.Uri);
            }
            else if (_pages.IsLoading)
            {
                // The page Show asked for, as the browser numbers it. A user navigation, cancelled above, isn't one.
                _loadingId = args.NavigationId;
            }
        };
        core.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            LinkClicked?.Invoke(this, args.Uri);
        };

        core.NavigationCompleted += OnNavigationCompleted;
        _control.SizeChanged += (_, _) => RemeasureIfIdle();
    }

    private void OnFrameResized(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e) => RemeasureIfIdle();

    // A new width reflows the page, and a new height can too, as a scrollbar appears or goes. Measuring again
    // ends once the height is the page's own, which raises no size change. While a page loads, its own
    // completion measures, at the size then. Before the first page has loaded there is nothing to measure.
    private void RemeasureIfIdle()
    {
        if (!_disposed && _pageLoaded && !_pages.IsLoading)
        {
            MeasureHeight();
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_disposed || _loadingId != e.NavigationId)
        {
            // The end of a page that a newer one replaced, or of a navigation the adapter didn't start.
            return;
        }

        if (LoadNext())
        {
            return;
        }

        if (e.IsSuccess)
        {
            _pageLoaded = true;
            MeasureHeight();
        }
        else
        {
            Fail(new InvalidOperationException($"The page did not load: {e.WebErrorStatus}."));
        }
    }

    // Sets the element's height to the height of the page's <html> box, so the page is never cut and, with no
    // script, a shorter page shrinks it again. The box is the page's content height, which the browser's viewport
    // height doesn't raise. A result made stale by a newer measurement, or by disposal, is dropped.
    private async void MeasureHeight()
    {
        var measurement = ++_measurement;
        try
        {
            var core = _control.CoreWebView2;
            var document = await CallAsync(core, "DOM.getDocument", "{\"depth\":0}");
            var root = document.GetProperty("root").GetProperty("nodeId").GetInt32();
            var html = await CallAsync(core, "DOM.querySelector", $"{{\"nodeId\":{root},\"selector\":\"html\"}}");
            var box = await CallAsync(core, "DOM.getBoxModel", $"{{\"nodeId\":{html.GetProperty("nodeId").GetInt32()}}}");
            if (!_disposed && measurement == _measurement)
            {
                _control.Height = Math.Ceiling(box.GetProperty("model").GetProperty("height").GetDouble());
            }
        }
        catch (Exception failure)
        {
            // An error from an old page's node ids, or after disposal, has no one left to hear it. Only an
            // error for the page now shown is a failure. Every case is handled here: nothing may escape async void.
            if (!_disposed && measurement == _measurement)
            {
                Fail(failure);
            }
        }
    }

    private static async Task<JsonElement> CallAsync(CoreWebView2 core, string method, string parameters)
    {
        using var result = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync(method, parameters));
        return result.RootElement.Clone();
    }

    // Raised once, so the exception reaches the listener and not the unhandled-exception handler.
    private void Fail(Exception failure)
    {
        if (_failed)
        {
            return;
        }

        _failed = true;
        Failed?.Invoke(this, failure.Message);
    }
}
