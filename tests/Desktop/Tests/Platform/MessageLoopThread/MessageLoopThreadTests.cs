using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class MessageLoopThreadTests
{
    private sealed class Loop : IMessageLoop, IDisposable
    {
        public readonly ManualResetEventSlim Pumping = new();
        public readonly ManualResetEventSlim End = new();
        public uint PreparedOn;
        public uint QuitThread;
        public Exception? PrepareFailure;
        public Exception? RunFailure;
        public Exception? QuitFailure;
        public bool IgnoreQuit;
        public void Prepare()
        {
            if (PrepareFailure is not null) throw PrepareFailure;
            PreparedOn = CurrentThread();
        }
        public uint CurrentThread() => (uint)Environment.CurrentManagedThreadId;
        public void Run()
        {
            Pumping.Set();
            if (!End.Wait(TestWait.ForSignal)) throw new TimeoutException("Test did not end its fake loop.");
            if (RunFailure is not null) throw RunFailure;
        }
        public void Quit(uint thread)
        {
            QuitThread = thread;
            if (!IgnoreQuit) End.Set();
            if (QuitFailure is not null) throw QuitFailure;
        }
        public void Dispose() { Pumping.Dispose(); End.Dispose(); }
    }

    [Fact]
    public void Owns_installation_pumping_and_removal_on_one_prepared_background_thread()
    {
        using var loop = new Loop();
        uint installedOn = 0, removedOn = 0;
        nint removedHandle = 0;
        var worker = new MessageLoopThread(loop, () =>
        {
            installedOn = loop.CurrentThread();
            Assert.Equal(loop.PreparedOn, installedOn);
            return 81;
        }, handle => { removedOn = loop.CurrentThread(); removedHandle = handle; }, TestWait.ForSignal);
        worker.Start();
        Assert.True(loop.Pumping.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken));
        var failures = new FailureList();
        worker.Stop(failures);
        worker.Stop(failures);
        failures.ThrowIfAny();
        Assert.NotEqual((uint)Environment.CurrentManagedThreadId, installedOn);
        Assert.Equal(installedOn, removedOn);
        Assert.Equal(installedOn, loop.QuitThread);
        Assert.Equal((nint)81, removedHandle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reports_setup_failure_without_running_the_loop_or_removing_an_uninstalled_hook(bool preparing)
    {
        using var loop = new Loop();
        var error = new InvalidOperationException("install-failure");
        if (preparing) loop.PrepareFailure = error;
        var removed = false;
        var worker = new MessageLoopThread(loop, () => throw error, _ => removed = true, TestWait.ForSignal);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(worker.Start));
        var failures = new FailureList();
        worker.Stop(failures);
        failures.ThrowIfAny();
        Assert.False(loop.Pumping.IsSet);
        Assert.False(removed);
    }

    [Fact]
    public void A_late_installation_after_startup_timeout_removes_its_hook_without_pumping()
    {
        using var loop = new Loop();
        using var allowInstall = new ManualResetEventSlim();
        using var removed = new ManualResetEventSlim();
        var worker = new MessageLoopThread(loop, () =>
        {
            if (!allowInstall.Wait(TestWait.ForSignal)) throw new TimeoutException("Test did not allow installation.");
            return 91;
        }, _ => removed.Set(), TimeSpan.Zero);
        try
        {
            Assert.Throws<TimeoutException>(worker.Start);
        }
        finally
        {
            allowInstall.Set();
        }
        Assert.True(removed.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken));
        Assert.False(loop.Pumping.IsSet);
    }

    [Theory]
    [InlineData("loop")]
    [InlineData("remove")]
    [InlineData("quit")]
    public void Reports_a_lifetime_failure_instead_of_silently_succeeding(string step)
    {
        using var loop = new Loop();
        var error = new InvalidOperationException(step);
        if (step == "loop") loop.RunFailure = error;
        if (step == "quit") loop.QuitFailure = error;
        var worker = new MessageLoopThread(loop, () => 81, _ =>
        {
            if (step == "remove") throw error;
        }, TestWait.ForSignal);
        worker.Start();
        Assert.True(loop.Pumping.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken));
        var failures = new FailureList();
        worker.Stop(failures);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(failures.ThrowIfAny));
    }

    [Fact]
    public void Reports_failure_to_remove_a_hook_installed_after_startup_timed_out()
    {
        using var loop = new Loop();
        using var allowInstall = new ManualResetEventSlim();
        var error = new InvalidOperationException("late-remove-failed");
        var worker = new MessageLoopThread(loop, () =>
        {
            if (!allowInstall.Wait(TestWait.ForSignal)) throw new TimeoutException("Test did not allow installation.");
            return 91;
        }, _ => throw error, TimeSpan.FromMilliseconds(200));
        try
        {
            Assert.Throws<TimeoutException>(worker.Start);
        }
        finally
        {
            allowInstall.Set();
        }
        var failures = new FailureList();
        worker.Stop(failures);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(failures.ThrowIfAny));
        Assert.False(loop.Pumping.IsSet);
    }

    [Fact]
    public void A_loop_that_will_not_stop_fails_within_its_bound()
    {
        using var loop = new Loop { IgnoreQuit = true };
        using var removed = new ManualResetEventSlim();
        var worker = new MessageLoopThread(loop, () => 81, _ => removed.Set(), TimeSpan.FromMilliseconds(200));
        worker.Start();
        Assert.True(loop.Pumping.Wait(TestWait.ForSignal, TestContext.Current.CancellationToken));
        try
        {
            var failures = new FailureList();
            worker.Stop(failures);
            Assert.Throws<TimeoutException>(failures.ThrowIfAny);
        }
        finally
        {
            loop.End.Set();
            Assert.True(removed.Wait(TestWait.ForSignal, CancellationToken.None));
        }
    }
}
