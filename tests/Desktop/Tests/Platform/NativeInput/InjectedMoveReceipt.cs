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

    public InjectedMoveReceipt(nint marker, Win32.Point target)
    {
        ArgumentOutOfRangeException.ThrowIfZero(marker);
        _marker = marker;
        _target = target;
    }

    public bool Received { get; private set; }
    public Win32.Point? MisplacedAt { get; private set; }

    public void Observe(ObservedMessage message)
    {
        _eligible = false;
        if (message.Id != WmMouseMove || message.ExtraInfo != _marker || !message.InExpectedWindow)
        {
            return;
        }

        Received = true;
        if (Math.Abs((long)message.Screen.X - _target.X) > Tolerance || Math.Abs((long)message.Screen.Y - _target.Y) > Tolerance)
        {
            MisplacedAt = message.Screen;
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
