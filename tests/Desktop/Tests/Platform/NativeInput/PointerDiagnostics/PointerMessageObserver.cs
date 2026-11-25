using System.Runtime.InteropServices;
using System.Windows.Interop;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Observes the test thread's input path without changing its messages or their handling.</summary>
[RealInput]
internal sealed class PointerMessageObserver
{
    private readonly HwndSource _source;
    private readonly PointerMoveRecorder _recorder;
    private readonly Action<ObservedMessage> _processed;
    private readonly Action<uint> _filtered;
    private readonly Win32.MessageHook _retrieved;
    private readonly Win32.MessageHook _mouse;
    private readonly Win32.MessageHook _sent;
    private readonly HwndSourceHook _arrived;
    private nint _retrievalHook;
    private nint _mouseHook;
    private nint _sentHook;
    private bool _targetAttached;
    private bool _filterAttached;

    public PointerMessageObserver(HwndSource source, PointerMoveRecorder recorder,
        Action<ObservedMessage> processed, Action<uint> filtered)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(filtered);
        _source = source;
        _recorder = recorder;
        _processed = processed;
        _filtered = filtered;
        _retrieved = Retrieved;
        _mouse = Mouse;
        _sent = Sent;
        _arrived = Arrived;
    }

    public void Attach()
    {
        _source.Dispatcher.VerifyAccess();
        if (_retrievalHook != 0)
        {
            throw new InvalidOperationException("The pointer observer is already attached.");
        }
        _retrievalHook = Win32.HookRetrievedMessages(_retrieved);
        _mouseHook = Win32.HookMouseMessages(_mouse);
        _sentHook = Win32.HookSentMessages(_sent);
        _source.AddHook(_arrived);
        _targetAttached = true;
        ComponentDispatcher.ThreadFilterMessage += Filtered;
        _filterAttached = true;
    }

    public void Release(FailureList failures)
    {
        if (_filterAttached)
        {
            _filterAttached = false;
            failures.Attempt(() => ComponentDispatcher.ThreadFilterMessage -= Filtered);
        }
        if (_targetAttached)
        {
            _targetAttached = false;
            failures.Attempt(() => _source.RemoveHook(_arrived));
        }
        Unhook(ref _sentHook, failures);
        Unhook(ref _mouseHook, failures);
        Unhook(ref _retrievalHook, failures);
        GC.KeepAlive(_retrieved);
        GC.KeepAlive(_mouse);
        GC.KeepAlive(_sent);
    }

    private static void Unhook(ref nint handle, FailureList failures)
    {
        var hook = handle;
        handle = 0;
        if (hook != 0)
        {
            failures.Attempt(() => Win32.UnhookMessages(hook));
        }
    }

    private nint Retrieved(int code, nint removal, nint data)
    {
        if (code == 0)
        {
            _recorder.Guard(() => _recorder.Retrieved(Marshal.PtrToStructure<Win32.Message>(data), removal != 0));
        }
        return Win32.NextMessage(code, removal, data);
    }

    private nint Mouse(int code, nint id, nint data)
    {
        if (code is 0 or 3)
        {
            _recorder.Guard(() =>
            {
                var packet = Marshal.PtrToStructure<Win32.MouseHook>(data);
                _recorder.MouseObserved((uint)id, packet, processed: code == 0);
                if (code == 0)
                {
                    _processed(new ObservedMessage((uint)id, packet.ExtraInfo, packet.Point,
                        packet.Window == _source.Handle || Win32.IsChildOf(packet.Window, _source.Handle)));
                }
            });
        }
        return Win32.NextMessage(code, id, data);
    }

    private nint Sent(int code, nint message, nint data)
    {
        if (code == 0)
        {
            _recorder.Guard(() => _recorder.SentMessage(Marshal.PtrToStructure<Win32.SentMessage>(data)));
        }
        return Win32.NextMessage(code, message, data);
    }

    private void Filtered(ref MSG message, ref bool handled)
    {
        var packet = new Win32.Message
        {
            Id = (uint)message.message, Window = message.hwnd,
            Point = new Win32.Point { X = message.pt_x, Y = message.pt_y },
        };
        _recorder.Guard(() =>
        {
            _recorder.Filtered(packet);
            _filtered(packet.Id);
        });
    }

    private nint Arrived(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        _recorder.Guard(() => _recorder.Arrived(window, (uint)message));
        return 0;
    }
}
