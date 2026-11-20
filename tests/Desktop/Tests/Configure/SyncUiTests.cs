using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// What one open window's sync controls share: the one "changed" notice on the UI thread, running the window's own
/// operations, and what happens when the window goes away. The "UI thread" is a queue the test runs.
/// </summary>
[Unit]
public class SyncUiTests : IDisposable
{
    private readonly FakeSyncService _sync = new();
    private readonly Queue<Action> _posted = new();
    private readonly SyncUi _ui;
    private int _changed;

    public SyncUiTests()
    {
        _ui = new SyncUi(_sync, new FakeSetupActions(), _posted.Enqueue, Serilog.Core.Logger.None);
        _ui.Changed += () => _changed++;
    }

    public void Dispose() => _ui.Dispose();

    private void RunPosted()
    {
        while (_posted.Count > 0)
        {
            _posted.Dequeue()();
        }
    }

    [Fact]
    public void A_setup_change_raises_Changed_on_the_posted_thread()
    {
        _sync.SetupControl.Publish(new FakeSyncSetupState { DeviceName = "Other" });
        Assert.Equal(0, _changed);

        RunPosted();

        Assert.Equal(1, _changed);
    }

    [Fact]
    public void A_status_change_raises_Changed_on_the_posted_thread()
    {
        _sync.Publish(new FakeSyncStatus());
        Assert.Equal(0, _changed);

        RunPosted();

        Assert.Equal(1, _changed);
    }

    [Fact]
    public void Exposes_the_sync_service_and_setup_actions_it_was_given()
    {
        var actions = new FakeSetupActions();
        using var ui = new SyncUi(_sync, actions, _ => { }, Serilog.Core.Logger.None);

        Assert.Same(_sync, ui.Sync);
        Assert.Same(actions, ui.Actions);
    }

    [Fact]
    public async Task A_finished_operation_raises_Changed()
    {
        await _ui.RunAsync(_ => Task.CompletedTask, _ => { });

        Assert.Equal(1, _changed);
    }

    [Fact]
    public async Task A_failed_operation_shows_its_message_and_raises_Changed()
    {
        string? shown = null;

        await _ui.RunAsync(_ => throw new IOException("Disk full."), text => shown = text);

        Assert.Equal("Disk full.", shown);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public async Task A_failure_after_the_window_was_disposed_shows_nothing_and_raises_nothing()
    {
        var release = new TaskCompletionSource();
        string? shown = null;
        var running = _ui.RunAsync(async _ =>
        {
            await release.Task;
            throw new IOException("Too late.");
        }, text => shown = text);

        _ui.Dispose();
        release.SetResult();
        await running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Null(shown);
        Assert.Equal(0, _changed);
    }

    [Fact]
    public async Task Disposing_cancels_the_waiting_operation_without_showing_an_error()
    {
        string? shown = null;
        var running = _ui.RunAsync(token => Task.Delay(Timeout.Infinite, token), text => shown = text);

        _ui.Dispose();
        await running.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Null(shown);
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void Disposing_detaches_the_notices()
    {
        _ui.Dispose();

        _sync.SetupControl.Publish(new FakeSyncSetupState());
        _sync.Publish(new FakeSyncStatus());
        RunPosted();

        Assert.Equal(0, _changed);
    }

    [Fact]
    public void A_notice_posted_before_disposal_does_nothing_after_it()
    {
        _sync.Publish(new FakeSyncStatus());

        _ui.Dispose();
        RunPosted();

        Assert.Equal(0, _changed);
    }

    [Fact]
    public void Disposing_twice_is_harmless()
    {
        _ui.Dispose();

        var failure = Record.Exception(_ui.Dispose);

        Assert.Null(failure);
    }

    [Fact]
    public async Task Sync_now_runs_one_pass_and_reports_nothing_when_it_succeeds()
    {
        string? shown = null;

        await _ui.SyncNowAsync(text => shown = text, CancellationToken.None);

        Assert.Equal(1, _sync.SyncCount);
        Assert.Null(shown);
    }

    [Theory]
    [InlineData(SyncState.Error)]
    [InlineData(SyncState.Offline)]
    public async Task Sync_now_reports_the_details_of_a_failed_pass(SyncState state)
    {
        _sync.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus { State = state, Details = "Read-only key" });
        string? shown = null;

        await _ui.SyncNowAsync(text => shown = text, CancellationToken.None);

        Assert.Equal("Read-only key", shown);
    }
}
