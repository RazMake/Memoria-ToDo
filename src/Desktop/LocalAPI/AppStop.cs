namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The cancellation of the app's stop, for the stream being run. <see cref="CallInterceptor"/> sets it around a stream's handler and
/// <see cref="ChangeStream"/> reads it, so a stream ends when the app stops. It flows with the call, so the handler keeps the web
/// server's own call context, which the server needs to find the handler's service.
/// </summary>
internal static class AppStop
{
    private static readonly AsyncLocal<CancellationToken> Current = new();

    /// <summary>The token of the stop that ends the stream running on this flow; never cancelled outside a stream.</summary>
    public static CancellationToken Token
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
