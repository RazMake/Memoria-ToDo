namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Which page the description browser should load and when. One page loads at a time. It holds the page asked
/// for while the browser isn't ready or another page is loading, replaced by each newer one.
/// </summary>
internal sealed class PageLoads
{
    private string? _waiting;
    private bool _ready;

    /// <summary>Whether a page the caller was told to load has not completed yet.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>A page is asked for. Returns the page to load now, or null when it waits for the browser to be ready.</summary>
    public string? Request(string page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!_ready || IsLoading)
        {
            _waiting = page;
            return null;
        }

        IsLoading = true;
        return page;
    }

    /// <summary>The browser is ready. Returns the latest page asked for before, to load now, or null when there is none.</summary>
    public string? Ready()
    {
        _ready = true;
        var page = _waiting;
        _waiting = null;
        IsLoading = page is not null;
        return page;
    }

    /// <summary>The loading page ended, loaded or not. Returns the latest page asked for meanwhile, to load now, or null when there is none.</summary>
    public string? Complete()
    {
        var next = _waiting;
        _waiting = null;
        IsLoading = next is not null;
        return next;
    }

    /// <summary>Forgets the waiting page, so it is no longer held.</summary>
    public void Clear() => _waiting = null;
}
