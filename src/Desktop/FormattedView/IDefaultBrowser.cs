namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>The system's default browser.</summary>
public interface IDefaultBrowser
{
    /// <summary>
    /// Opens the address. The Formatted view passes only addresses that pass Rendering's browser check.
    /// Throws when the browser can't open it.
    /// </summary>
    void Open(string address);
}
