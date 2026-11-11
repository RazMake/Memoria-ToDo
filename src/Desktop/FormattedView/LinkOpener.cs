using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>
/// What a click on a link does: an address that passes Rendering's browser check opens in the default browser, and any other
/// is reported. A browser that fails to open is reported too, never thrown into the click.
/// </summary>
internal sealed class LinkOpener(IDefaultBrowser browser, Action<string> otherLink, Action<Exception> openFailed)
{
    public void Click(string address)
    {
        if (!BrowserCheck.MayOpen(address))
        {
            otherLink(address);
            return;
        }

        try
        {
            browser.Open(address);
        }
        catch (Exception failure)
        {
            openFailed(failure);
        }
    }
}
