using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.Platform.Tests;

internal interface IHookChain
{
    nint CallNext(int code, nint message, nint data);
}

internal sealed class MarkedMoveWatch
{
    public const int MaxSamples = 4;
    private const int WmMouseMove = 0x0200;
    private const uint InjectionBits = 0x3;
    private const uint Injected = 0x1;
    private const int NotKept = -1;

    private static readonly int ExtraInfoOffset = (int)Marshal.OffsetOf<Win32.LowLevelMouseHook>(nameof(Win32.LowLevelMouseHook.ExtraInfo));
    private static readonly int FlagsOffset = (int)Marshal.OffsetOf<Win32.LowLevelMouseHook>(nameof(Win32.LowLevelMouseHook.Flags));
    private static readonly int PointOffset = (int)Marshal.OffsetOf<Win32.LowLevelMouseHook>(nameof(Win32.LowLevelMouseHook.Point));

    private readonly nint _marker;
    private readonly IHookChain _chain;
    private readonly TimeProvider _time;
    private readonly long _start;
    private readonly MarkedMoveSample?[] _samples = new MarkedMoveSample?[MaxSamples];
    private int _count;
    private Exception? _failure;

    public MarkedMoveWatch(nint marker, IHookChain chain, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(marker);
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(time);
        _marker = marker;
        _chain = chain;
        _time = time;
        _start = time.GetTimestamp();
    }

    public nint Observe(int code, nint message, nint data)
    {
        // Publish entry before calling downstream, so a blocked call remains visible.
        var index = code == 0 && message == WmMouseMove ? Enter(data) : NotKept;
        nint result;
        try
        {
            result = _chain.CallNext(code, message, data);
        }
        catch (Exception failure)
        {
            Fail(failure);
            return 0;
        }

        if (index != NotKept)
        {
            Complete(index, result);
        }

        return result;
    }

    public MarkedMoveReport Snapshot()
    {
        var count = Volatile.Read(ref _count);
        var samples = new MarkedMoveSample[Math.Min(count, MaxSamples)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = Volatile.Read(ref _samples[i])!;
        }

        return new MarkedMoveReport(count, samples, Volatile.Read(ref _failure));
    }

    private int Enter(nint packet)
    {
        try
        {
            if (packet == 0)
            {
                throw new InvalidOperationException("The low-level mouse hook gave no packet for a mouse move.");
            }

            if (Marshal.ReadIntPtr(packet, ExtraInfoOffset) != _marker)
            {
                return NotKept;
            }

            var flags = (uint)Marshal.ReadInt32(packet, FlagsOffset) & InjectionBits;
            if ((flags & Injected) == 0)
            {
                return NotKept;
            }

            var index = _count;
            if (index < MaxSamples)
            {
                var point = new Win32.Point
                {
                    X = Marshal.ReadInt32(packet, PointOffset),
                    Y = Marshal.ReadInt32(packet, PointOffset + sizeof(int)),
                };
                Volatile.Write(ref _samples[index], new MarkedMoveSample(Elapsed(), null, null, point, flags));
            }

            Volatile.Write(ref _count, index + 1);
            return index < MaxSamples ? index : NotKept;
        }
        catch (Exception failure)
        {
            Fail(failure);
            return NotKept;
        }
    }

    private void Complete(int index, nint result)
    {
        try
        {
            var entered = _samples[index]!;
            Volatile.Write(ref _samples[index], entered with { Completed = Elapsed(), Result = result });
        }
        catch (Exception failure)
        {
            Fail(failure);
        }
    }

    private void Fail(Exception failure) => Interlocked.CompareExchange(ref _failure, failure, null);
    private TimeSpan Elapsed() => _time.GetElapsedTime(_start);
}
