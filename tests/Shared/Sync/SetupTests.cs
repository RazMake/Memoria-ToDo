using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>Device setup: addresses, the device name and key, and choosing the clone.</summary>
[Unit]
public sealed class SetupTests
{
    private readonly SyncFixture f = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://git.example.org/me/tasks.git")]
    [InlineData("ssh://git@git.example.org")]
    [InlineData("ssh://git@git.example.org:0/me/tasks.git")]
    [InlineData("ssh://git@git.example.org:70000/me/tasks.git")]
    [InlineData("git@git.example.org")]
    public async Task Rejects_an_unsupported_address_before_any_side_effect(string address)
    {
        f.WithKey();
        await using var sync = f.Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(address, f.CloneFolder, CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.UseExistingAsync(address, f.CloneFolder, CancellationToken.None));

        AssertNothingChanged();
    }

    [Theory]
    [InlineData("C:\\tasks")]
    [InlineData("C:/tasks")]
    [InlineData("d:\\work\\tasks.git")]
    public async Task Rejects_a_windows_drive_path_given_as_an_address_before_any_side_effect(string address)
    {
        f.WithKey();
        await using var sync = f.Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(address, f.CloneFolder, CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.UseExistingAsync(address, f.CloneFolder, CancellationToken.None));

        AssertNothingChanged();
    }

