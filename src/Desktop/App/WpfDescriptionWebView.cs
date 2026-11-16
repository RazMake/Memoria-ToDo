using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using static Memoria.ToDo.Desktop.App.DevTools;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The description web view as a WebView2 control that draws inside WPF, so it scrolls and clips with the list.
/// The browser starts on the UI thread once the control is shown. It serves the clone folder at the clone address,
/// runs no scripts, and cancels every navigation the user starts, reporting its address instead.
/// </summary>
internal sealed class WpfDescriptionWebView : IDescriptionWebView
{
    private const string CloneHost = "clone.memoria-todo.example";

    private readonly PageImages _images;
    private readonly WebView2CompositionControl _control;
    private readonly PageLoads _pages = new();
    private bool _startRequested;
    private bool _disposed;
    private bool _failed;
    private bool _pageLoaded;
    private CoreWebView2DevToolsProtocolEventReceiver? _resized;
    private int _measurement;
    private ulong? _loadingId;
    private CoreWebView2DevToolsProtocolEventReceiver? _styleChanged;
    private readonly IDragPause _dragPause;
    private readonly IImageMeasure _measure;
    private HashSet<int> _boxes = [];
    private Task<int>? _root;
    private int _page;

    // Counts the drag steps and the pages shown, so a measurement that finishes after either knows it is stale.
    private int _drag;
    private int _draggedBox;

    /// <param name="cloneFolder">The folder the clone address serves, or null when there is no clone yet.</param>
    /// <param name="editImagesFolder">The folder of the images that wait for an open edit, served after the clone.</param>
    /// <param name="browserDataFolder">The folder the browser keeps its data in.</param>
    /// <param name="files">Reads the images.</param>
    /// <param name="rules">Tasks' file rules, which say which paths are images.</param>
    public WpfDescriptionWebView(string? cloneFolder, string editImagesFolder, string browserDataFolder, IFileSystem files, IRepositoryFileRules rules)
        : this(cloneFolder, editImagesFolder, browserDataFolder, files, rules,         new DispatcherDragPause(), new DevToolsImageMeasure())
    {
    }

