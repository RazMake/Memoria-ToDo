using System.Globalization;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The words the Sync settings show for the setup and the sync status.</summary>
[Unit]
public class SyncTextTests
{
    [Fact]
    public void Repository_lines_show_the_address_folder_and_device_name()
    {
        var lines = SyncText.Repository(new FakeSyncSetupState
        {
            RepositoryAddress = "git@example.com:tasks.git", RepositoryFolder = @"C:\Tasks", DeviceName = "Desk",
        });

        Assert.Equal(new RepositoryLines("git@example.com:tasks.git", @"C:\Tasks", "Desk"), lines);
    }

    [Fact]
    public void Repository_lines_show_None_for_a_missing_address_and_folder()
    {
        var lines = SyncText.Repository(new FakeSyncSetupState { DeviceName = "Desk" });

        Assert.Equal(new RepositoryLines("None", "None", "Desk"), lines);
    }

    [Fact]
    public void A_status_with_no_success_yet_says_not_synced()
    {
        var text = SyncText.Status(new FakeSyncStatus());

        Assert.Equal("Not synced yet.", text);
    }

    [Fact]
    public void A_status_shows_the_last_success_in_local_time()
    {
        var at = new DateTimeOffset(2038, 2, 1, 12, 0, 0, TimeSpan.Zero);

        var text = SyncText.Status(new FakeSyncStatus { LastSuccessfulSync = at });

        Assert.Equal($"Last successful sync: {at.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}", text);
    }

    [Fact]
    public void A_status_says_when_changes_wait_to_be_synced()
    {
        var text = SyncText.Status(new FakeSyncStatus { HasPendingChanges = true });

        Assert.Equal($"Not synced yet.{Environment.NewLine}Changes wait to be synced.", text);
    }

    [Fact]
    public void A_status_lists_each_skipped_file_with_its_reason()
    {
        var text = SyncText.Status(new FakeSyncStatus
        {
            SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable."), new FakeSkippedFile("tasks/b.json", "Newer format.")],
        });

        Assert.Equal(
            string.Join(Environment.NewLine, "Not synced yet.", "Skipped tasks/a.json: Unreadable.", "Skipped tasks/b.json: Newer format."),
            text);
    }

    [Theory]
    [InlineData(SyncState.Error)]
    [InlineData(SyncState.Offline)]
    public void A_failing_status_shows_its_details(SyncState state)
    {
        var text = SyncText.Status(new FakeSyncStatus { State = state, Details = "Host unreachable" });

        Assert.Equal($"Not synced yet.{Environment.NewLine}Host unreachable", text);
    }

    [Theory]
    [InlineData(SyncState.Error)]
    [InlineData(SyncState.Offline)]
    public void A_failing_status_without_details_shows_no_extra_line(SyncState state)
    {
        var text = SyncText.Status(new FakeSyncStatus { State = state, Details = null });

        Assert.Equal("Not synced yet.", text);
    }

    [Theory]
    [InlineData(SyncState.Idle)]
    [InlineData(SyncState.Working)]
    public void Details_of_a_status_that_is_not_failing_are_not_shown(SyncState state)
    {
        var text = SyncText.Status(new FakeSyncStatus { State = state, Details = "Old news" });

        Assert.Equal("Not synced yet.", text);
    }

    [Theory]
    [InlineData(SyncState.Error, true)]
    [InlineData(SyncState.Offline, true)]
    [InlineData(SyncState.Idle, false)]
    [InlineData(SyncState.Working, false)]
    public void Only_the_Error_and_Offline_states_count_as_a_failure(SyncState state, bool failure)
    {
        Assert.Equal(failure, SyncText.IsFailure(new FakeSyncStatus { State = state }));
    }

    [Fact]
    public void The_last_sync_line_says_not_synced_yet_when_there_was_no_success()
    {
        Assert.Equal("Not synced yet.", SyncText.LastSync(new FakeSyncStatus()));
    }

    [Fact]
    public void The_last_sync_line_shows_the_last_success_in_local_time()
    {
        var at = new DateTimeOffset(2038, 2, 1, 12, 0, 0, TimeSpan.Zero);

        var text = SyncText.LastSync(new FakeSyncStatus { LastSuccessfulSync = at });

        Assert.Equal($"Last successful sync: {at.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}", text);
    }

    [Fact]
    public void The_last_sync_line_is_only_that_line_whatever_else_the_status_says()
    {
        var text = SyncText.LastSync(new FakeSyncStatus
        {
            HasPendingChanges = true, State = SyncState.Error, Details = "Host unreachable",
            SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable.")],
        });

        Assert.Equal("Not synced yet.", text);
    }

    [Fact]
    public void A_status_with_nothing_more_to_say_has_no_details()
    {
        Assert.Null(SyncText.StatusDetails(new FakeSyncStatus { LastSuccessfulSync = DateTimeOffset.UnixEpoch }));
    }

    [Fact]
    public void The_details_list_pending_changes_the_failure_and_each_skipped_file_one_per_line()
    {
        var text = SyncText.StatusDetails(new FakeSyncStatus
        {
            HasPendingChanges = true, State = SyncState.Offline, Details = "Host unreachable",
            SkippedFiles = [new FakeSkippedFile("tasks/a.json", "Unreadable."), new FakeSkippedFile("tasks/b.json", "Newer format.")],
        });

        Assert.Equal(
            string.Join(Environment.NewLine,
                "Changes wait to be synced.", "Host unreachable",
                "Skipped tasks/a.json: Unreadable.", "Skipped tasks/b.json: Newer format."),
            text);
    }

    [Fact]
    public void Details_of_a_status_that_is_not_failing_are_left_out_of_the_details()
    {
        Assert.Null(SyncText.StatusDetails(new FakeSyncStatus { State = SyncState.Idle, Details = "Old news" }));
    }

    [Fact]
    public void A_failing_status_without_details_adds_no_detail_line()
    {
        Assert.Null(SyncText.StatusDetails(new FakeSyncStatus { State = SyncState.Error, Details = null }));
    }
}
