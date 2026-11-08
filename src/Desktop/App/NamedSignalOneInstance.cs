using System.Runtime.InteropServices;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// One instance per sign-in session, by one named Windows semaphore in the <c>Local\</c> namespace. Whoever creates
/// it is the first instance and keeps it; a later one only opens it, adds one to its count, and lets go. The count
/// keeps each later start, even when several come before the first instance wakes. The semaphore itself is the
/// claim, so no thread owns anything: instances are told apart by the name, not by the thread or the process.
/// </summary>
internal sealed class NamedSignalOneInstance : IOneInstance
{
    private const uint AllowAnyProcess = uint.MaxValue; // ASFW_ANY

    private readonly string _signalName;
    private Semaphore? _signal;
    private RegisteredWaitHandle? _wait;

    public NamedSignalOneInstance(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        _signalName = $@"Local\{signalName}";
    }

    public bool Claim(Action laterStart)
    {
        ArgumentNullException.ThrowIfNull(laterStart);
        if (_signal is not null)
        {
            throw new InvalidOperationException("This instance already holds the claim.");
        }

        var signal = new Semaphore(initialCount: 0, maximumCount: int.MaxValue, _signalName, out var createdNew);
        if (!createdNew)
        {
            // The running instance may otherwise be refused the front, since this process holds the foreground right.
            AllowSetForegroundWindow(AllowAnyProcess);
            signal.Release();
            signal.Dispose();
            return false;
        }

        var uiThread = Dispatcher.CurrentDispatcher;
        _signal = signal;
        _wait = ThreadPool.RegisterWaitForSingleObject(
            signal,
            (_, _) => uiThread.BeginInvoke(laterStart),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        return true;
    }

    public void Release()
    {
        var signal = _signal;
        if (signal is null)
        {
            return;
        }

        _signal = null;
        _wait!.Unregister(null);
        _wait = null;
        signal.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
