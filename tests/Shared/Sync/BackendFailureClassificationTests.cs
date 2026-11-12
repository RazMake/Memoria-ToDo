using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// How Sync treats a failure a backend throws without anyone cancelling or stopping: it is an operational failure to
/// report, never a silent stop. Real cancellation and stopping are covered by the lifecycle and setup tests.
/// </summary>
[Unit]
public sealed class BackendFailureClassificationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly SyncFixture f = new();

    [Fact]
    public async Task Returns_an_unexpected_error_status_when_a_backend_throws_a_cancellation_nobody_requested_during_a_manual_pass()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        try
        {
            await held.HasStarted;
            held.Fail(new OperationCanceledException("The backend gave up."));

            var status = await running.WaitAsync(Bound, TestContext.Current.CancellationToken);

            Assert.Equal(SyncState.Error, status.State);
            Assert.Equal(SyncFailure.Unexpected, status.Failure);
            Assert.Equal(SyncState.Error, sync.Status.State);
        }
        finally
        {
            held.Complete();
        }
    }

    [Fact]
    public async Task Retries_after_a_scheduled_pass_whose_backend_threw_a_cancellation_nobody_requested()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        try
        {
            sync.Start();
            await held.HasStarted;

            held.Fail(new OperationCanceledException("The backend gave up."));
            await f.Time.WaitForActiveTimerAsync().WaitAsync(Bound, TestContext.Current.CancellationToken);
            f.Time.Advance(TimeSpan.FromSeconds(30));

            await f.Git.WaitForPassesStartedAsync(2);
        }
        finally
        {
            held.Complete();
        }
    }

    [Fact]
    public async Task Runs_a_follow_up_pass_for_an_edit_saved_during_a_pass_whose_backend_threw_a_cancellation_nobody_requested()
    {
        f.Ready();
        var held = f.Git.ScriptPass();
        await using var sync = f.Create();
        var running = sync.SyncNowAsync(CancellationToken.None);
        try
        {
            await held.HasStarted;
            f.Tasks.RaiseLocalSaved();

            held.Fail(new OperationCanceledException("The backend gave up."));
            await running.WaitAsync(Bound, TestContext.Current.CancellationToken);
            await f.Time.WaitForActiveTimerAsync().WaitAsync(Bound, TestContext.Current.CancellationToken);
            f.Time.Advance(TimeSpan.FromSeconds(5));

            await f.Git.WaitForPassesStartedAsync(2);
        }
        finally
        {
            held.Complete();
        }
    }

    [Fact]
    public async Task Publishes_an_unexpected_error_when_a_backend_throws_a_cancellation_nobody_requested_while_starting()
    {
        f.Ready();
        f.Git.OriginReadFailure = new OperationCanceledException("The backend gave up.");
        await using var sync = f.Create();
        var firstFailure = new TaskCompletionSource<(SyncState State, SyncFailure? Failure)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                firstFailure.TrySetResult((sync.Status.State, sync.Status.Failure));
        };

        sync.Start();
        var (state, failure) = await firstFailure.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, state);
        Assert.Equal(SyncFailure.Unexpected, failure);
    }

    [Fact]
    public async Task Reports_a_local_storage_error_when_reading_the_clone_fails_with_an_io_error_while_starting()
    {
        await AssertStartupFailureIsLocalStorage(new IOException("Unreadable clone."));
    }

    [Fact]
    public async Task Reports_a_local_storage_error_when_reading_the_clone_is_denied_while_starting()
    {
        await AssertStartupFailureIsLocalStorage(new UnauthorizedAccessException("Access is denied."));
    }

    [Fact]
    public async Task Publishes_the_error_and_throws_an_io_error_wrapping_a_cancellation_nobody_requested_during_setup()
    {
        f.WithKey();
        var cause = new OperationCanceledException("The backend gave up.");
        f.Git.NextUseExistingFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Publishes_the_error_and_throws_an_io_error_wrapping_a_cancellation_nobody_requested_during_a_clone()
    {
        f.WithKey();
        var cause = new OperationCanceledException("The backend gave up.");
        f.Git.NextCloneFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Publishes_the_error_and_throws_an_io_error_wrapping_a_disposed_dependency_during_setup_when_nothing_is_stopping()
    {
        f.WithKey();
        var cause = new ObjectDisposedException("Dependency");
        f.Git.NextUseExistingFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Publishes_a_local_storage_error_and_throws_an_io_error_wrapping_a_cryptographic_failure_opening_the_key_for_a_clone()
    {
        f.WithKey();
        var cause = new System.Security.Cryptography.CryptographicException("The key is unusable.");
        f.Keys.OpenFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
    }

    [Fact]
    public async Task Publishes_the_error_and_throws_an_io_error_wrapping_an_invalid_operation_thrown_by_the_backend_during_a_clone()
    {
        f.WithKey();
        var cause = new InvalidOperationException("The backend is in a bad state.");
        f.Git.NextCloneFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Publishes_the_error_and_throws_an_io_error_wrapping_an_argument_error_thrown_by_the_backend_while_using_an_existing_clone()
    {
        f.WithKey();
        var cause = new ArgumentException("The backend rejected the clone.");
        f.Git.NextUseExistingFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Returns_a_local_storage_error_for_a_manual_sync_when_the_saved_repository_folder_is_malformed()
    {
        f.Ready();
        f.Tasks.RepositoryFolder = "relative-folder";
        await using var sync = f.Create();

        var returned = await sync.SyncNowAsync(CancellationToken.None).WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, returned.State);
        Assert.Equal(SyncFailure.LocalStorage, returned.Failure);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal("relative-folder", f.Tasks.RepositoryFolder);
        Assert.Equal(0, f.Git.PassesStarted);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Fact]
    public async Task Publishes_a_local_storage_error_for_a_scheduled_pass_when_the_saved_repository_folder_is_malformed()
    {
        f.Ready();
        f.Tasks.RepositoryFolder = "relative-folder";
        await using var sync = f.Create();
        var reported = new TaskCompletionSource<SyncFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.StatusChanged += (_, _) =>
        {
            if (sync.Status.Failure is { } kind)
                reported.TrySetResult(kind);
        };

        sync.Start();
        var failure = await reported.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(SyncFailure.LocalStorage, failure);
        Assert.Equal("relative-folder", f.Tasks.RepositoryFolder);
        Assert.Equal(0, f.Git.PassesStarted);
        Assert.Equal(0, f.Git.ConnectionsOpened);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), false)]
    [InlineData(typeof(ArgumentException), false)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(ArgumentException), true)]
    public async Task Publishes_an_unexpected_error_and_throws_an_io_error_wrapping_a_failure_of_the_repository_selection(Type type, bool clone)
    {
        f.WithKey();
        var cause = (Exception)Activator.CreateInstance(type, "selection backend failed")!;
        f.Selection.NextSelectFailure = cause;
        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => clone
            ? sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None)
            : sync.Setup.UseExistingAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
        Assert.Empty(f.Selection.Selected);
    }

    public enum KeyStep { GenerateKeyReadsHasKey, CloneReadsHasKey, CloneOpensKey, GenerateKeyGenerates }

    [Theory]
    [InlineData(KeyStep.GenerateKeyReadsHasKey, typeof(InvalidOperationException))]
    [InlineData(KeyStep.GenerateKeyReadsHasKey, typeof(ArgumentException))]
    [InlineData(KeyStep.CloneReadsHasKey, typeof(InvalidOperationException))]
    [InlineData(KeyStep.CloneReadsHasKey, typeof(ArgumentException))]
    [InlineData(KeyStep.CloneOpensKey, typeof(InvalidOperationException))]
    [InlineData(KeyStep.CloneOpensKey, typeof(ArgumentException))]
    [InlineData(KeyStep.GenerateKeyGenerates, typeof(InvalidOperationException))]
    [InlineData(KeyStep.GenerateKeyGenerates, typeof(ArgumentException))]
    public async Task Publishes_an_unexpected_error_and_throws_an_io_error_wrapping_a_failure_of_the_key_store(KeyStep step, Type type)
    {
        var cause = (Exception)Activator.CreateInstance(type, "key store failed")!;
        switch (step)
        {
            case KeyStep.GenerateKeyReadsHasKey:
            case KeyStep.CloneReadsHasKey:
                f.Keys.HasKeyFailure = cause;
                break;
            case KeyStep.CloneOpensKey:
                f.WithKey();
                f.Keys.OpenFailure = cause;
                break;
            case KeyStep.GenerateKeyGenerates:
                f.Keys.GenerateFailure = cause;
                break;
        }

        await using var sync = f.Create();

        var failure = await Record.ExceptionAsync(() => step is KeyStep.GenerateKeyReadsHasKey or KeyStep.GenerateKeyGenerates
            ? sync.Setup.GenerateKeyAsync(CancellationToken.None)
            : sync.Setup.CloneAsync(SyncFixture.ScpAddress, f.CloneFolder, CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.Same(cause, io.InnerException);
        Assert.Equal(SyncState.Error, sync.Status.State);
        Assert.Equal(SyncFailure.Unexpected, sync.Status.Failure);
    }

    [Fact]
    public async Task Shows_no_device_name_instead_of_the_default_when_the_saved_name_is_corrupt_at_startup()
    {
        f.WriteDeviceFile("device-name.txt", "two\nlines");
        await using var sync = f.Create();
        var firstFailure = new TaskCompletionSource<(SyncFailure? Failure, string DeviceName)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                firstFailure.TrySetResult((sync.Status.Failure, sync.Setup.State.DeviceName));
        };

        sync.Start();
        var (failure, deviceName) = await firstFailure.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.NotNull(failure);
        Assert.Equal("", deviceName);
        Assert.Equal("two\nlines", f.ReadDeviceFile("device-name.txt"));
        Assert.Equal(SyncFailure.LocalStorage, failure);
    }

    [Fact]
    public async Task Shows_no_device_name_and_a_local_storage_error_when_the_saved_name_cannot_be_probed_or_read_at_startup()
    {
        f.WriteDeviceFile("device-name.txt", "Desk");
        var files = new UnprobeableFileSystem(f.Files, f.DeviceFile("device-name.txt"));
        await using var sync = SyncService.Create(f.Tasks, files, f.Keys, f.Selection, f.Time, f.DeviceFolder, SyncFixture.DefaultDeviceName, null, f.Git);
        var firstFailure = new TaskCompletionSource<(SyncFailure? Failure, string DeviceName)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) =>
        {
            if (sync.Status.Failure is not null)
                firstFailure.TrySetResult((sync.Status.Failure, sync.Setup.State.DeviceName));
        };

        sync.Start();
        var (failure, deviceName) = await firstFailure.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal("", deviceName);
        Assert.Equal(SyncFailure.LocalStorage, failure);
        Assert.Equal("Desk", f.ReadDeviceFile("device-name.txt"));
        Assert.Equal(0, files.Writes);
    }

    [Fact]
    public async Task Fails_with_a_local_storage_error_and_generates_no_key_when_the_saved_name_cannot_be_probed_or_read()
    {
        f.WriteDeviceFile("device-name.txt", "Desk");
        var files = new UnprobeableFileSystem(f.Files, f.DeviceFile("device-name.txt"));
        await using var sync = SyncService.Create(f.Tasks, files, f.Keys, f.Selection, f.Time, f.DeviceFolder, SyncFixture.DefaultDeviceName, null, f.Git);

        var failure = await Record.ExceptionAsync(() => sync.Setup.GenerateKeyAsync(CancellationToken.None));

        var io = Assert.IsType<IOException>(failure);
        Assert.IsType<UnauthorizedAccessException>(io.InnerException);
        Assert.Equal(SyncFailure.LocalStorage, sync.Status.Failure);
        Assert.Equal("Desk", f.ReadDeviceFile("device-name.txt"));
        Assert.Equal(0, f.Keys.GenerateCalls);
        Assert.Equal(0, files.Writes);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidDataException))]
    public async Task Reports_a_local_storage_error_when_loading_the_saved_repository_selection_fails_while_starting(Type type)
    {
        f.Ready();
        f.Selection.LoadFailure = (Exception)Activator.CreateInstance(type, "The saved selection cannot be loaded.")!;
        await using var sync = f.Create();
        var firstNotice = new TaskCompletionSource<(SyncState State, SyncFailure? Failure)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) => firstNotice.TrySetResult((sync.Status.State, sync.Status.Failure));

        sync.Start();
        var (state, kind) = await firstNotice.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, state);
        Assert.Equal(SyncFailure.LocalStorage, kind);
    }

    [Fact]
    public async Task Loads_the_saved_repository_selection_once_before_the_first_pass_when_starting()
    {
        f.Ready();
        var first = f.Git.ScriptPass();
        await using var sync = f.Create();
        try
        {
            sync.Start();
            await first.HasStarted.WaitAsync(Bound, TestContext.Current.CancellationToken);

            Assert.Equal(1, f.Selection.LoadCalls);
        }
        finally
        {
            first.Complete();
        }
    }

    private async Task AssertStartupFailureIsLocalStorage(Exception failure)
    {
        f.Ready();
        f.Git.OriginReadFailure = failure;
        await using var sync = f.Create();
        var firstNotice = new TaskCompletionSource<(SyncState State, SyncFailure? Failure)>(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.Setup.Changed += (_, _) => firstNotice.TrySetResult((sync.Status.State, sync.Status.Failure));

        sync.Start();
        var (state, kind) = await firstNotice.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(SyncState.Error, state);
        Assert.Equal(SyncFailure.LocalStorage, kind);
    }
}
