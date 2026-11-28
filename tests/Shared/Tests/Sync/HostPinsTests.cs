using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>The built-in hosts' published fingerprints, which Sync trusts without asking the user (docs/architecture/sync.md).</summary>
[Unit]
public sealed class HostPinsTests
{
    [Theory]
    [InlineData("github.com", "ssh-rsa", "SHA256:uNiVztksCsDhcc0u9e8BujQXVUpKZIDTMczCvj3tD2s")]
    [InlineData("github.com", "ecdsa-sha2-nistp256", "SHA256:p2QAMXNIC1TJYWeIOttrVc98/R1BUFWu3/LiyKgUfQM")]
    [InlineData("github.com", "ssh-ed25519", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU")]
    [InlineData("gitlab.com", "rsa-sha2-512", "SHA256:ROQFvPThGrW4RuWLoL9tq9I9zJ42fK4XywyRtbOz/EQ")]
    [InlineData("gitlab.com", "ecdsa-sha2-nistp256", "SHA256:HbW3g8zUjNSksFbqTiUWPWg2Bq1x8xdGUrliXFzSnUw")]
    [InlineData("gitlab.com", "ssh-ed25519", "SHA256:eUXGGm1YGsMAS7vkcx6JOJdOGHPem5gQp4taiCfCLB8")]
    [InlineData("bitbucket.org", "ssh-rsa", "SHA256:46OSHA1Rmj8E8ERTC6xkNcmGOw9oFxYr0WF6zWW8l1E")]
    [InlineData("bitbucket.org", "ecdsa-sha2-nistp256", "SHA256:FC73VB6C4OQLSCrjEayhMp9UMxS97caD/Yyi2bhW/J0")]
    [InlineData("bitbucket.org", "ssh-ed25519", "SHA256:ybgmFkzwOSotHTHLJgHO0QN8L0xErw6vd0VhFA9m3SM")]
    [InlineData("ssh.bitbucket.org", "ssh-rsa", "SHA256:46OSHA1Rmj8E8ERTC6xkNcmGOw9oFxYr0WF6zWW8l1E")]
    [InlineData("ssh.bitbucket.org", "ecdsa-sha2-nistp256", "SHA256:FC73VB6C4OQLSCrjEayhMp9UMxS97caD/Yyi2bhW/J0")]
    [InlineData("ssh.bitbucket.org", "ssh-ed25519", "SHA256:ybgmFkzwOSotHTHLJgHO0QN8L0xErw6vd0VhFA9m3SM")]
    [InlineData("ssh.dev.azure.com", "ssh-rsa", "SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og")]
    public void Trusts_each_published_fingerprint_of_a_built_in_host(string host, string algorithm, string fingerprint)
    {
        Assert.True(HostPins.BuiltIn.IsBuiltIn(host));
        Assert.True(HostPins.BuiltIn.Matches(host, 22, algorithm, fingerprint));
    }

    [Theory]
    [InlineData("github.com", 22, "ssh-ed25519", "SHA256:uNiVztksCsDhcc0u9e8BujQXVUpKZIDTMczCvj3tD2s")] // another family's fingerprint
    [InlineData("github.com", 2222, "ssh-ed25519", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU")] // not the published port
    [InlineData("ssh.dev.azure.com", 22, "ssh-ed25519", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU")] // no published key of that family
    [InlineData("github.com", 22, "ssh-dss", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU")] // unknown algorithm
    [InlineData("git.example.org", 22, "ssh-ed25519", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU")] // not a built-in host
    public void Does_not_trust_a_key_that_is_not_published_for_that_host_port_and_family(string host, int port, string algorithm, string fingerprint)
    {
        Assert.False(HostPins.BuiltIn.Matches(host, port, algorithm, fingerprint));
    }
}
