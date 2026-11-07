namespace Memoria.Proofs;

/// <summary>
/// SHA256 host key fingerprints the hosts publish. GitHub: https://api.github.com/meta.
/// Azure DevOps: https://learn.microsoft.com/azure/devops/repos/git/use-ssh-keys-to-authenticate.
/// </summary>
public static class PublishedHostKeys
{
    public static IReadOnlyDictionary<string, string[]> Pins { get; } = new Dictionary<string, string[]>
    {
        ["github.com"] =
        [
            "uNiVztksCsDhcc0u9e8BujQXVUpKZIDTMczCvj3tD2s",
            "p2QAMXNIC1TJYWeIOttrVc98/R1BUFWu3/LiyKgUfQM",
            "+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU",
        ],
        ["ssh.dev.azure.com"] = ["ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og"],
    };
}