    // A bare one-letter host is written with its SSH user, so Git never reads `g:me/tasks.git` as a local path.
    [Theory]
    [InlineData("g:me/tasks.git", "git@g:me/tasks.git", "git", "g", "me/tasks.git")]
    [InlineData("G:me/tasks.git", "git@G:me/tasks.git", "git", "g", "me/tasks.git")]
    [InlineData("g:Me/Tasks.GIT", "git@g:Me/Tasks.GIT", "git", "g", "Me/Tasks.GIT")]
    [InlineData("  g:me/tasks.git  ", "git@g:me/tasks.git", "git", "g", "me/tasks.git")]
    [InlineData("git@g:me/tasks.git", "git@g:me/tasks.git", "git", "g", "me/tasks.git")]
    [InlineData("me@g:me/tasks.git", "me@g:me/tasks.git", "me", "g", "me/tasks.git")]
    [InlineData("gh:me/tasks.git", "gh:me/tasks.git", "git", "gh", "me/tasks.git")]
    [InlineData("git.example.org:me/tasks.git", "git.example.org:me/tasks.git", "git", "git.example.org", "me/tasks.git")]
    [InlineData("ssh://g/me/tasks.git", "ssh://g/me/tasks.git", "git", "g", "/me/tasks.git")]
    public async Task Gives_a_bare_single_letter_scp_host_its_ssh_user_and_leaves_other_addresses_as_written(string address, string text, string user, string host, string path)
    {
        f.WithKey();
        await using var sync = f.Create();

        await sync.Setup.CloneAsync(address, f.CloneFolder, CancellationToken.None);
        await sync.Setup.UseExistingAsync(address, f.CloneFolder, CancellationToken.None);

        foreach (var used in new[] { Assert.Single(f.Git.Cloned), Assert.Single(f.Git.UsedExisting) })
        {
            Assert.Equal(text, used.Text);
            Assert.Equal(user, AddressParser.User(used.Text));
            Assert.Equal(host, used.Host);
            Assert.Equal(22, used.Port);
            Assert.Equal(path, used.Path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative-folder")]
    public async Task Rejects_an_unusable_folder_before_any_side_effect(string folder)
    {
        f.WithKey();
        await using var sync = f.Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, folder, CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, folder, CancellationToken.None));

        AssertNothingChanged();
    }

    [Fact]
    public async Task Rejects_a_clone_that_is_or_contains_the_device_folder_before_any_side_effect()
    {
        f.WithKey();
        await using var sync = f.Create();

        foreach (var folder in new[] { f.DeviceFolder, Path.GetDirectoryName(f.DeviceFolder)! })
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, folder, CancellationToken.None));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, folder, CancellationToken.None));
        }

        AssertNothingChanged();
    }

    [Fact]
    public async Task Accepts_a_clone_inside_the_device_folder()
    {
        f.WithKey();
        await using var sync = f.Create();
        var inside = Path.Combine(f.DeviceFolder, "clone");

        await sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, inside, CancellationToken.None);

        Assert.Equal(inside, f.Tasks.RepositoryFolder);
    }

    [Fact]
    public async Task Rejects_a_clone_folder_that_is_a_link_to_the_device_folder_or_its_parent_before_any_side_effect()
    {
        f.WithKey();
        var toDevice = Path.GetFullPath("link-to-device");
        var toParent = Path.GetFullPath("link-to-parent");
        f.Files.AddAlias(toDevice, f.DeviceFolder);
        f.Files.AddAlias(toParent, Path.GetDirectoryName(f.DeviceFolder)!);
        await using var sync = f.Create();

        foreach (var folder in new[] { toDevice, toParent })
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, folder, CancellationToken.None));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, folder, CancellationToken.None));
        }

        AssertNothingChanged();
    }

    [Fact]
    public async Task Rejects_a_clone_folder_that_contains_the_device_folder_through_a_link_to_the_device_folder()
    {
        f.WithKey();
        var clone = Path.GetFullPath("physical-clone");
        var deviceLink = Path.GetFullPath("device-link");
        f.Files.AddAlias(deviceLink, Path.Combine(clone, "inner"));
        await using var sync = SyncService.Create(f.Tasks, f.Files, f.Keys, f.Selection, f.Time, deviceLink, SyncFixture.DefaultDeviceName, null, f.Git);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, clone, CancellationToken.None));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, clone, CancellationToken.None));

        AssertNothingChanged();
    }

    [Fact]
    public async Task Accepts_a_clone_folder_that_is_a_link_to_a_folder_outside_the_device_folder()
    {
        f.WithKey();
        var link = Path.GetFullPath("link-to-elsewhere");
        f.Files.AddAlias(link, Path.GetFullPath("elsewhere"));
        await using var sync = f.Create();

        await sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, link, CancellationToken.None);

        Assert.Equal(link, f.Tasks.RepositoryFolder);
    }

    [Fact]
    public async Task Refuses_a_clone_folder_when_its_real_location_cannot_be_checked_before_any_side_effect()
    {
        f.WithKey();
        f.Files.FailPhysicalChecks();
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));
        await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        AssertNothingChanged();
    }

    [Fact]
    public async Task Does_not_check_real_locations_when_the_service_is_created()
    {
        f.Ready();

        await using var sync = f.Create();

        Assert.Equal(0, f.Files.PhysicalChecks);
    }

    [Fact]
    public async Task Does_not_sync_a_selected_clone_that_reaches_the_device_folder_through_a_link()
    {
        f.Keys.SeedKey();
        var link = Path.GetFullPath("link-to-parent");
        f.Files.AddAlias(link, Path.GetDirectoryName(f.DeviceFolder)!);
        f.Tasks.RepositoryFolder = link;
        f.Git.SeedClone(link, f.Address);
        await using var sync = f.Create();

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => sync.SyncNowAsync(CancellationToken.None));

        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Refuses_a_saved_multi_line_device_name_and_leaves_the_file_unchanged()
    {
        f.WriteDeviceFile("device-name.txt", "two\nlines");
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.GenerateKeyAsync(CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.IsType<InvalidDataException>(io.InnerException);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal("two\nlines", f.ReadDeviceFile("device-name.txt"));
        Assert.Equal(0, f.Keys.GenerateCalls);
    }

    [Fact]
    public async Task Clones_from_an_scp_style_address_on_the_default_port()
    {
        f.WithKey();
        await using var sync = f.Create();

        await sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);

        var cloned = Assert.Single(f.Git.Cloned);
        Assert.Equal("git.example.org", cloned.Host);
        Assert.Equal(22, cloned.Port);
    }

    [Fact]
    public async Task Clones_from_an_ssh_url_with_a_custom_port()
    {
        f.WithKey();
        await using var sync = f.Create();

        await sync.Setup.CloneAsync("ssh://git@git.example.org:2222/me/tasks.git", f.CloneFolder, CancellationToken.None);

        var cloned = Assert.Single(f.Git.Cloned);
        Assert.Equal("git.example.org", cloned.Host);
        Assert.Equal(2222, cloned.Port);
    }

    [Fact]
    public async Task Shows_the_address_of_the_selected_clone_after_starting_and_before_the_first_sync()
    {
        f.Ready();
        await using var sync = f.Create();
        Assert.Equal(0, f.Git.OriginReads);
        var firstNotice = new TaskCompletionSource<(string? Address, int Passes)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) => firstNotice.TrySetResult((sync.Setup.State.RepositoryAddress, f.Git.PassesStarted));
        var pass = f.Git.ScriptPass();

        try
        {
            sync.Start();
            var (address, passesStarted) = await firstNotice.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.Equal(SyncFixture.ScpAddress, address);
            Assert.Equal(0, passesStarted);
        }
        finally
        {
            pass.Complete();
        }
    }

    [Fact]
    public async Task Reports_a_failed_startup_read_as_an_error_and_still_publishes_the_setup_state()
    {
        f.Ready();
        f.Git.OriginReadFailure = new IOException("Clone unreadable.");
        await using var sync = f.Create();
        var firstNotice = new TaskCompletionSource<(SyncState State, string? Details, int Passes)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) => firstNotice.TrySetResult((sync.Status.State, sync.Status.Details, f.Git.PassesStarted));

        sync.Start();
        var (state, details, passes) = await firstNotice.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, state);
        Assert.False(string.IsNullOrWhiteSpace(details));
        Assert.Equal(0, passes);
    }

    [Fact]
    public async Task Selects_the_clone_only_after_cloning_succeeds()
    {
        f.WithKey();
        await using var sync = f.Create();
        var changed = 0;
        sync.Setup.Changed += (_, _) => Interlocked.Increment(ref changed);

        await sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);

        Assert.Equal([f.CloneFolder], f.Selection.Selected);
        Assert.Equal(f.CloneFolder, sync.Setup.State.RepositoryFolder);
        Assert.Equal(SyncFixture.ScpAddress, sync.Setup.State.RepositoryAddress);
        Assert.True(changed > 0);
    }

    [Fact]
    public async Task Keeps_the_previous_selection_when_cloning_fails()
    {
        f.WithKey();
        f.Tasks.RepositoryFolder = Path.GetFullPath("previous-clone");
        var cause = new GitFailureException(SyncFailure.Connection, "No route to host.");
        f.Git.NextCloneFailure = cause;
        await using var sync = f.Create();

        var failure = await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        Assert.Same(cause, failure.InnerException);
        Assert.Empty(f.Selection.Selected);
        Assert.Equal(Path.GetFullPath("previous-clone"), sync.Setup.State.RepositoryFolder);
    }

    [Fact]
    public async Task Refuses_to_clone_without_a_device_key_before_any_git_work()
    {
        await using var sync = f.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        AssertNothingChanged();
    }

    [Fact]
    public async Task Selects_an_existing_clone_after_validating_it_and_does_not_count_it_as_synced()
    {
        f.WithKey();
        await using var sync = f.Create();

        await sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None);

        Assert.Single(f.Git.UsedExisting);
        Assert.Equal([f.CloneFolder], f.Selection.Selected);
        Assert.False(sync.Setup.State.IsConfigured);
    }

    [Fact]
    public async Task Keeps_the_previous_selection_when_an_existing_clone_is_refused()
    {
        f.WithKey();
        var cause = new GitFailureException(SyncFailure.Repository, "Not a clone.");
        f.Git.NextUseExistingFailure = cause;
        await using var sync = f.Create();

        var failure = await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        Assert.Same(cause, failure.InnerException);
        Assert.Empty(f.Selection.Selected);
    }

    [Fact]
    public async Task Reports_a_refused_existing_clone_as_the_sync_status_and_keeps_the_selected_clone()
    {
        f.Ready();
        f.Git.NextUseExistingFailure = new GitFailureException(SyncFailure.Repository, "Not a clone.");
        await using var sync = f.Create();
        var other = Path.GetFullPath("other-clone");

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, other, CancellationToken.None));

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Repository, sync.Status.Failure);
        Assert.Equal(f.CloneFolder, sync.Setup.State.RepositoryFolder);
    }

    [Fact]
    public async Task Uses_the_app_supplied_device_name_without_writing_it()
    {
        await using var sync = f.Create();

        Assert.Equal(SyncFixture.DefaultDeviceName, sync.Setup.State.DeviceName);
        Assert.Empty(f.Files.AllFilesUnder(f.DeviceFolder));
    }

    [Fact]
    public async Task Saves_a_changed_device_name_without_making_a_new_key()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        await sync.Setup.SetDeviceNameAsync("Desk", CancellationToken.None);

        Assert.Equal("Desk", f.ReadDeviceFile("device-name.txt").Trim());
        Assert.Equal("Desk", sync.Setup.State.DeviceName);
        Assert.EndsWith(" Desk", sync.Setup.State.PublicKey);
        Assert.Equal(1, f.Keys.GenerateCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two\nlines")]
    public async Task Rejects_a_blank_or_multi_line_device_name_before_writing(string name)
    {
        await using var sync = f.Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => sync.Setup.SetDeviceNameAsync(name, CancellationToken.None));

        Assert.Empty(f.Files.AllFilesUnder(f.DeviceFolder));
        Assert.Equal(SyncFixture.DefaultDeviceName, sync.Setup.State.DeviceName);
    }

    [Fact]
    public async Task Does_not_make_a_key_when_the_saved_device_name_cannot_be_read()
    {
        f.WriteDeviceFile("device-name.txt", "Desk");
        f.Files.FailReadsUnder(f.DeviceFolder);
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.GenerateKeyAsync(CancellationToken.None));

        Assert.Equal(0, f.Keys.GenerateCalls);
        Assert.False(f.Keys.HasKey);
    }

    [Fact]
    public async Task Shows_the_public_key_with_the_device_name_and_releases_the_handle()
    {
        await using var sync = f.Create();

        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        Assert.Equal($"ssh-rsa {Convert.ToBase64String(f.Keys.PublicKeyBlob)} {SyncFixture.DefaultDeviceName}", sync.Setup.State.PublicKey);
        Assert.Equal(0, f.Keys.OpenHandles);
    }

    [Fact]
    public async Task Never_replaces_an_existing_key()
    {
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        await sync.Setup.GenerateKeyAsync(CancellationToken.None);

        Assert.Equal(1, f.Keys.GenerateCalls);
    }

    [Fact]
    public async Task Reports_a_failed_device_name_read_as_a_storage_error_and_keeps_the_io_cause()
    {
        f.WriteDeviceFile("device-name.txt", "Desk");
        f.Files.FailReadsUnder(f.DeviceFolder);
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.GenerateKeyAsync(CancellationToken.None));

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
    }

    [Fact]
    public async Task Reports_a_failed_device_name_write_as_a_storage_error_and_keeps_the_name()
    {
        f.Ready();
        f.Files.FailWritesUnder(f.DeviceFolder);
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.SetDeviceNameAsync("Desk", CancellationToken.None));

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal(SyncFixture.DefaultDeviceName, sync.Setup.State.DeviceName);
    }

    [Fact]
    public async Task Reports_a_stored_key_that_cannot_be_opened_as_a_storage_error()
    {
        f.Ready();
        f.Keys.FailOpen = true;
        await using var sync = f.Create();

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.SetDeviceNameAsync("Desk", CancellationToken.None));

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal("Desk", f.ReadDeviceFile("device-name.txt").Trim());
        Assert.Equal("Desk", sync.Setup.State.DeviceName);
    }

    [Fact]
    public async Task Keeps_showing_the_saved_device_name_when_the_stored_key_cannot_be_opened_at_startup()
    {
        f.Ready();
        f.WriteDeviceFile("device-name.txt", "Desk");
        f.Keys.FailOpen = true;
        await using var sync = f.Create();
        var firstFailure = new TaskCompletionSource<(SyncFailure? Failure, string DeviceName)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                firstFailure.TrySetResult((sync.Status.Failure, sync.Setup.State.DeviceName));
        };

        sync.Start();
        var (failure, deviceName) = await firstFailure.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(SyncFailure.LocalStorage, failure);
        Assert.Equal("Desk", deviceName);
        Assert.Equal("Desk", f.ReadDeviceFile("device-name.txt"));
    }

    [Fact]
    public async Task Shows_the_recovered_device_name_when_a_later_manual_pass_cannot_open_the_key()
    {
        f.Ready();
        f.WriteDeviceFile("device-name.txt", "Desk");
        f.Files.FailReadsUnder(f.DeviceFolder);
        f.Keys.FailOpen = true;
        await using var sync = f.Create();
        var firstReturned = await sync.SyncNowAsync(CancellationToken.None);
        Assert.Equal(SyncState.Error, firstReturned.State);
        f.Files.StopFailing();
        var notices = new System.Collections.Concurrent.ConcurrentQueue<(SyncState State, SyncFailure? Failure, string DeviceName)>();
        sync.Setup.Changed += (_, _) => notices.Enqueue((sync.Status.State, sync.Status.Failure, sync.Setup.State.DeviceName));

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal("Desk", sync.Setup.State.DeviceName);
        var last = notices.ToArray().LastOrDefault();
        Assert.Equal((SyncState.Error, (SyncFailure?)SyncFailure.LocalStorage, "Desk"), last);
    }

    [Fact]
    public async Task Keeps_the_public_key_comment_in_step_with_the_saved_device_name_when_the_origin_cannot_be_read_afterwards()
    {
        f.Ready();
        await using var sync = f.Create();
        await sync.Setup.GenerateKeyAsync(CancellationToken.None);
        var keyBefore = Convert.ToBase64String(f.Keys.PublicKeyBlob);
        f.Git.OriginReadFailure = new IOException("The origin can't be read.");

        await Record.ExceptionAsync(() => sync.Setup.SetDeviceNameAsync("Desk", CancellationToken.None));

        Assert.Equal("Desk", f.ReadDeviceFile("device-name.txt").Trim());
        Assert.Equal("Desk", sync.Setup.State.DeviceName);
        Assert.Equal($"ssh-rsa {keyBefore} Desk", sync.Setup.State.PublicKey);
    }

    [Fact]
    public async Task Reports_a_denied_real_location_check_as_a_storage_error_with_the_denial_as_the_cause_before_any_side_effect()
    {
        f.WithKey();
        var denied = new UnauthorizedAccessException("Access is denied.");
        f.Files.FailPhysicalChecks(denied);
        await using var sync = f.Create();

        var cloning = await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));
        var existing = await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        Assert.Same(denied, cloning.InnerException);
        Assert.Same(denied, existing.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        AssertNothingChanged();
    }

    [Fact]
    public async Task Reports_a_failed_selection_as_a_storage_error_and_keeps_the_previous_selection()
    {
        f.Ready();
        f.Selection.NextSelectFailure = new IOException("Settings can't be saved.");
        await using var sync = f.Create();
        var other = Path.GetFullPath("other-clone");

        await Assert.ThrowsAsync<IOException>(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, other, CancellationToken.None));

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal(f.CloneFolder, sync.Setup.State.RepositoryFolder);
    }

    [Fact]
    public async Task Keeps_the_clone_failure_when_refreshing_the_setup_after_it_also_fails()
    {
        f.WithKey();
        f.Git.NextCloneFailure = new GitFailureException(SyncFailure.Connection, "Offline.");
        f.Git.OriginReadFailure = new IOException("Unreadable clone.");
        await using var sync = f.Create();

        var failure = await Assert.ThrowsAsync<IOException>(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        Assert.IsType<GitFailureException>(failure.InnerException);
        Assert.Equal(SyncFailure.Connection, sync.Status.Failure);
    }

    private void AssertNothingChanged()
    {
        Assert.Empty(f.Git.Cloned);
        Assert.Empty(f.Git.UsedExisting);
        Assert.Empty(f.Selection.Selected);
        Assert.Empty(f.Files.AllFilesUnder(f.DeviceFolder));
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }
}
