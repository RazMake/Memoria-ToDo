using System.Text.RegularExpressions;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Parses the two supported SSH remote forms, `[user@]host:path` and `ssh://[user@]host[:port]/path`.</summary>
internal static partial class AddressParser
{
    public static SshAddress Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("The repository address is empty.", nameof(text));
        }

        text = text.Trim();
        return text.Contains("://", StringComparison.Ordinal) ? ParseUrl(text) : ParseScp(text);
    }

    /// <summary>The SSH user of an address, `git` when the address names none.</summary>
    public static string User(string text)
    {
        var authority = text.Trim();
        var scheme = authority.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            authority = authority[(scheme + 3)..];
        }

        var at = authority.IndexOf('@');
        var end = authority.IndexOfAny(['/', ':']);
        return at > 0 && (end < 0 || at < end) ? authority[..at] : "git";
    }

    /// <summary>The page where the user adds a deploy key for a well-known host, or null.</summary>
    public static Uri? AddKeyPage(SshAddress address)
    {
        var segments = address.Path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var repository = segments.Length > 0 ? RemoveGitSuffix(segments[^1]) : "";
        switch (address.Host)
        {
            case "github.com" when segments.Length == 2:
                return new Uri($"https://github.com/{segments[0]}/{repository}/settings/keys/new");
            case "gitlab.com" when segments.Length >= 2:
                var project = string.Join('/', segments[..^1].Append(repository));
                return new Uri($"https://gitlab.com/{project}/-/deploy_keys");
            case "bitbucket.org" or "ssh.bitbucket.org":
                return new Uri("https://bitbucket.org/account/settings/ssh-keys/");
            case "ssh.dev.azure.com" when segments.Length == 4 && segments[0] == "v3":
                return new Uri($"https://dev.azure.com/{segments[1]}/_usersSettings/keys");
            default:
                return null;
        }
    }

    /// <summary>The add-key page for an origin the clone already has, or null when the text isn't a supported address.</summary>
    public static Uri? AddKeyPage(string originText) =>
        TryParse(originText) is { } address ? AddKeyPage(address) : null;

    private static SshAddress? TryParse(string text)
    {
        try
        {
            return Parse(text);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static SshAddress ParseScp(string text)
    {
        var match = ScpForm().Match(text);
        if (!match.Success || DrivePath().IsMatch(text))
        {
            throw new ArgumentException($"'{text}' is not an SSH address like git@host:owner/repo.git.", nameof(text));
        }

        var rawHost = match.Groups["host"].Value;
        // A bare one-letter host reads as a Windows drive to some Git clients, so its text names the user explicitly.
        var canonical = rawHost.Length == 1 && !match.Groups["user"].Success ? $"{User(text)}@{text}" : text;
        return new SshAddress(canonical, NormalizeHost(rawHost), 22, match.Groups["path"].Value);
    }

    private static SshAddress ParseUrl(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "ssh" || uri.Host.Length == 0)
        {
            throw new ArgumentException($"'{text}' is not an ssh:// address.", nameof(text));
        }

        var port = uri.IsDefaultPort ? 22 : uri.Port;
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (port is < 1 or > 65535 || path.Trim('/').Length == 0)
        {
            throw new ArgumentException($"'{text}' needs a valid port and a repository path.", nameof(text));
        }

        return new SshAddress(text, NormalizeHost(uri.Host), port, path);
    }

    internal static string NormalizeHost(string host) => host.TrimEnd('.').ToLowerInvariant();

    private static string RemoveGitSuffix(string name) =>
        name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    // A Windows drive path such as C:\tasks or C:/tasks, not a one-letter host; `g:me/tasks.git` stays an address.
    [GeneratedRegex(@"^[A-Za-z]:[\\/]")]
    private static partial Regex DrivePath();

    [GeneratedRegex(@"^(?:(?<user>[^@/:\s]+)@)?(?<host>[^@/:\s]+):(?<path>[^\s]+)$")]
    private static partial Regex ScpForm();
}
