using System.Runtime.ExceptionServices;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.Platform.Tests;

internal interface IMessageLoop
{
    void Prepare();
    uint CurrentThread();
    void Run();
    void Quit(uint thread);
}

internal interface IMessageLoopThread
{
    void Start();
    void Stop(FailureList failures);
}

internal sealed class MessageLoopThread : IMessageLoopThread
{
    private const int Starting = 0;
    private const int Running = 1;
    private const int Failed = 2;
    private const int Abandoned = 3;

    private readonly IMessageLoop _loop;
    private readonly Func<nint> _install;
    private readonly Action<nint> _remove;
    private readonly TimeSpan _bound;
    private readonly ManualResetEvent _ready = new(false);
    private readonly ManualResetEvent _ended = new(false);
    private int _owners = 2;
    private Thread? _thread;
    private int _state;
    private int _stopped;
    private uint _threadId;
    // Each is written once by the thread before the signal that publishes it.
    private Exception? _startFailure;
    private Exception? _loopFailure;
    private Exception? _removeFailure;

    public MessageLoopThread(IMessageLoop loop, Func<nint> install, Action<nint> remove, TimeSpan bound)
    {
        ArgumentNullException.ThrowIfNull(loop);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(remove);
        ArgumentOutOfRangeException.ThrowIfLessThan(bound, TimeSpan.Zero);
        _loop = loop;
        _install = install;
        _remove = remove;
        _bound = bound;
    }

    public void Start()
    {
        if (_thread is not null)
        {
            throw new InvalidOperationException("The message loop thread was already started.");
        }

        _thread = new Thread(Body) { IsBackground = true, Name = "Message loop hook thread" };
        _thread.Start();
        try
        {
            Win32.WaitWithoutPumping(_ready, _bound);
        }
        catch
        {
            Interlocked.CompareExchange(ref _state, Abandoned, Starting);
            throw;
        }

        var decided = Interlocked.CompareExchange(ref _state, Abandoned, Starting);
        if (decided == Starting)
        {
            throw new TimeoutException($"The message loop thread did not install its hook within {_bound}.");
        }

        if (decided == Failed)
        {
            ExceptionDispatchInfo.Capture(_startFailure!).Throw();
        }
    }

    public void Stop(FailureList failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (_thread is null || Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        try
        {
            var state = Volatile.Read(ref _state);
            if (state == Running && !Win32.WaitWithoutPumping(_ended, TimeSpan.Zero))
            {
                failures.Attempt(() => _loop.Quit(_threadId));
            }

            if (!Win32.WaitWithoutPumping(_ended, _bound))
            {
                failures.Add(new TimeoutException($"The message loop thread did not end within {_bound}."));
                return;
            }

            if (state == Abandoned && _startFailure is not null)
            {
                failures.Add(_startFailure);
            }

            if (_loopFailure is not null)
            {
                failures.Add(_loopFailure);
            }

            if (_removeFailure is not null)
            {
                failures.Add(_removeFailure);
            }
        }
        finally
        {
            CloseWhenUnused();
        }
    }

    private void Body()
    {
        try
        {
            nint hook;
            try
            {
                _loop.Prepare();
                _threadId = _loop.CurrentThread();
                hook = _install();
            }
            catch (Exception failure)
            {
                _startFailure = failure;
                Interlocked.CompareExchange(ref _state, Failed, Starting);
                _ready.Set();
                return;
            }

            if (Interlocked.CompareExchange(ref _state, Running, Starting) != Starting)
            {
                Remove(hook);
                return;
            }

            _ready.Set();
            try
            {
                _loop.Run();
            }
            catch (Exception failure)
            {
                _loopFailure = failure;
            }

            Remove(hook);
        }
        finally
        {
            _ended.Set();
            CloseWhenUnused();
        }
    }

    private void CloseWhenUnused()
    {
        if (Interlocked.Decrement(ref _owners) == 0)
        {
            _ready.Dispose();
            _ended.Dispose();
        }
    }

    private void Remove(nint hook)
    {
        try
        {
            _remove(hook);
        }
        catch (Exception failure)
        {
            _removeFailure = failure;
        }
    }
}
