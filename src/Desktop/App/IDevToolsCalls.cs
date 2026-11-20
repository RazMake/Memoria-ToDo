using System.Text.Json;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Calls the browser's DevTools protocol on the page shown.</summary>
internal interface IDevToolsCalls
{
    /// <summary>Calls <paramref name="method"/> with its parameters as JSON and returns its result, which stays valid after the call.</summary>
    Task<JsonElement> CallAsync(string method, string parametersJson);
}
