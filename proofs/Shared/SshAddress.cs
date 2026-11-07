namespace Memoria.Proofs;

/// <summary>
/// A git SSH address in either form: <c>ssh://[user@]host[:port]/path</c> or <c>[user@]host:path</c>.
/// </summary>
/// <param name="Path">The path git sends to the server: absolute for the URL form, as typed for the scp form.</param>
public sealed record SshAddress(string User, string Host, int Port, string Path)
{
    private const string Scheme = "ssh://";

    public static SshAddress Parse(string address)
    {
        if (address.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(address);
            string path = Uri.UnescapeDataString(uri.AbsolutePath);

            // Like git, "ssh://host/~user/repo" means "~user/repo".
            if (path.StartsWith("/~", StringComparison.Ordinal))
            {
                path = path[1..];
            }

            return new SshAddress(
                RequireUser(Uri.UnescapeDataString(uri.UserInfo), address),
                uri.Host,
                uri.Port < 0 ? 22 : uri.Port,
                path);
        }

        int colon = address.IndexOf(':');
        int slash = address.IndexOf('/');
        if (colon <= 0 || (slash >= 0 && slash < colon) || colon == address.Length - 1)
        {
            throw new FormatException($"'{address}' is neither an ssh:// URL nor a user@host:path address.");
        }

        string userAndHost = address[..colon];
        int at = userAndHost.LastIndexOf('@');
        return new SshAddress(
            RequireUser(at < 0 ? string.Empty : userAndHost[..at], address),
            userAndHost[(at + 1)..],
            22,
            address[(colon + 1)..]);
    }

    public string ToUrlForm() =>
        $"{Scheme}{User}@{Host}{(Port == 22 ? string.Empty : $":{Port}")}/{Path.TrimStart('/')}";

    public string ToScpForm() => Port == 22
        ? $"{User}@{Host}:{Path.TrimStart('/')}"
        : throw new InvalidOperationException($"The scp form can't name port {Port}.");

    private static string RequireUser(string user, string address) => user.Length > 0
        ? user
        : throw new FormatException($"'{address}' names no user; git hosts expect one, such as git@.");
}
