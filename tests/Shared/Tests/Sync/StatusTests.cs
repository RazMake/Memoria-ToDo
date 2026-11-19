using System.Security.Cryptography;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>What Sync reports about the last pass: state, failure kind, receipt, and pending changes.</summary>
[Unit]
public sealed class StatusTests
{
    private readonly SyncFixture f = new();

    /// <summary>Waits for a pass to end; a pass that throws instead of returning a status fails the test.</summary>
    private static Task<ISyncStatus> ReturnedAsync(Task<ISyncStatus> running) =>
        running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Starts_idle_with_no_sync_failure_or_skipped_files()
    {
        await using var sync = f.Create();

        var status = sync.Status;

        Assert.Equal(SyncState.Idle, status.State);
        Assert.Null(status.Failure);
        Assert.Null(status.LastSuccessfulSync);
        Assert.Empty(status.SkippedFiles);
    }

    [Fact]
    public async Task Reports_idle_with_the_clock_time_and_pending_changes_after_a_pass()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(true);
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Idle, sync.Status.State);
        Assert.Equal(f.Time.GetUtcNow(), sync.Status.LastSuccessfulSync);
        Assert.True(sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Keeps_a_status_read_before_a_pass_unchanged_after_the_pass()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(true);
        await using var sync = f.Create();
        var before = sync.Status;

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Idle, before.State);
        Assert.Null(before.LastSuccessfulSync);
        Assert.False(before.HasPendingChanges);
    }

    [Fact]
    public async Task Records_the_sync_time_and_key_fingerprint_so_the_device_counts_as_configured()
    {
        f.Ready();
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        var receipt = f.Git.ReadReceipt(f.CloneFolder)!;
        Assert.Equal(f.Time.GetUtcNow(), receipt.LastSuccessfulSync);
        Assert.Equal("SHA256:" + Convert.ToBase64String(SHA256.HashData(f.Keys.PublicKeyBlob)).TrimEnd('='), receipt.KeyFingerprint);
        Assert.True(sync.Setup.State.IsConfigured);
    }

    [Fact]
    public async Task Shows_working_while_a_pass_runs()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();

        var running = sync.SyncNowAsync(CancellationToken.None);
        try
        {
            await held.HasStarted;

            Assert.Equal(SyncState.Working, sync.Status.State);
        }
        finally
        {
            held.Complete();
        }

        await running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Raises_status_changed_when_a_pass_finishes()
    {
        f.Ready();
        await using var sync = f.Create();
        var raised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.LastSuccessfulSync is not null)
                raised.TrySetResult();
        };

        await sync.SyncNowAsync(CancellationToken.None);

        await raised.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(SyncFailure.Connection, SyncState.Offline)]
    [InlineData(SyncFailure.Authentication, SyncState.Error)]
    [InlineData(SyncFailure.UnknownHostKey, SyncState.Error)]
    [InlineData(SyncFailure.ChangedHostKey, SyncState.Error)]
    [InlineData(SyncFailure.Repository, SyncState.Error)]
    [InlineData(SyncFailure.PushRejected, SyncState.Error)]
    [InlineData(SyncFailure.InvalidFile, SyncState.Error)]
    [InlineData(SyncFailure.LocalStorage, SyncState.Error)]
    public async Task Reports_each_git_failure_kind_with_its_state(SyncFailure kind, SyncState state)
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        try
        {
            await Task.WhenAny(held.HasStarted, running);
            Assert.False(
                running.IsCompleted,
                $"The pass ended before Git started: {sync.Status.State}, {sync.Status.Failure}, {sync.Status.Details}");
            await held.HasStarted;

            held.Fail(new GitFailureException(kind, "It failed."));
            var returned = await ReturnedAsync(running);

            Assert.Equal(state, returned.State);
            Assert.Equal(kind, returned.Failure);
            Assert.Equal(state, sync.Status.State);
            Assert.Equal(kind, sync.Status.Failure);
            Assert.False(string.IsNullOrWhiteSpace(sync.Status.Details));
        }
        finally
        {
            held.Complete();
        }
    }

    [Fact]
    public async Task Reports_an_unexpected_failure_for_an_error_that_is_not_a_git_failure()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Fail(new NotSupportedException("Surprise."));
        var returned = await ReturnedAsync(running);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.Unexpected, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Reports_a_local_storage_failure_for_saved_data_on_this_device_that_cannot_be_used()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Fail(new InvalidDataException("The saved data is corrupt."));
        var returned = await ReturnedAsync(running);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal("The saved data is corrupt.", returned.Details);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
    }

    [Fact]
    public async Task Shows_an_error_with_details_and_retries_after_the_usual_backoff_when_a_pass_fails_unexpectedly()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        sync.Start();
        await held.HasStarted;
        var next = f.Git.ScriptPass();

        held.Fail(new NotSupportedException("Surprise."));
        await f.Time.WaitForActiveTimerAsync().WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        Assert.Equal("Surprise.", sync.Status.Details);
        f.Time.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(1, f.Git.PassesStarted);
        f.Time.Advance(TimeSpan.FromSeconds(1));
        await next.HasStarted.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        next.Complete();
    }

    [Fact]
    public async Task Keeps_the_last_success_time_when_a_later_pass_fails()
    {
        f.Ready();
        await using var sync = f.Create();
        await sync.SyncNowAsync(CancellationToken.None);
        var synced = sync.Status.LastSuccessfulSync;
        var held = f.Git.ScriptPass();
        f.Time.Advance(TimeSpan.FromMinutes(1));
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;

        held.Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
        var returned = await ReturnedAsync(running);

        Assert.Equal(synced, returned.LastSuccessfulSync);
        Assert.Equal(synced, sync.Status.LastSuccessfulSync);
    }

    private async Task<ISyncService> SyncedWithPendingAsync(bool pending)
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(pending);
        var sync = f.Create();
        await sync.SyncNowAsync(CancellationToken.None);
        Assert.Equal(pending, sync.Status.HasPendingChanges);
        return sync;
    }

    private async Task<ISyncStatus> FailNextPassAsync(ISyncService sync, Exception failure, Action? beforeFailing = null)
    {
        var held = f.Git.ScriptPass();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        beforeFailing?.Invoke();
        held.Fail(failure);
        return await ReturnedAsync(running);
    }

    [Fact]
    public async Task Shows_the_pending_changes_a_pass_found_when_it_then_fails_to_connect()
    {
        await using var sync = await SyncedWithPendingAsync(false);

        await FailNextPassAsync(sync, new PassFailedException(new GitFailureException(SyncFailure.Connection, "Offline."), hasPendingChanges: true));

        Assert.Equal(SyncState.Offline, sync.Status.State);
        Assert.Equal(SyncFailure.Connection, sync.Status.Failure);
        Assert.True(sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Shows_no_pending_changes_when_a_failed_pass_found_none_even_if_the_previous_status_had_some()
    {
        await using var sync = await SyncedWithPendingAsync(true);

        await FailNextPassAsync(sync, new PassFailedException(new GitFailureException(SyncFailure.Connection, "Offline."), hasPendingChanges: false));

        Assert.Equal(SyncFailure.Connection, sync.Status.Failure);
        Assert.False(sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Reports_the_failure_a_failed_pass_wrapped_not_the_wrapper()
    {
        await using var sync = await SyncedWithPendingAsync(false);

        await FailNextPassAsync(sync, new PassFailedException(new GitFailureException(SyncFailure.Authentication, "The host refused the key."), hasPendingChanges: true));

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Authentication, sync.Status.Failure);
        Assert.Equal("The host refused the key.", sync.Status.Details);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Keeps_the_pending_changes_shown_before_when_a_pass_fails_a_local_check_without_knowing_them(bool before)
    {
        await using var sync = await SyncedWithPendingAsync(before);

        await FailNextPassAsync(sync, new GitFailureException(SyncFailure.Repository, "detached HEAD"));

        Assert.Equal(SyncFailure.Repository, sync.Status.Failure);
        Assert.Equal(before, sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Shows_pending_changes_and_a_local_storage_error_when_the_receipt_cannot_be_written_after_a_pass_that_found_changes()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.NextWriteReceiptFailure = new IOException("The disk is full.");

        held.Complete(hasPendingChanges: true);
        var returned = await ReturnedAsync(running);

        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal("The disk is full.", returned.Details);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.True(sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Shows_no_pending_changes_and_a_local_storage_error_when_the_receipt_cannot_be_written_after_a_pass_that_found_none_following_one_that_found_some()
    {
        await using var sync = await SyncedWithPendingAsync(true);
        var held = f.Git.ScriptPass();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.NextWriteReceiptFailure = new IOException("The disk is full.");

        held.Complete(hasPendingChanges: false);
        var returned = await ReturnedAsync(running);

        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal("The disk is full.", returned.Details);
        Assert.False(sync.Status.HasPendingChanges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shows_the_pending_changes_a_finished_pass_found_when_closing_the_key_afterwards_fails(bool found)
    {
        await using var sync = await SyncedWithPendingAsync(!found);
        f.Git.DefaultResult = new GitPassResult(found);
        f.Keys.OnHandleDisposed = () => throw new IOException("The key handle could not be closed.");

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal("The key handle could not be closed.", returned.Details);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal(found, sync.Status.HasPendingChanges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shows_the_pending_changes_a_failed_pass_found_when_closing_the_key_afterwards_also_fails(bool found)
    {
        await using var sync = await SyncedWithPendingAsync(!found);

        var returned = await FailNextPassAsync(
            sync,
            new PassFailedException(new GitFailureException(SyncFailure.Connection, "Offline."), hasPendingChanges: found),
            () => f.Keys.OnHandleDisposed = () => throw new IOException("The key handle could not be closed."));

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal("The key handle could not be closed.", returned.Details);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal(found, sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Keeps_the_pending_changes_of_a_failed_pass_when_the_setup_refresh_after_it_also_fails()
    {
        await using var sync = await SyncedWithPendingAsync(false);

        var returned = await FailNextPassAsync(
            sync,
            new PassFailedException(new GitFailureException(SyncFailure.Connection, "Offline."), hasPendingChanges: true),
            () => f.Git.OriginReadFailure = new IOException("Unreadable clone."));

        Assert.Equal(SyncFailure.Connection, sync.Status.Failure);
        Assert.StartsWith("Offline.", returned.Details);
        Assert.Contains("Unreadable clone.", returned.Details);
        Assert.True(sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Shows_no_pending_changes_after_a_successful_pass_that_follows_a_failed_pass_with_pending_changes()
    {
        await using var sync = await SyncedWithPendingAsync(false);
        await FailNextPassAsync(sync, new PassFailedException(new GitFailureException(SyncFailure.Connection, "Offline."), hasPendingChanges: true));
        f.Git.DefaultResult = new GitPassResult(false);

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Null(sync.Status.Failure);
        Assert.False(sync.Status.HasPendingChanges);
    }

    [Fact]
    public async Task Reports_skipped_files_as_an_invalid_file_error()
    {
        f.Ready();
        f.Tasks.SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable.")];
        await using var sync = f.Create();

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.InvalidFile, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
    }

    [Fact]
    public async Task Counts_a_pass_that_skipped_unreadable_files_as_a_successful_sync_while_still_reporting_them()
    {
        f.Ready();
        f.Tasks.SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable.")];
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncFailure.InvalidFile, sync.Status.Failure);
        Assert.Equal(f.Time.GetUtcNow(), sync.Status.LastSuccessfulSync);
        Assert.False(sync.Status.HasPendingChanges);
        Assert.True(sync.Setup.State.IsConfigured);
    }

    [Fact]
    public async Task Reports_a_file_the_git_pass_could_not_merge_even_when_the_tasks_reload_can_read_the_local_copy()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(false) { Skipped = [new FakeSkippedFile("tasks/a.json", "Both sides changed it unreadably.")] };
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.InvalidFile, sync.Status.Failure);
        var skipped = Assert.Single(sync.Status.SkippedFiles);
        Assert.Equal("tasks/a.json", skipped.RelativePath);
        Assert.Equal("Both sides changed it unreadably.", skipped.Reason);
        Assert.Equal(f.Time.GetUtcNow(), sync.Status.LastSuccessfulSync);
    }

    [Fact]
    public async Task Reports_a_file_once_when_both_the_git_pass_and_the_tasks_reload_skipped_it()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(false) { Skipped = [new FakeSkippedFile("tasks/a.json", "Both sides changed it unreadably.")] };
        f.Tasks.SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable.")];
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncFailure.InvalidFile, sync.Status.Failure);
        var skipped = Assert.Single(sync.Status.SkippedFiles);
        Assert.Equal("tasks/a.json", skipped.RelativePath);
        Assert.Equal("Both sides changed it unreadably.", skipped.Reason);
        Assert.Equal(f.Time.GetUtcNow(), sync.Status.LastSuccessfulSync);
    }

    [Fact]
    public async Task Keeps_the_newer_version_hint_of_the_tasks_reload_when_the_git_reason_wins_for_the_same_file()
    {
        f.Ready();
        f.Git.DefaultResult = new GitPassResult(false)
        {
            Skipped = [new FakeSkippedFile("tasks/a.json", "Both sides changed it unreadably."), new FakeSkippedFile("tasks/git-only.json", "Git only.")],
        };
        f.Tasks.SkippedFiles =
        [
            new FakeSkippedFile("tasks/a.json", "Written by a newer version.", RequiresUpdate: true),
            new FakeSkippedFile("tasks/reload-only.json", "Unreadable."),
        ];
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        var byPath = sync.Status.SkippedFiles.ToDictionary(file => file.RelativePath);
        Assert.Equal(3, sync.Status.SkippedFiles.Count);
        Assert.Equal("Both sides changed it unreadably.", byPath["tasks/a.json"].Reason);
        Assert.True(byPath["tasks/a.json"].RequiresUpdate);
        Assert.Equal("Git only.", byPath["tasks/git-only.json"].Reason);
        Assert.False(byPath["tasks/git-only.json"].RequiresUpdate);
        Assert.Equal("Unreadable.", byPath["tasks/reload-only.json"].Reason);
        Assert.False(byPath["tasks/reload-only.json"].RequiresUpdate);
    }

    [Fact]
    public async Task Lists_the_skipped_contact_files_after_the_skipped_task_files()
    {
        var contactsPart = new FakeRepositoryPart("contacts");
        f.Repository.Add(contactsPart);
        f.Ready();
        f.Tasks.SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable task.")];
        contactsPart.SkippedFiles = [new FakeSkippedFile("autocomplete/contacts/team.json", "Unreadable contacts.")];
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncFailure.InvalidFile, sync.Status.Failure);
        Assert.Equal(["tasks/a.json", "autocomplete/contacts/team.json"], sync.Status.SkippedFiles.Select(file => file.RelativePath));
    }

    [Fact]
    public async Task Reports_a_contact_file_once_with_the_merges_reason_when_both_the_git_pass_and_the_contacts_reload_skipped_it()
    {
        var contactsPart = new FakeRepositoryPart("contacts");
        f.Repository.Add(contactsPart);
        f.Ready();
        const string path = "autocomplete/contacts/team.json";
        f.Git.DefaultResult = new GitPassResult(false) { Skipped = [new FakeSkippedFile(path, "Both sides changed it unreadably.")] };
        contactsPart.SkippedFiles = [new FakeSkippedFile(path, "Unreadable.")];
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncFailure.InvalidFile, sync.Status.Failure);
        var skipped = Assert.Single(sync.Status.SkippedFiles);
        Assert.Equal(path, skipped.RelativePath);
        Assert.Equal("Both sides changed it unreadably.", skipped.Reason);
    }

    [Fact]
    public async Task Lists_the_skipped_task_files_before_a_contact_file_the_merge_skipped_and_the_contacts_reload_skipped_again()
    {
        var contactsPart = new FakeRepositoryPart("contacts");
        f.Repository.Add(contactsPart);
        f.Ready();
        const string contact = "autocomplete/contacts/team.json";
        f.Git.DefaultResult = new GitPassResult(false) { Skipped = [new FakeSkippedFile(contact, "Both sides changed it unreadably.")] };
        f.Tasks.SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable task.")];
        contactsPart.SkippedFiles = [new FakeSkippedFile(contact, "Unreadable.")];
        await using var sync = f.Create();

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncFailure.InvalidFile, sync.Status.Failure);
        Assert.Equal(["tasks/a.json", contact], sync.Status.SkippedFiles.Select(file => file.RelativePath));
        Assert.Equal("Both sides changed it unreadably.", sync.Status.SkippedFiles[1].Reason);
    }

    [Fact]
    public async Task Never_shows_a_clean_finished_status_while_a_pass_that_skipped_files_is_being_reported()
    {
        f.Ready();
        f.Tasks.SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable.")];
        await using var sync = f.Create();
        var seen = new System.Collections.Concurrent.ConcurrentQueue<ISyncStatus>();
        sync.StatusChanged += (_, _) => seen.Enqueue(sync.Status);

        await sync.SyncNowAsync(CancellationToken.None);

        Assert.NotEmpty(seen);
        Assert.Equal(SyncFailure.InvalidFile, seen.Last().Failure);
        Assert.DoesNotContain(
            seen,
            status => status.State == SyncState.Idle && status.Failure is null && status.LastSuccessfulSync is not null);
    }

    [Fact]
    public async Task Reports_a_local_storage_status_and_never_a_clean_finished_one_when_the_setup_refresh_after_a_successful_pass_fails()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var seen = new System.Collections.Concurrent.ConcurrentQueue<ISyncStatus>();
        sync.StatusChanged += (_, _) => seen.Enqueue(sync.Status);
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.OriginReadFailure = new IOException("Unreadable clone.");

        held.Complete();
        var status = await ReturnedAsync(running);

        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.LocalStorage, status.Failure);
        Assert.Equal("Unreadable clone.", status.Details);
        Assert.Equal(f.Time.GetUtcNow(), status.LastSuccessfulSync);
        Assert.NotNull(f.Git.ReadReceipt(f.CloneFolder));
        Assert.NotEmpty(seen);
        Assert.Equal(SyncFailure.LocalStorage, seen.Last().Failure);
        Assert.DoesNotContain(seen, s => s.State == SyncState.Idle && s.Failure is null && s.LastSuccessfulSync is not null);
    }

    [Fact]
    public async Task Keeps_the_pass_failure_and_appends_the_refresh_details_when_the_setup_refresh_after_it_also_fails()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.OriginReadFailure = new IOException("Unreadable clone.");

        held.Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
        var status = await ReturnedAsync(running);

        Assert.Equal(SyncFailure.Connection, status.Failure);
        Assert.StartsWith("Offline.", status.Details);
        Assert.Contains("Unreadable clone.", status.Details);
    }

    [Fact]
    public async Task Reports_a_denied_real_location_check_as_a_storage_error_for_a_manual_sync_without_touching_git_or_the_key()
    {
        await using var sync = await SyncedWithPendingAsync(true);
        var connectionsBefore = f.Git.ConnectionsOpened;
        var opensBefore = f.Keys.OpenCalls;
        f.Files.FailPhysicalChecks(new UnauthorizedAccessException("Access is denied."));

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.True(sync.Status.HasPendingChanges);
        Assert.Equal(connectionsBefore, f.Git.ConnectionsOpened);
        Assert.Equal(opensBefore, f.Keys.OpenCalls);
    }

    [Fact]
    public async Task Reports_a_disposed_file_system_during_the_real_location_check_as_an_unexpected_error_for_a_manual_sync()
    {
        f.Ready();
        await using var sync = f.Create();
        f.Files.FailPhysicalChecks(new ObjectDisposedException("filesystem"));

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.Unexpected, returned.Failure);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Publishes_an_unexpected_error_and_retries_after_the_usual_backoff_when_the_real_location_check_hits_a_disposed_file_system_in_a_scheduled_pass()
    {
        f.Ready();
        f.Files.FailPhysicalChecks(new ObjectDisposedException("filesystem"));
        await using var sync = f.Create();
        sync.Start();
        await f.Time.WaitForActiveTimerAsync().WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        var checks = f.Files.PhysicalChecks;

        f.Time.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(checks, f.Files.PhysicalChecks);
        f.Time.Advance(TimeSpan.FromSeconds(1));
        await f.Time.WaitForActiveTimerAsync().WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(checks + 1, f.Files.PhysicalChecks);
    }

    [Fact]
    public async Task Reports_a_key_location_that_cannot_be_read_as_a_storage_error_for_a_manual_sync()
    {
        await using var sync = await SyncedWithPendingAsync(true);
        var connectionsBefore = f.Git.ConnectionsOpened;
        f.Keys.HasKeyFailure = new UnauthorizedAccessException("Access is denied.");

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.True(sync.Status.HasPendingChanges);
        Assert.Equal(connectionsBefore, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Reports_a_key_that_cannot_be_opened_as_a_storage_error_for_a_manual_sync()
    {
        await using var sync = await SyncedWithPendingAsync(true);
        var connectionsBefore = f.Git.ConnectionsOpened;
        f.Keys.OpenFailure = new UnauthorizedAccessException("Access is denied.");

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.True(sync.Status.HasPendingChanges);
        Assert.Equal(connectionsBefore, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Reports_a_dependency_disposed_while_reading_the_key_location_as_an_unexpected_error_for_a_manual_sync()
    {
        f.Ready();
        await using var sync = f.Create();
        f.Keys.HasKeyFailure = new ObjectDisposedException("The key store.");

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.Unexpected, returned.Failure);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Retries_after_the_usual_backoff_when_reading_the_key_location_fails_unexpectedly_in_a_scheduled_pass()
    {
        f.Ready();
        f.Keys.HasKeyFailure = new ObjectDisposedException("The key store.");
        await using var sync = f.Create();
        sync.Start();
        await f.Time.WaitForActiveTimerAsync().WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        f.Keys.HasKeyFailure = null;

        f.Time.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(0, f.Git.PassesStarted);
        f.Time.Advance(TimeSpan.FromSeconds(1));

        await f.Git.WaitForPassesStartedAsync(1).WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Reports_saved_key_data_that_cannot_be_used_as_a_storage_error_for_a_manual_sync()
    {
        f.Ready();
        await using var sync = f.Create();
        f.Keys.HasKeyFailure = new InvalidDataException("The saved key is corrupt.");

        var returned = await sync.SyncNowAsync(CancellationToken.None);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal("The saved key is corrupt.", returned.Details);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Reports_a_denied_real_location_check_as_a_storage_error_for_a_scheduled_pass()
    {
        f.Ready();
        f.Files.FailPhysicalChecks(new UnauthorizedAccessException("Access is denied."));
        await using var sync = f.Create();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                reported.TrySetResult();
        };

        sync.Start();
        await reported.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Reports_a_disposed_dependency_in_a_pass_as_an_unexpected_failure_not_as_a_stopped_service()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        held.Fail(new ObjectDisposedException("Dependency"));

        var returned = await ReturnedAsync(running);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.Unexpected, returned.Failure);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Reports_a_disposed_dependency_at_startup_as_an_error_and_still_publishes_the_setup_state()
    {
        f.Ready();
        f.Git.OriginReadFailure = new ObjectDisposedException("Dependency");
        await using var sync = f.Create();
        var firstNotice = new TaskCompletionSource<(SyncState State, SyncFailure? Failure)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) => firstNotice.TrySetResult((sync.Status.State, sync.Status.Failure));

        sync.Start();
        var (state, failure) = await firstNotice.Task.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, state);
        Assert.Equal(SyncFailure.Unexpected, failure);
    }

    [Fact]
    public async Task Keeps_the_pass_failure_when_the_setup_refresh_after_it_fails_because_a_dependency_was_disposed()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        await held.HasStarted;
        f.Git.OriginReadFailure = new ObjectDisposedException("Dependency");

        held.Fail(new GitFailureException(SyncFailure.Connection, "Offline."));
        var returned = await ReturnedAsync(running);

        Assert.Equal(SyncFailure.Connection, returned.Failure);
        Assert.StartsWith("Offline.", returned.Details);
        Assert.Contains("Dependency", returned.Details);
        Assert.Equal(SyncFailure.Connection, sync.Status.Failure);
    }

    [Fact]
    public async Task Retries_after_a_successful_pass_whose_setup_refresh_failed()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        sync.Start();
        try
        {
            await held.HasStarted;
            f.Git.OriginReadFailure = new IOException("Unreadable clone.");

            held.Complete();
            await f.Time.WaitForActiveTimerAsync();
            f.Time.Advance(TimeSpan.FromSeconds(30));

            await f.Git.WaitForPassesStartedAsync(2);
        }
        finally
        {
            held.Complete();
        }
    }

    [Fact]
    public async Task Reports_a_repository_error_when_the_selected_clone_contains_the_device_folder()
    {
        f.Keys.SeedKey();
        f.Tasks.RepositoryFolder = Path.GetDirectoryName(f.DeviceFolder)!;
        await using var sync = f.Create();
        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                reported.TrySetResult();
        };

        sync.Start();
        await reported.Task.        WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(SyncFailure.Repository, sync.Status.Failure);
        Assert.False(sync.Setup.State.IsConfigured);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Does_not_start_a_pass_when_the_selected_clone_contains_the_device_folder_and_reports_a_repository_error()
    {
        f.Keys.SeedKey();
        f.Tasks.RepositoryFolder = Path.GetDirectoryName(f.DeviceFolder)!;
        await using var sync = f.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.SyncNowAsync(CancellationToken.None));

        Assert.Equal(SyncFailure.Repository, sync.Status.Failure);
        Assert.Equal(0, f.Git.ConnectionsOpened);
        Assert.Equal(0, f.Keys.OpenCalls);
    }

    [Fact]
    public async Task Does_not_start_a_pass_without_a_selected_repository()
    {
        f.WithKey();
        await using var sync = f.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.SyncNowAsync(CancellationToken.None));

        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Does_not_start_a_pass_without_a_device_key()
    {
        f.Tasks.RepositoryFolder = f.CloneFolder;
        await using var sync = f.Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.SyncNowAsync(CancellationToken.None));

        Assert.Equal(0, f.Git.ConnectionsOpened);
    }
}