    /// <param name="dragPause">Tells when a drag has ended, in place of the dispatcher's clock.</param>
            /// <param name="measure">Reads a dragged image box's width, in place of the DevTools calls.</param>
    internal WpfDescriptionWebView(
                string? cloneFolder, string editImagesFolder, string browserDataFolder, IFileSystem files, IRepositoryFileRules rules,
                IDragPause dragPause, IImageMeasure measure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(browserDataFolder);
        ArgumentNullException.ThrowIfNull(dragPause);
                ArgumentNullException.ThrowIfNull(measure);
                _measure = measure;
        _images = new PageImages(files, rules, cloneFolder, editImagesFolder);
        _dragPause = dragPause;
        _control = new WebView2CompositionControl
        {
            // The page never holds the keyboard focus, so every key reaches the window. A click on the page then moves the
            // focus to the nearest focusable ancestor, and Tab skips the page.
            Focusable = false,
            CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = browserDataFolder },
            // The page sets no background, so the WPF theme shows through.
            DefaultBackgroundColor = Color.Transparent,
        };
        _control.CoreWebView2InitializationCompleted += OnInitialized;
        // The control starts its browser by itself only when its Source is set, so start it once it is shown.
        _control.Loaded += OnLoaded;
        _dragPause.Tick += OnDragPause;
    }

    public FrameworkElement Element => _control;

    public Uri CloneAddress { get; } = new($"https://{CloneHost}/");

    public event EventHandler<string>? LinkClicked;

    public event EventHandler<string>? Failed;

    public event EventHandler<ImageResize>? ImageResized;

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
        _page++;
        _drag++;
        _dragPause.Stop();
        _boxes = [];
        _root = null;
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
        _dragPause.Stop();
        if (_resized is not null)
        {
            _resized.DevToolsProtocolEventReceived -= OnFrameResized;
        }

        if (_styleChanged is not null)
        {
            _styleChanged.DevToolsProtocolEventReceived -= OnStyleInvalidated;
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
            // The browser tells of a changed style only for nodes this client has been sent, and DOM.enable would
            // break the node queries, so MeasureHeight sends the image boxes.
            _styleChanged = core.GetDevToolsProtocolEventReceiver("DOM.inlineStyleInvalidated");
            _styleChanged.DevToolsProtocolEventReceived += OnStyleInvalidated;
            // The element is as tall as the page, so a scrollbar would only take width from a box the user drags wider.
            await core.CallDevToolsProtocolMethodAsync("Emulation.setScrollbarsHidden", "{\"hidden\":true}");
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
        // Every request to the clone's address is answered here, since a mapped host would raise no event.
        core.AddWebResourceRequestedFilter($"https://{CloneHost}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;

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

    // Answers a request to the clone's address with what PageImages finds for its path, with no query. The nosniff header
    // keeps the browser to the content type given.
    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var image = _images.Find(e.Request.Method, RawPath(e.Request.Uri));
        var headers = image.ContentType is null
            ? "X-Content-Type-Options: nosniff"
            : $"Content-Type: {image.ContentType}\r\nX-Content-Type-Options: nosniff";
        e.Response = _control.CoreWebView2.Environment.CreateWebResourceResponse(
            image.Content is null ? null : new MemoryStream(image.Content), image.Status, ReasonPhrase(image.Status), headers);
    }

    // The request's path as sent, still escaped, without query or fragment. Uri would decode it, and PageImages judges the raw path.
    private static string RawPath(string address)
    {
        var path = address.IndexOf('/', address.IndexOf("//", StringComparison.Ordinal) + 2);
        var end = address.IndexOfAny(['?', '#'], path);
        return end < 0 ? address[path..] : address[path..end];
    }

    private static string ReasonPhrase(int status) => status switch
    {
        200 => "OK",
        404 => "Not Found",
        _ => "Internal Server Error",
    };

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
            var root = await (_root ??= BindPage(core, _page));
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

    // Sends the page's document and its image boxes to the browser, which tells of a changed style only for nodes
    // it has sent. A later DOM.getDocument discards the ids sent so far, so it runs once for each page: the ids
    // stay valid while the user drags, though the drag resizes the page and so measures it again.
    private async Task<int> BindPage(CoreWebView2 core, int page)
    {
        var document = await CallAsync(core, "DOM.getDocument", "{\"depth\":0}");
        var root = document.GetProperty("root").GetProperty("nodeId").GetInt32();
        var boxes = await CallAsync(core, "DOM.querySelectorAll", $"{{\"nodeId\":{root},\"selector\":\"[data-image]\"}}");
        if (page == _page)
        {
            _boxes = boxes.GetProperty("nodeIds").EnumerateArray().Select(id => id.GetInt32()).ToHashSet();
        }

        return root;
    }

    // A drag step changed a box's style: the drag goes on until the box's style has been quiet for the pause.
    private void OnStyleInvalidated(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        using var changed = JsonDocument.Parse(e.ParameterObjectAsJson);
        foreach (var id in changed.RootElement.GetProperty("nodeIds").EnumerateArray())
        {
            if (_boxes.Contains(id.GetInt32()))
            {
                _draggedBox = id.GetInt32();
                _drag++;
                _dragPause.Stop();
                _dragPause.Start();
                // The box changes the page's height, which no size change tells the control.
                RemeasureIfIdle();
            }
        }
    }

    private async void OnDragPause(object? sender, EventArgs e)
    {
        _dragPause.Stop();
        if (_dragPause.IsButtonDown)
        {
            // The user holds the button, so the drag goes on, only still. It ends when the button is released.
            _dragPause.Start();
            return;
        }

        var page = _page;
        var drag = _drag;
        var node = _draggedBox;
        try
        {
            var resize = await _measure.MeasureAsync(_control.CoreWebView2, node);
            // The measurement's round trips took time. The measurement is stale once another drag step (which started the pause
            // again), another page, or a failure came in.
            if (_disposed || _failed || page != _page || drag != _drag)
            {
                return;
            }

            if (_dragPause.IsButtonDown)
            {
                // Pressed again with no step yet: that drag ends when the button is released.
                _dragPause.Start();
                return;
            }

            ImageResized?.Invoke(this, resize);
        }
        catch (Exception failure)
        {
            // Every case is handled here: nothing may escape async void. A page replaced meanwhile has no one
            // left to hear its error.
            if (!_disposed && page == _page)
            {
                Fail(failure);
            }
        }
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
