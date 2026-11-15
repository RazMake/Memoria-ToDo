using System.Diagnostics;
using System.Threading.Channels;
using Xunit.Sdk;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Tells when a process shows a message box, by Windows' own signals instead of looking for it again and again.
/// Windows sends "a dialog started" for standard dialogs, which includes message boxes, and "shown" for every window.
/// A message box is reported once it is visible, which is after its text is set. Create it before the process starts,
/// so no message box can come before it listens.
/// </summary>
internal sealed class DialogWatcher : IDisposable
{
    private readonly Channel<(nint Dialog, int ProcessId)> _shown = Channel.CreateUnbounded<(nint, int)>();
    private readonly HashSet<nint> _started = []; // Used only by the message-loop thread.
    private readonly Win32.WinEventProc _callback; // Kept so it isn't collected while Windows may call it.
    private uint _loopThreadId;

    public DialogWatcher()
    {
        _callback = OnEvent;
        Exception? hookFailure = null;
        using var listening = new ManualResetEventSlim();
        // Out-of-context hooks are called on the thread that set them, from its message loop, so it gets a thread of its own.
        new Thread(() =>
        {
            nint hook;
            try
            {
                hook = Win32.HookDialogEvents(_callback);
            }
            catch (Exception e)
            {
                hookFailure = e;
                listening.Set();
                return;
            }
            _loopThreadId = Win32.CurrentThreadId();
            listening.Set();
            Win32.RunMessageLoop();
            Win32.Unhook(hook);
        })
        { IsBackground = true }.Start();
        listening.Wait();
        if (hookFailure is not null)
        {
            throw new InvalidOperationException("The message box watcher couldn't listen for dialogs.", hookFailure);
        }
    }

    /// <summary>Waits until the process shows a message box, and returns it. Fails if the process ends first, or after the limit.</summary>
    public nint WaitForMessageBoxOf(Process process, TimeSpan limit)
    {
        var exited = process.WaitForExitAsync();
        var timeout = Task.Delay(limit); // Only the failure bound; a message box ends the wait at once.
        while (true)
        {
            var arrived = _shown.Reader.WaitToReadAsync().AsTask();
            var first = Task.WhenAny(arrived, exited, timeout).GetAwaiter().GetResult();
            if (first == arrived)
            {
                while (_shown.Reader.TryRead(out var box))
                {
                    if (box.ProcessId == process.Id)
                    {
                        return box.Dialog;
                    }
                }
            }
            else
            {
                throw new XunitException(first == exited
                    ? "The test program ended without showing a message box."
                    : $"The test program showed no message box within {limit}.");
            }
        }
    }

    public void Dispose() => Win32.StopMessageLoop(_loopThreadId);

    private void OnEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time)
    {
        if (eventType == Win32.EventSystemDialogStart)
        {
            if (Win32.IsVisible(window))
            {
                Report(window);
            }
            else
            {
                _started.Add(window);
            }
        }
        else if (objectId == 0 && _started.Remove(window))
        {
            Report(window);
        }
    }

    private void Report(nint dialog) => _shown.Writer.TryWrite((dialog, Win32.ProcessIdOf(dialog)));
}
