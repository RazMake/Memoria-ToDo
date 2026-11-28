using System.ComponentModel;
using System.Runtime.InteropServices;
using Memoria.ToDo.Desktop.KeepAwake;
using Microsoft.Win32.SafeHandles;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Windows' power request that keeps the display, and the system, on while it's set. Windows drops it when the process ends.</summary>
internal sealed class PowerDisplayRequest : IDisplayRequest
{
    private const string Reason = "Memoria-ToDo: Prevent screen saver is on";
    private const uint SimpleStringContext = 0x1; // POWER_REQUEST_CONTEXT_SIMPLE_STRING
    private const int DisplayRequired = 0;        // PowerRequestDisplayRequired
    private const int SystemRequired = 1;         // PowerRequestSystemRequired

    private RequestHandle? _handle; // Made by the first Set(true), so a refusal shows in Keep awake and never stops the app starting.
    private bool _displaySet; // Windows counts each request type, so each is tracked on its own.
    private bool _systemSet;

    /// <summary>Sets each request type not yet set, or clears each one still set. Throws <see cref="Win32Exception"/> when Windows refuses.</summary>
    public void Set(bool on)
    {
        if (on)
        {
            var handle = _handle ??= Create();
            if (!_displaySet)
            {
                Call(PowerSetRequest, handle, DisplayRequired);
                _displaySet = true;
            }

            if (!_systemSet)
            {
                try
                {
                    Call(PowerSetRequest, handle, SystemRequired);
                }
                catch
                {
                    if (PowerClearRequest(handle, DisplayRequired))
                    {
                        _displaySet = false;
                    }

                    throw;
                }

                _systemSet = true;
            }
        }
        else if (_handle is { } handle)
        {
            if (_displaySet)
            {
                Call(PowerClearRequest, handle, DisplayRequired);
                _displaySet = false;
            }

            if (_systemSet)
            {
                Call(PowerClearRequest, handle, SystemRequired);
                _systemSet = false;
            }
        }
    }

    private static void Call(Func<RequestHandle, int, bool> power, RequestHandle handle, int type)
    {
        if (!power(handle, type))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private static RequestHandle Create()
    {
        var reason = Marshal.StringToHGlobalUni(Reason);
        try
        {
            var context = new ReasonContext { Version = 0, Flags = SimpleStringContext, SimpleReasonString = reason };
            var handle = PowerCreateRequest(ref context);
            return handle.IsInvalid ? throw new Win32Exception(Marshal.GetLastPInvokeError()) : handle;
        }
        finally
        {
            Marshal.FreeHGlobal(reason);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
    }

    private sealed class RequestHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public RequestHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern RequestHandle PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerSetRequest(RequestHandle request, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PowerClearRequest(RequestHandle request, int requestType);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
