using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>Which SSH host keys Sync trusts: built-in pins, custom hosts the user accepted, and changed keys.</summary>
[Unit]
public sealed class HostTrustTests
{
    private const string GitHubAddress = "git@github.com:me/tasks.git";
    private const string GitHubEd25519 = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";
    private const string OtherHostAddress = "git@other.example.org:me/tasks.git";

    private readonly SyncFixture f = new();

    private const string CustomAddress = SyncFixture.ScpAddress;

    private string CustomHost => f.Address.Host;

    private string OtherPortAddress => $"ssh://git@{CustomHost}:2222/me/tasks.git";

    private async Task AssertTrustedAsync(ISyncService sync, string address)
    {
        await sync.Setup.CloneAsync(address, f.CloneFolder, CancellationToken.None);
        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
    }

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
        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
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
    }

    [Fact]
    public async Task Keeps_trusting_an_accepted_custom_host_in_a_new_service_that_reads_the_saved_trust()
    {
        f.WithKey();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using (var first = f.Create())
        {
            await Assert.ThrowsAsync<IOException>(() => first.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
            await first.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);
        }

        await using var second = f.Create();
        await second.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None);

        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
        Assert.True(second.Setup.State.HostKey is not { CanAccept: true });
    }

    [Fact]
    public async Task Keeps_the_trust_of_other_hosts_and_other_ports_when_a_new_host_key_is_accepted()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile(("other.example.org", 22, "SHA256:first"), (CustomHost, 2222, "SHA256:first")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);

        await AssertTrustedAsync(sync, CustomAddress);
        await AssertTrustedAsync(sync, OtherPortAddress);
        await AssertTrustedAsync(sync, OtherHostAddress);
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

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(OtherPortAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
        Assert.Equal(2222, sync.Setup.State.HostKey!.Port);

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(OtherHostAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
        Assert.Equal("other.example.org", sync.Setup.State.HostKey!.Host);
    }

    [Fact]
    public async Task Refuses_a_changed_custom_host_key_and_offers_it_for_acceptance_without_saving_it()
    {
        f.WithKey();
        var saved = TrustedFile((CustomHost, 22, "SHA256:first"));
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        Assert.Equal(SyncFailure.ChangedHostKey, sync.Status.Failure);
        Assert.True(sync.Setup.State.HostKey!.CanAccept);
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Offers_a_changed_key_for_acceptance_when_a_configured_service_syncs_and_leaves_the_saved_trust_unchanged()
    {
        f.Ready();
        var saved = TrustedFile((CustomHost, 22, "SHA256:first"));
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        Assert.Equal(SyncState.Error, returned.State);
        Assert.Null(returned.LastSuccessfulSync);
        Assert.True(sync.Setup.State.HostKey!.CanAccept);
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Replaces_the_saved_key_of_a_custom_host_when_its_changed_key_is_accepted()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:first")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None);

        await AssertTrustedAsync(sync, CustomAddress);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
    }

    [Fact]
    public async Task Keeps_the_trust_of_other_hosts_and_other_ports_when_a_changed_key_is_accepted()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:old"), (CustomHost, 2222, "SHA256:first"), ("other.example.org", 22, "SHA256:first")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None);

        await AssertTrustedAsync(sync, CustomAddress);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await AssertTrustedAsync(sync, OtherPortAddress);
        await AssertTrustedAsync(sync, OtherHostAddress);
    }

    [Fact]
    public async Task Replaces_every_saved_key_of_a_host_and_port_when_a_changed_key_is_accepted()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:first"), (CustomHost, 22, "SHA256:third")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None);

        await AssertTrustedAsync(sync, CustomAddress);
        foreach (var old in new[] { "SHA256:first", "SHA256:third" })
        {
            f.Git.OfferedHostKey = ("ssh-ed25519", old);
            await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
            Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        }
    }

    [Fact]
    public async Task Replaces_saved_keys_under_case_and_trailing_dot_aliases_of_the_host_when_a_changed_key_is_accepted()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:first"), (CustomHost.ToUpperInvariant() + ".", 22, "SHA256:third"), (CustomHost.ToUpperInvariant(), 22, "SHA256:fourth")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None);

        await AssertTrustedAsync(sync, CustomAddress);
        foreach (var old in new[] { "SHA256:first", "SHA256:third", "SHA256:fourth" })
        {
            f.Git.OfferedHostKey = ("ssh-ed25519", old);
            await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
            Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        }

        var savedTrust = f.ReadDeviceFile("known-hosts.json");
        Assert.Contains("SHA256:second", savedTrust);
        Assert.DoesNotContain("SHA256:first", savedTrust);
        Assert.DoesNotContain("SHA256:third", savedTrust);
        Assert.DoesNotContain("SHA256:fourth", savedTrust);
    }

    [Fact]
    public async Task Trusts_a_key_saved_under_a_case_and_trailing_dot_alias_of_the_host()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost.ToUpperInvariant() + ".", 22, "SHA256:first")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        await AssertTrustedAsync(sync, CustomAddress);
    }

    [Fact]
    public async Task Refuses_to_accept_a_fingerprint_other_than_the_offered_changed_key_and_changes_nothing()
    {
        f.WithKey();
        var saved = TrustedFile((CustomHost, 22, "SHA256:first"));
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:other", CancellationToken.None));

        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Refuses_to_accept_a_key_when_none_is_pending_and_changes_nothing()
    {
        f.WithKey();
        await using var sync = f.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));

        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
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
        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        Assert.Equal(SyncFailure.ChangedHostKey, sync.Status.Failure);
        Assert.False(sync.Setup.State.HostKey!.CanAccept);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:forged", CancellationToken.None));
        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(GitHubAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        f.Git.OfferedHostKey = ("ssh-ed25519", GitHubEd25519);
        await sync.Setup.CloneAsync(GitHubAddress, f.CloneFolder, CancellationToken.None);
        Assert.Equal(HostKeyVerdict.Trusted, f.Git.Verdicts[^1]);
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
        Assert.DoesNotContain(HostKeyVerdict.Trusted, f.Git.Verdicts);
        AssertOfferedButNotAcceptable(sync);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
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
        var cause = Assert.IsType<GitFailureException>(failure.InnerException);
        Assert.Equal(SyncFailure.LocalStorage, cause.Kind);
        Assert.IsType<InvalidDataException>(cause.InnerException);
        Assert.DoesNotContain(HostKeyVerdict.Unknown, f.Git.Verdicts);
        AssertOfferedButNotAcceptable(sync);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
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
        AssertOfferedButNotAcceptable(sync);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
        f.Files.StopFailing();
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Shows_the_offered_key_as_not_acceptable_when_the_trust_cannot_be_loaded_and_the_setup_refresh_after_the_pass_also_fails()
    {
        f.Ready();
        f.WriteDeviceFile("known-hosts.json", "not json");
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        f.Git.OriginReadFailure = new IOException("Unreadable clone.");
        await using var sync = f.Create();

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Contains("Unreadable clone.", returned.Details);
        AssertOfferedButNotAcceptable(sync);
    }

    [Fact]
    public async Task Keeps_the_offered_key_visible_but_not_acceptable_when_accepting_it_fails_because_the_saved_trust_became_unusable()
    {
        await using var sync = await OfferUnknownKeyThenCorruptTrustAsync();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));

        AssertOfferedButNotAcceptable(sync);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
    }

    [Fact]
    public async Task Keeps_the_offered_key_not_acceptable_after_a_successful_setup_refresh_while_the_saved_trust_is_still_unusable()
    {
        await using var sync = await OfferUnknownKeyThenCorruptTrustAsync();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));

        await sync.Setup.SetDeviceNameAsync("Renamed", CancellationToken.None);

        AssertOfferedButNotAcceptable(sync);
    }

    [Fact]
    public async Task Keeps_the_offered_key_not_acceptable_after_a_failed_setup_refresh_while_the_saved_trust_is_still_unusable()
    {
        await using var sync = await OfferUnknownKeyThenCorruptTrustAsync();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None));
        f.Git.OriginReadFailure = new IOException("Unreadable clone.");

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.SetDeviceNameAsync("Renamed", CancellationToken.None));

        AssertOfferedButNotAcceptable(sync);
    }

    [Fact]
    public async Task Still_lets_an_unknown_key_be_accepted_when_only_the_setup_refresh_fails_and_the_saved_trust_is_fine()
    {
        f.Ready();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        f.Git.OriginReadFailure = new IOException("Unreadable clone.");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.True(sync.Setup.State.HostKey!.CanAccept);
        f.Git.OriginReadFailure = null;
        await sync.Setup.AcceptHostKeyAsync("SHA256:first", CancellationToken.None);
        await AssertTrustedAsync(sync, CustomAddress);
    }

    [Fact]
    public async Task Reports_an_unexpected_error_and_trusts_nothing_when_a_dependency_is_disposed_while_the_saved_trust_is_read_during_a_pass()
    {
        f.Ready();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:first")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();
        f.Files.FailReadsUnder(f.DeviceFolder, new ObjectDisposedException("filesystem"));

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.Unexpected, returned.Failure);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        Assert.DoesNotContain(HostKeyVerdict.Trusted, f.Git.Verdicts);
        AssertOfferedButNotAcceptable(sync);
    }

    // An unknown key is offered for acceptance, then the saved trust becomes unusable.
    private async Task<ISyncService> OfferUnknownKeyThenCorruptTrustAsync()
    {
        f.Ready();
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        var sync = f.Create();
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        Assert.True(sync.Setup.State.HostKey!.CanAccept);
        f.WriteDeviceFile("known-hosts.json", "not json");
        return sync;
    }

    private void AssertOfferedButNotAcceptable(ISyncService sync)
    {
        var offered = sync.Setup.State.HostKey;
        Assert.NotNull(offered);
        Assert.Equal(CustomHost, offered.Host);
        Assert.Equal(22, offered.Port);
        Assert.Equal("ssh-ed25519", offered.Algorithm);
        Assert.Equal("SHA256:first", offered.Fingerprint);
        Assert.False(offered.CanAccept);
    }

    private static string TrustedFile(params (string Host, int Port, string Fingerprint)[] entries)
    {
        var hosts = entries.Select(e => $$"""{"host":"{{e.Host}}","port":{{e.Port}},"fingerprint":"{{e.Fingerprint}}"}""");
        return $$"""{"version":1,"hosts":[{{string.Join(",", hosts)}}]}""";
    }
}
