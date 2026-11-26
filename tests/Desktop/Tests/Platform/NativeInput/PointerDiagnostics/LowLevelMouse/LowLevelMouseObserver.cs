using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[RealInput]
internal sealed class LowLevelMouseObserver : IHookChain
{
    private readonly MarkedMoveWatch _watch;
    private readonly Win32.MessageHook _hook;
    private readonly MessageLoopThread _thread;
    private bool _attached;
    private HookLifecycle _lifecycle = HookLifecycle.NeverInstalled;
    private Exception? _reportedFailure;

    public LowLevelMouseObserver(nint marker)
    {
        _watch = new MarkedMoveWatch(marker, this, TimeProvider.System);
        _hook = _watch.Observe;
        _thread = new MessageLoopThread(new NativeMessageLoop(),
            () => Win32.HookLowLevelMouse(_hook), Win32.UnhookMessages, TestWait.ForSignal);
    }

    public void Attach()
    {
        if (_attached)
        {
            throw new InvalidOperationException("The low-level mouse observer is already attached.");
        }

        _attached = true;
        _lifecycle = HookLifecycle.Installing;
        try
        {
            _thread.Start();
            _lifecycle = HookLifecycle.Installed;
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch
        {
            _lifecycle = HookLifecycle.NeverInstalled;
            throw;
        }
    }

    public MarkedMoveReport Snapshot()
    {
        var report = _watch.Snapshot() with { Lifecycle = _lifecycle };
        _reportedFailure = report.Failure;
        return report;
    }

    public void Release(FailureList failures)
    {
        var stopped = new FailureList();
        _thread.Stop(stopped);
        try
        {
            stopped.ThrowIfAny();
            if (_lifecycle == HookLifecycle.Installed)
            {
                _lifecycle = HookLifecycle.Removed;
            }
        }
        catch (Exception failure)
        {
            if (_lifecycle == HookLifecycle.Installed)
            {
                _lifecycle = HookLifecycle.RemovalFailed;
            }
            failures.Add(failure);
        }

        if (_watch.Snapshot().Failure is { } late && !ReferenceEquals(late, _reportedFailure))
        {
            failures.Add(late);
            _reportedFailure = late;
        }
        GC.KeepAlive(_hook);
    }

    nint IHookChain.CallNext(int code, nint message, nint data) => Win32.NextMessage(code, message, data);
}
