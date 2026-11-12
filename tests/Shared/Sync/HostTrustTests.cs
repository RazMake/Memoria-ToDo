using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>Which SSH host keys Sync trusts: built-in pins, custom hosts the user accepted, and changed keys.</summary>
[Unit]
public sealed class HostTrustTests
{
    private const string CustomAddress = "git@git.example.org:me/tasks.git";
    private const string CustomHost = "git.example.org";
    private const string GitHubAddress = "git@github.com:me/tasks.git";
    private const string GitHubEd25519 = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";

    private readonly SyncFixture f = new();

    [Fact]
    public async Task Offers_an_unknown_custom_host_key_for_acceptance_and_refuses_the_connection()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        var offered = sync.Setup.State.HostKey!;
        Assert.Equal(CustomHost, offered.Host);
        Assert.Equal(22, offered.Port);
        Assert.Equal("ssh-ed25519", offered.Algorithm);
        Assert.Equal("SHA256:first", offered.Fingerprint);
        Assert.True(offered.CanAccept);
        Assert.Equal(SyncFailure.UnknownHostKey, sync.Status.Failure);
        Assert.Empty(f.Selection.Selected);
    }

    [Fact]
    public async Task Trusts_a_custom_host_after_its_exact_key_is_accepted()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);
        await sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None);

        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
        var saved = f.ReadDeviceFile("known-hosts.json");
        Assert.Contains(CustomHost, saved);
        Assert.Contains("SHA256:first", saved);
    }

    [Fact]
    public async Task Refuses_to_accept_a_fingerprint_that_was_not_offered_and_saves_nothing()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:other", CancellationToken.None));

        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
    }

    [Fact]
    public async Task Scopes_custom_trust_to_the_host_and_port()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync("ssh://git@git.example.org:2222/me/tasks.git", f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
        Assert.Equal(2222, sync.Setup.State.HostKey!.Port);
    }

    [Fact]
    public async Task Offers_the_key_again_after_a_custom_host_is_forgotten()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);

        await sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
        Assert.True(sync.Setup.State.HostKey!.CanAccept);
    }

    [Fact]
    public async Task Forgets_a_custom_host_named_with_different_case_or_a_trailing_dot()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);

        await sync.Setup.ForgetHostKeyAsync("GIT.Example.org.", 22, CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
    }

    [Fact]
    public async Task Refuses_a_changed_custom_host_key_and_does_not_let_it_be_accepted()
    {
        f.WithKey();
        var saved = TrustedFile(CustomHost, 22, "SHA256:first");
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(SyncFailure.ChangedHostKey, sync.Status.Failure);
        Assert.False(sync.Setup.State.HostKey!.CanAccept);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None));
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Accepts_a_replacement_key_only_after_the_old_one_is_forgotten()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile(CustomHost, 22, "SHA256:first"));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();
        await sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.True(sync.Setup.State.HostKey!.CanAccept);
        await sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None);
        await sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None);
        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
    }

    [Fact]
    public async Task Trusts_a_built_in_host_by_its_published_fingerprint_without_acceptance()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", GitHubEd25519);
        await using var sync = f.Create();

        await sync.Setup.CloneAsync(GitHubAddress, f.CloneFolder, CancellationToken.None);

        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
    }

    [Fact]
    public async Task Compares_host_names_without_case_or_a_trailing_dot()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", GitHubEd25519);
        await using var sync = f.Create();

        await sync.Setup.CloneAsync("git@GitHub.COM.:me/tasks.git", f.CloneFolder, CancellationToken.None);

        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
    }

    [Fact]
    public async Task Refuses_a_wrong_key_from_a_built_in_host_and_cannot_be_overridden()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:forged");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(GitHubAddress, f.CloneFolder, CancellationToken.None));
        Assert.Equal(SyncFailure.ChangedHostKey, sync.Status.Failure);
        Assert.False(sync.Setup.State.HostKey!.CanAccept);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:forged", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.ForgetHostKeyAsync("github.com", 22, CancellationToken.None));
        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(GitHubAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        f.Git.OfferedHostKey = ("ssh-ed25519", GitHubEd25519);
        await sync.Setup.CloneAsync(GitHubAddress, f.CloneFolder, CancellationToken.None);
        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
    }

    [Fact]
    public async Task Does_not_trust_anything_when_the_saved_trust_file_has_an_unknown_version()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", "{\"version\":2,\"hosts\":[]}");
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.DoesNotContain(HostKeyVerdict.Trusted, f.Git.Verdicts);
        Assert.False(sync.Setup.State.HostKey!.CanAccept);
        Assert.Equal("{\"version\":2,\"hosts\":[]}", f.ReadDeviceFile("known-hosts.json"));
    }

    [Theory]
    [InlineData("{\"version\":2,\"hosts\":[]}")]
    [InlineData("not json")]
    [InlineData("{\"version\":1,\"hosts\":[{\"host\":\"git.example.org\"}]}")]
    public async Task Fails_with_a_local_storage_error_and_cannot_accept_a_key_when_the_saved_trust_is_unusable(string saved)
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.DoesNotContain(HostKeyVerdict.Unknown, f.Git.Verdicts);
        Assert.True(sync.Setup.State.HostKey is not { CanAccept: true });
        await Assert.ThrowsAnyAsync<Exception>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Theory]
    [InlineData("""{"version":1,"hosts":[{"host":null,"port":22,"fingerprint":"SHA256:first"}]}""")]
    [InlineData("""{"version":1,"hosts":[{"host":"","port":22,"fingerprint":"SHA256:first"}]}""")]
    [InlineData("""{"version":1,"hosts":[{"host":"git.example.org","port":22,"fingerprint":null}]}""")]
    [InlineData("""{"version":1,"hosts":[{"host":"git.example.org","port":22,"fingerprint":""}]}""")]
    [InlineData("""{"version":1,"hosts":[{"host":"   ","port":22,"fingerprint":"SHA256:first"}]}""")]
    [InlineData("""{"version":1,"hosts":[{"host":"git.example.org","port":22,"fingerprint":"   "}]}""")]
    public async Task Fails_with_a_local_storage_error_and_cannot_accept_a_key_when_a_saved_trust_entry_has_a_null_or_empty_host_or_fingerprint(string saved)
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        var failure = await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.IsType<InvalidDataException>(failure.InnerException);
        Assert.DoesNotContain(HostKeyVerdict.Unknown, f.Git.Verdicts);
        Assert.True(sync.Setup.State.HostKey is not { CanAccept: true });
        await Assert.ThrowsAnyAsync<Exception>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Fails_with_a_local_storage_error_and_cannot_accept_a_key_when_the_saved_trust_cannot_be_read()
    {
        f.WithKey();
        const string saved = "{\"version\":1,\"hosts\":[]}";
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Files.FailReadsUnder(f.DeviceFolder);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.DoesNotContain(HostKeyVerdict.Unknown, f.Git.Verdicts);
        Assert.True(sync.Setup.State.HostKey is not { CanAccept: true });
        await Assert.ThrowsAnyAsync<Exception>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
        f.Files.StopFailing();
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    private static string TrustedFile(string host, int port, string fingerprint) =>
        $$"""{"version":1,"hosts":[{"host":"{{host}}","port":{{port}},"fingerprint":"{{fingerprint}}"}]}""";
}
