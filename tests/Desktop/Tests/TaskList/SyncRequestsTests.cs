using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The window's requests for sync passes: it asks the shared service for a pass and waits for it, shows that one is
/// running, and keeps why a pass failed. It never stops the shared pass; closing the window cancels only its own wait.
/// </summary>
[Unit]
public class SyncRequestsTests
{
    private readonly FakeSyncService _service = new();
    private readonly SyncRequests _requests;
    private int _changed;

    public SyncRequestsTests()
    {
        _requests = new SyncRequests(_service, post => post(), () => _changed++);
    }

    private static TaskCompletionSource<ISyncStatus> Pass() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static FakeSyncStatus Failed(SyncState state = SyncState.Error, string? details = "Requested sync failed") =>
        new() { State = state, Failure = SyncFailure.Connection, Details = details };

    [Fact]
    public void Starts_with_no_pass_running_and_nothing_failed()
    {
        Assert.False(_requests.IsWorking);
        Assert.Equal("", _requests.FailureText);
        Assert.Equal(1, _service.StatusSubscriberCount);
    }

    [Fact]
    public async Task Requests_a_pass_and_is_working_until_it_ends()
    {
        var pass = Pass();
        _service.SyncOperation = _ => pass.Task;

        var waiting = _requests.SyncAsync();

        Assert.True(_requests.IsWorking);
        Assert.Equal(1, _service.SyncCount);
        var changedWhileWorking = _changed;
        Assert.True(changedWhileWorking > 0);

        pass.SetResult(new FakeSyncStatus());
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.False(_requests.IsWorking);
        Assert.Equal("", _requests.FailureText);
        Assert.True(_changed > changedWhileWorking);
    }

    [Fact]
    public async Task Requests_each_later_pass_when_the_one_before_has_ended()
    {
        await _requests.SyncAsync();
        await _requests.SyncAsync();

        Assert.Equal(2, _service.SyncCount);
    }

    [Fact]
    public async Task Does_not_request_another_pass_while_one_it_requested_is_still_running()
    {
        var pass = Pass();
        _service.SyncOperation = _ => pass.Task;
        var waiting = _requests.SyncAsync();

        await _requests.SyncAsync().WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(1, _service.SyncCount);
        pass.SetResult(new FakeSyncStatus());
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Is_working_while_the_shared_service_is_working_on_a_pass_it_did_not_request()
    {
        _service.Publish(new FakeSyncStatus { State = SyncState.Working });

        Assert.True(_requests.IsWorking);

        _service.Publish(new FakeSyncStatus { State = SyncState.Idle });

        Assert.False(_requests.IsWorking);
    }

    [Fact]
    public async Task Requests_no_pass_while_the_shared_service_is_working_on_one_it_did_not_request_and_requests_again_when_it_ends()
    {
        _service.Publish(new FakeSyncStatus { State = SyncState.Working });

        await _requests.SyncAsync();

        Assert.Equal(0, _service.SyncCount);

        _service.Publish(new FakeSyncStatus { State = SyncState.Idle });
        await _requests.SyncAsync();

        Assert.Equal(1, _service.SyncCount);
    }

    [Fact]
    public void Tells_the_window_to_change_when_the_shared_status_changes()
    {
        _service.Publish(new FakeSyncStatus { State = SyncState.Working });

        Assert.True(_changed > 0);
    }

    [Theory]
    [InlineData(SyncState.Error)]
    [InlineData(SyncState.Offline)]
    public async Task A_failed_pass_keeps_its_details_as_the_failure_text(SyncState state)
    {
        _service.SyncOperation = _ => Task.FromResult<ISyncStatus>(Failed(state));

        await _requests.SyncAsync();

        Assert.Equal("Requested sync failed", _requests.FailureText);
    }

    [Fact]
    public async Task A_failed_pass_without_details_still_says_it_failed()
    {
        _service.SyncOperation = _ => Task.FromResult<ISyncStatus>(Failed(details: null));

        await _requests.SyncAsync();

        Assert.StartsWith("Sync failed: ", _requests.FailureText);
    }

    [Fact]
    public async Task A_pass_that_succeeds_has_no_failure_text()
    {
        _service.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus { State = SyncState.Idle });

        await _requests.SyncAsync();

        Assert.Equal("", _requests.FailureText);
    }

    [Fact]
    public async Task The_next_request_clears_an_earlier_failure_text_while_it_runs()
    {
        _service.SyncOperation = _ => Task.FromResult<ISyncStatus>(Failed());
        await _requests.SyncAsync();
        var pass = Pass();
        _service.SyncOperation = _ => pass.Task;

        var waiting = _requests.SyncAsync();

        Assert.Equal("", _requests.FailureText);
        pass.SetResult(new FakeSyncStatus());
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_unexpected_exception_from_the_service_becomes_the_failure_text_and_requests_still_work()
    {
        _service.SyncOperation = _ => Task.FromException<ISyncStatus>(new IOException("Unexpected backend failure"));

        await _requests.SyncAsync();

        Assert.Equal("Unexpected backend failure", _requests.FailureText);
        Assert.False(_requests.IsWorking);

        _service.SyncOperation = null;
        await _requests.SyncAsync();

        Assert.Equal(2, _service.SyncCount);
    }

    [Fact]
    public async Task A_request_during_app_shutdown_finishes_quietly()
    {
        _service.SyncOperation = _ => Task.FromException<ISyncStatus>(new ObjectDisposedException("Sync service"));

        await _requests.SyncAsync();

        Assert.Equal("", _requests.FailureText);
        Assert.False(_requests.IsWorking);
    }

    [Fact]
    public async Task Disposing_cancels_its_own_wait_but_not_the_shared_pass_and_shows_no_failure()
    {
        var pass = Pass();
        var caller = CancellationToken.None;
        _service.SyncOperation = token =>
        {
            caller = token;
            return pass.Task;
        };
        var waiting = _requests.SyncAsync();

        _requests.Dispose();
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.True(caller.IsCancellationRequested);
        Assert.False(pass.Task.IsCompleted);
        Assert.Equal("", _requests.FailureText);
        pass.SetResult(new FakeSyncStatus());
    }

    [Fact]
    public async Task Requests_nothing_after_it_is_disposed()
    {
        _requests.Dispose();

        await _requests.SyncAsync();

        Assert.Equal(0, _service.SyncCount);
    }

    [Fact]
    public void Stops_listening_to_the_service_when_disposed_and_tells_the_window_nothing_more()
    {
        _requests.Dispose();
        var changed = _changed;

        _service.Publish(new FakeSyncStatus { State = SyncState.Working });

        Assert.Equal(0, _service.StatusSubscriberCount);
        Assert.Equal(changed, _changed);
    }

    [Fact]
    public async Task A_pass_that_ends_after_the_window_closed_tells_the_window_nothing_more()
    {
        var pass = Pass();
        _service.SyncOperation = _ => pass.Task;
        var waiting = _requests.SyncAsync();
        var changed = _changed;
        _requests.Dispose();

        pass.SetResult(Failed());
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Equal(changed, _changed);
        Assert.Equal("", _requests.FailureText);
    }

    [Fact]
    public void Tells_the_window_through_the_poster_it_was_given_not_on_the_thread_the_service_raised_on()
    {
        _requests.Dispose();
        var posted = new List<Action>();
        var requests = new SyncRequests(_service, posted.Add, () => _changed++);
        var before = _changed;

        _service.Publish(new FakeSyncStatus { State = SyncState.Working });

        Assert.Equal(before, _changed);
        posted.Single()();
        Assert.True(_changed > before);
        requests.Dispose();
    }
}
