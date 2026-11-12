using System.Windows;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Configure's setup actions on Windows: the WPF clipboard and the default browser.</summary>
internal sealed class WindowsSetupActions(IDefaultBrowser browser) : ISetupActions
{
    public void CopyPublicKey(string publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        Clipboard.SetText(publicKey);
    }

    public void OpenKeyPage(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        browser.Open(address.AbsoluteUri);
    }
}
