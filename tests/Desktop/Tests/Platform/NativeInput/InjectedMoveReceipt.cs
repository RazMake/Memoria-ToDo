namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>A fetched message's identity, screen point, and target-window match.</summary>
internal readonly record struct ObservedMessage(uint Id, nint ExtraInfo, Win32.Point Screen, bool InExpectedWindow);

/// <summary>Correlates a routed mouse move with its injected native packet.</summary>
internal sealed class InjectedMoveReceipt
{
    private const uint WmMouseMove = 0x0200;
    private const int Tolerance = 1;
    private readonly nint _marker;
    private readonly Win32.Point _target;
    private bool _eligible;
    private bool _taken;

    public InjectedMoveReceipt(nint marker, Win32.Point start, Win32.Point target)
    {
        ArgumentOutOfRangeException.ThrowIfZero(marker);
        if (IsAtTarget(start, target))
        {
            throw new ArgumentException(
                "A move must start more than one pixel from its target, or Windows may drop it as no movement.", nameof(start));
        }

        _marker = marker;
        _target = target;
    }

    public bool Received { get; private set; }
    public Win32.Point? MisplacedAt { get; private set; }

    public static bool IsAtTarget(Win32.Point observed, Win32.Point target) =>
        Math.Abs((long)observed.X - target.X) <= Tolerance && Math.Abs((long)observed.Y - target.Y) <= Tolerance;

    public void Observe(ObservedMessage message)
    {
        _eligible = false;
        // Windows replaces a pending move with an unmarked one (extra info 0) when any window shows, hides, or moves.
        var own = message.ExtraInfo == _marker;
        if (message.Id != WmMouseMove || !message.InExpectedWindow || (!own && message.ExtraInfo != 0))
        {
            return;
        }

        var atTarget = IsAtTarget(message.Screen, _target);
        if (own)
        {
            Received = true;
            if (!atTarget)
            {
                MisplacedAt = message.Screen;
                return;
            }
        }
        else if (!atTarget)
        {
            return;
        }

        _eligible = MisplacedAt is null;
    }

    public bool TakeAcknowledgement()
    {
        if (!_eligible || _taken)
        {
            return false;
        }

        _taken = true;
        return true;
    }
}
