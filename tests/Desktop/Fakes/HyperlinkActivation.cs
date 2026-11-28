using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Documents;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>Follows a link as assistive tools do, through its automation Invoke pattern. Shared test setup, not a fake of a port.</summary>
public static class HyperlinkActivation
{
    /// <summary>Invokes the link, which raises its navigation as a click that completes does. It sends no mouse input.</summary>
    public static void Invoke(Hyperlink link)
    {
        var peer = ContentElementAutomationPeer.CreatePeerForElement(link);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }
}
