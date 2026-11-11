using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IDescriptionWebView"/>. It shows no page: it records the pages it would show.
/// Like the real web view, it holds the last page until <see cref="Start"/>, as a browser that is still starting does.
/// A test starts it, clicks links in it, and makes it fail.
/// </summary>
public sealed class FakeDescriptionWebView : IDescriptionWebView
{
    private readonly CountSignal shown = new();
    private readonly List<string> pages = [];
    private string? waiting;
    private bool started;

    /// <summary>Starts as if the browser is ready, so a page shows at once. Pass false to make a test start it.</summary>
    public FakeDescriptionWebView(bool started = true) => this.started = started;

    /// <inheritdoc />
    public FrameworkElement Element { get; } = new Border();

    /// <inheritdoc />
    public Uri CloneAddress { get; set; } = new("https://clone.fake.example/");

    /// <summary>When set, the web view fails with this reason as soon as it is asked to show a page, before <c>Show</c> returns.</summary>
    public string? FailWhenShowing { get; set; }

    /// <summary>The pages shown, in order. Read it on the UI thread, where the web view is used.</summary>
    public IReadOnlyList<string> Pages => pages;

    /// <summary>The page on screen now; null before one has shown.</summary>
    public string? Page => pages.Count == 0 ? null : pages[^1];

    /// <summary>Whether the web view was released.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public event EventHandler<string>? LinkClicked;

    /// <inheritdoc />
    public event EventHandler<string>? Failed;

    /// <inheritdoc />
    public void Show(string page)
    {
        if (FailWhenShowing is { } reason)
        {
            Fail(reason);
            return;
        }

        if (started)
        {
            Add(page);
        }
        else
        {
            waiting = page;
        }
    }

    /// <summary>The browser has started: the last page that waited shows now.</summary>
    public void Start()
    {
        started = true;
        if (waiting is { } page)
        {
            waiting = null;
            Add(page);
        }
    }

    /// <summary>The user clicks a link with the address in the page.</summary>
    public void ClickLink(string address) => LinkClicked?.Invoke(this, address);

    /// <summary>The browser can't start, for the reason.</summary>
    public void Fail(string reason) => Failed?.Invoke(this, reason);

    /// <summary>Completes once at least <paramref name="count"/> pages have shown.</summary>
    public Task WaitForPagesAsync(int count) => shown.WaitForAsync(count);

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;

    private void Add(string page)
    {
        pages.Add(page);
        shown.Increment();
    }
}
