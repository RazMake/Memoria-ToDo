using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The DevTools calls of one browser, made through <see cref="DevTools"/>.</summary>
internal sealed class CoreDevToolsCalls(CoreWebView2 core) : IDevToolsCalls
{
    public Task<JsonElement> CallAsync(string method, string parametersJson) => DevTools.CallAsync(core, method, parametersJson);
}
