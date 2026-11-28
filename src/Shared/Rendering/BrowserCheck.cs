namespace Memoria.ToDo.Shared.Rendering;

/// <summary>Which links may open in the default browser: only absolute web addresses.</summary>
public static class BrowserCheck
{
    /// <summary>Whether the address may open in the default browser: true only for an absolute http or https address.</summary>
    public static bool MayOpen(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Length > 0;
    }
}
