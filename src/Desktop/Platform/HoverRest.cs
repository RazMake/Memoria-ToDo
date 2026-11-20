using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>What <see cref="HoverRest"/> asks its owner to do.</summary>
internal interface IHoverRestOutput
{
    /// <summary>Finds the part at the place, in the owner's coordinates, and answers through <see cref="HoverRest.PartAnswered"/> with the same token. May be called on any thread.</summary>
    void FindPart(Point at, int token);

    /// <summary>Runs the action later on the owner's UI thread. May be called on any thread.</summary>
    void Post(Action action);

    /// <summary>Shows the card of the part the answer with this token named.</summary>
    void Show(int token);

    /// <summary>Closes the card shown.</summary>
    void Close();
}

/// <summary>
/// Decides when a hover card is asked for, shown, and closed, from plain inputs, so the rules need no mouse, window, or clock of
/// their own. Inputs come on the UI thread; only the timers run on another, and they reach the owner only through
/// <see cref="IHoverRestOutput.FindPart"/> and <see cref="IHoverRestOutput.Post"/>.
/// </summary>
internal sealed class HoverRest
{
    private readonly object _gate = new();
    private readonly TimeSpan _hoverTime;
    private readonly HoverCardSlot _slot;
    private readonly IHoverRestOutput _output;
    private readonly ITimer _timer;
    private readonly ITimer _graceTimer;
    private readonly Action _replaced;
    private bool _active;
    private bool _onCard;
    private bool _graceRunning;
    private int _grace;
    private Point? _point;
    private bool _resting;
    private bool _awaiting;
    private int _token;
    private Rect? _shown;

    public HoverRest(TimeProvider time, TimeSpan hoverTime, HoverCardSlot slot, IHoverRestOutput output, bool windowActive)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(output);
        _hoverTime = hoverTime;
        _slot = slot;
        _output = output;
        _active = windowActive;
        _replaced = Replaced;
        _timer = time.CreateTimer(_ => Rested(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _graceTimer = time.CreateTimer(_ => GraceEnded(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// The pointer is at the place, in the owner's coordinates. Moving to a new place restarts the wait for a rest there. Moving out of a
    /// card's part also starts a grace period of the hover time, so the pointer can reach the card; moving back into the part ends it and
    /// the wait. A part found where the pointer rested replaces the card shown. Ignored while the pointer is on the card.
    /// </summary>
    public void PointerMoved(Point at)
    {
        lock (_gate)
        {
            if (!_active || _onCard)
            {
                return;
            }

            if (_shown is { } box)
            {
                if (box.Contains(at))
                {
                    CancelGrace();
                    CancelRest();
                    return;
                }

                if (!_graceRunning)
                {
                    StartGrace();
                }
            }

            if (_point == at)
            {
                return;
            }

            CancelRest();
            _point = at;
            _resting = true;
            _timer.Change(_hoverTime, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The pointer is on the shown card. Ends a grace period and any wait for another part; the card stays until the pointer leaves it.</summary>
    public void CardEntered()
    {
        lock (_gate)
        {
            if (_shown is null)
            {
                return;
            }

            _onCard = true;
            CancelGrace();
            CancelRest();
        }
    }

    /// <summary>The pointer left the card. The card closes after the hover time unless the pointer comes back to it or its part.</summary>
    public void CardLeft()
    {
        lock (_gate)
        {
            if (!_onCard)
            {
                return;
            }

            _onCard = false;
            StartGrace();
        }
    }

    public void PointerLeft() => StopAndClose();

    /// <summary>A mouse button went down, on the card or elsewhere. A press on the card changes nothing.</summary>
    public void ButtonPressed(bool onCard)
    {
        if (!onCard)
        {
            StopAndClose();
        }
    }

    /// <summary>The wheel turned, over the card or elsewhere. A turn over the card changes nothing.</summary>
    public void WheelTurned(bool onCard)
    {
        if (!onCard)
        {
            StopAndClose();
        }
    }

    /// <summary>A key went down. Modifier keys change nothing, so a card's own Ctrl handling can work.</summary>
    public void KeyPressed(bool isModifier)
    {
        if (!isModifier)
        {
            StopAndClose();
        }
    }

    public void WindowActivated()
    {
        lock (_gate)
        {
            _active = true;
        }
    }

    public void WindowDeactivated()
    {
        lock (_gate)
        {
            _active = false;
        }

        StopAndClose();
    }

    public void WindowMoved() => StopAndClose();

    public void WindowHidden() => StopAndClose();

    public void Unloaded() => StopAndClose();

    /// <summary>The owner found the part for a request: its box, or null for none. An answer to anything but the newest request, or after a closing input, is dropped.</summary>
    public void PartAnswered(int token, Rect? box)
    {
        bool wasShowing;
        lock (_gate)
        {
            if (!_awaiting || token != _token)
            {
                return;
            }

            _awaiting = false;
            if (box is not { } part)
            {
                return;
            }

            wasShowing = _shown is not null;
            _shown = part;
            _onCard = false;
            CancelGrace();
        }

        Closed(wasShowing);
        _slot.Show(_replaced);
        _output.Show(token);
    }

    private void Rested()
    {
        Point at;
        int token;
        lock (_gate)
        {
            if (!_resting)
            {
                return;
            }

            _resting = false;
            _awaiting = true;
            token = ++_token;
            at = _point!.Value;
        }

        _output.FindPart(at, token);
    }

    private void StopAndClose()
    {
        bool wasShowing;
        lock (_gate)
        {
            wasShowing = Stop();
        }

        Closed(wasShowing);
    }

    // The hover time passed, on the timer's thread. The close itself must come back on the UI thread, where the pointer may have
    // moved on meanwhile, so it checks that the grace period it was posted for is still the running one.
    private void GraceEnded()
    {
        int grace;
        lock (_gate)
        {
            grace = _grace;
        }

        _output.Post(() => GraceClosed(grace));
    }

    private void GraceClosed(int grace)
    {
        bool wasShowing;
        lock (_gate)
        {
            if (!_graceRunning || _onCard || grace != _grace)
            {
                return;
            }

            // Only the card goes; a rest still pending on another part goes on to open its card.
            CancelGrace();
            wasShowing = _shown is not null;
            _shown = null;
        }

        Closed(wasShowing);
    }

    private void StartGrace()
    {
        _graceRunning = true;
        _grace++;
        _graceTimer.Change(_hoverTime, Timeout.InfiniteTimeSpan);
    }

    private void CancelGrace()
    {
        _graceRunning = false;
        _graceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // Cancels the wait, the grace period, the pending answer, and the resting place, so nothing opens until the pointer moves again.
    private bool Stop()
    {
        CancelGrace();
        _onCard = false;
        CancelRest();
        var wasShowing = _shown is not null;
        _shown = null;
        return wasShowing;
    }

    // Cancels the wait for a rest, the pending answer, and the resting place; the card shown and its grace period stay.
    private void CancelRest()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _resting = false;
        _awaiting = false;
        _point = null;
    }

    private void Closed(bool wasShowing)
    {
        if (wasShowing)
        {
            _slot.Closed(_replaced);
            _output.Close();
        }
    }

    // Another card took the slot.
    private void Replaced()
    {
        bool wasShowing;
        lock (_gate)
        {
            wasShowing = _shown is not null;
            _shown = null;
            _onCard = false;
            CancelGrace();
        }

        if (wasShowing)
        {
            _output.Close();
        }
    }
}
