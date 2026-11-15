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
    public async Task Keeps_the_trust_of_other_hosts_and_other_ports_when_a_host_and_port_is_forgotten()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:first"), (CustomHost, 2222, "SHA256:first"), ("other.example.org", 22, "SHA256:first")));
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:first");
        await using var sync = f.Create();

        await sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None);

        await AssertTrustedAsync(sync, OtherPortAddress);
        await AssertTrustedAsync(sync, OtherHostAddress);
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));
        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
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

        await sync.Setup.ForgetHostKeyAsync(CustomHost.ToUpperInvariant() + ".", 22, CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Unknown, f.Git.Verdicts[^1]);
    }

    [Fact]
    public async Task Refuses_a_changed_custom_host_key_and_does_not_let_it_be_accepted()
    {
        f.WithKey();
        var saved = TrustedFile((CustomHost, 22, "SHA256:first"));
        f.WriteDeviceFile("known-hosts.json", saved);
        f.Git.OfferedHostKey = ("ssh-ed25519", "SHA256:second");
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(CustomAddress, f.CloneFolder, CancellationToken.None));

        Assert.Equal(HostKeyVerdict.Changed, f.Git.Verdicts[^1]);
        Assert.Equal(SyncFailure.ChangedHostKey, sync.Status.Failure);
        Assert.False(sync.Setup.State.HostKey!.CanAccept);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.AcceptHostKeyAsync("SHA256:second", CancellationToken.None));
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Refuses_a_changed_key_when_a_configured_service_syncs_and_leaves_the_saved_trust_unchanged()
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
        Assert.False(sync.Setup.State.HostKey!.CanAccept);
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Accepts_a_replacement_key_only_after_the_old_one_is_forgotten()
    {
        f.WithKey();
        f.WriteDeviceFile("known-hosts.json", TrustedFile((CustomHost, 22, "SHA256:first")));
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

    [Fact]
    public async Task Refuses_to_forget_a_built_in_host_even_when_it_is_named_with_different_case_and_a_trailing_dot()
    {
        f.WithKey();
        await using var sync = f.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.ForgetHostKeyAsync("GitHub.com.", 22, CancellationToken.None));

        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
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
    public async Task Keeps_the_offered_key_visible_but_not_acceptable_when_forgetting_a_host_fails_because_the_saved_trust_became_unusable()
    {
        await using var sync = await OfferUnknownKeyThenCorruptTrustAsync();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None));

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

    [Fact]
    public async Task Forgets_nothing_and_creates_no_trust_file_when_a_custom_host_has_no_saved_trust()
    {
        f.WithKey();
        await using var sync = f.Create();

        await sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None);
        await sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None);

        Assert.False(f.Files.FileExists(f.DeviceFile("known-hosts.json")));
    }

    [Fact]
    public async Task Leaves_the_saved_trust_file_unchanged_when_forgetting_a_host_and_port_it_does_not_hold()
    {
        f.WithKey();
        var saved = TrustedFile(("other.example.org", 22, "SHA256:other"), (CustomHost, 2222, "SHA256:first"));
        f.WriteDeviceFile("known-hosts.json", saved);
        await using var sync = f.Create();
        f.Files.FailWritesUnder(f.DeviceFolder);

        await sync.Setup.ForgetHostKeyAsync(CustomHost, 22, CancellationToken.None);

        f.Files.StopFailing();
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
    }

    [Fact]
    public async Task Rejects_forgetting_a_missing_host_before_returning_a_task_and_without_reporting_an_error()
    {
        f.WithKey();
        await using var sync = f.Create();

        Assert.Throws<ArgumentNullException>(() => { _ = sync.Setup.ForgetHostKeyAsync(null!, 22, CancellationToken.None); });

        Assert.Null(sync.Status.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_forgetting_a_blank_host_before_returning_a_task_and_without_reporting_an_error(string host)
    {
        f.WithKey();
        await using var sync = f.Create();

        Assert.Throws<ArgumentException>(() => { _ = sync.Setup.ForgetHostKeyAsync(host, 22, CancellationToken.None); });

        Assert.Null(sync.Status.Failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task Rejects_forgetting_a_port_outside_1_to_65535_before_returning_a_task_without_reporting_an_error_or_touching_the_saved_trust(int port)
    {
        f.WithKey();
        var saved = TrustedFile((CustomHost, 22, "SHA256:first"));
        f.WriteDeviceFile("known-hosts.json", saved);
        await using var sync = f.Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = sync.Setup.ForgetHostKeyAsync(CustomHost, port, CancellationToken.None); });

        Assert.Null(sync.Status.Failure);
        Assert.Equal(saved, f.ReadDeviceFile("known-hosts.json"));
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
