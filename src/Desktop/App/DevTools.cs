using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The browser's DevTools protocol calls the description web view's parts make.</summary>
internal static class DevTools
{
    /// <summary>Calls <paramref name="method"/> and returns its result, which stays valid after the call.</summary>
    public static async Task<JsonElement> CallAsync(CoreWebView2 core, string method, string parameters)
    {
        using var result = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync(method, parameters));
        return result.RootElement.Clone();
    }
}
