using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A pill dragged live: a press on a pill, then a move past Windows' drag distance, takes the mouse for the owner and reorders the
/// pills while the pointer moves; the pills show the new order at once. A release drops it, Esc or losing the mouse puts it back.
/// It reads the pointer through <see cref="IPointerPosition"/> and takes the mouse capture, so the pills follow it outside the owner too.
/// </summary>
internal sealed class PillDrag
{
    private readonly FrameworkElement _owner;
    private readonly IPointerPosition _pointer;
    private readonly TagPills _pills;
    private readonly Action _moved;
    private readonly Action<int, int> _dropped;
    private readonly Action _ended;
    private readonly ILogger _logger;
    private readonly PressDrag _press = new();
    private IReadOnlyList<string>? _start;
    private int _from = -1;
    private int _to = -1;
    private int _pressed = -1;
    private IReadOnlyList<string> _tagsAtPress = [];

    /// <param name="moved">Called when the order shown changes while dragging.</param>
    /// <param name="dropped">Called with the place the pill came from and the place it was dropped at, only when they differ.</param>
    /// <param name="ended">Called when a drag ends, however it ends, after the order shown is back to the tags.</param>
    public PillDrag(FrameworkElement owner, IPointerPosition pointer, TagPills pills, Action moved, Action<int, int> dropped, Action ended, ILogger logger)
    {
        _owner = owner;
        _pointer = pointer;
        _pills = pills;
        _moved = moved;
        _dropped = dropped;
        _ended = ended;
        _logger = logger;
        owner.PreviewMouseMove += OnMove;
        owner.PreviewMouseUp += OnUp;
        owner.LostMouseCapture += OnLostCapture;
        owner.MouseLeave += (_, _) =>
        {
            if (!IsDragging)
            {
                _press.Leave();
            }
        };
    }

    /// <summary>Whether a pill is being dragged.</summary>
    public bool IsDragging => _start is not null;

    /// <summary>The tags in the order the drag shows them now; only meaningful while <see cref="IsDragging"/>.</summary>
    public IReadOnlyList<string> Order { get; private set; } = [];

    /// <summary>The tag being dragged, or null.</summary>
    public string? Held => IsDragging ? _start![_from] : null;

    /// <summary>The left button went down on the pill at the place, while the tags are <paramref name="tags"/>.</summary>
    public void Press(int index, IReadOnlyList<string> tags)
    {
        _pressed = index;
        _tagsAtPress = tags;
        _press.Press(_pointer.PositionIn(_owner));
    }

    /// <summary>Esc: puts the pill back where it came from. Returns false, doing nothing, when no pill is being dragged.</summary>
    public bool Cancel()
    {
        if (!IsDragging)
        {
            return false;
        }

        _logger.Information("A tag's drag was cancelled, so its place is unchanged");
        Finish();
        Mouse.Capture(null);
        _ended();
        return true;
    }

    /// <summary>Drops a drag in progress, as <see cref="Cancel"/> does, and a press that hasn't become a drag yet, so a move after it starts nothing.</summary>
    public void Abandon()
    {
        if (!Cancel())
        {
            Finish();
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        var at = _pointer.PositionIn(_owner);
        if (!IsDragging)
        {
            if (!_press.Move(at, leftButtonDown: true) || _pressed < 0 || _pressed >= _tagsAtPress.Count)
            {
                return;
            }

            if (!Mouse.Capture(_owner, CaptureMode.SubTree))
            {
                _logger.Warning("A tag's drag couldn't start: the editor couldn't take the mouse");
                return;
            }

            _start = _tagsAtPress;
            _from = _to = _pressed;
            Order = _start;
            _logger.Information("Dragging the tag at place {From}", _from);
        }

        var to = _pills.NearestTo(at, _owner);
        if (to != _to)
        {
            _to = to;
            Order = TagOrder.Move(_start!, _from, to);
            _moved();
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (!IsDragging)
        {
            _press.Release();
            return;
        }

        var (from, to) = (_from, _to);
        Finish();
        Mouse.Capture(null);
        e.Handled = true;
        if (from != to)
        {
            _logger.Information("Dropped the tag from place {From} at place {To}", from, to);
            _dropped(from, to);
        }

        _ended();
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (IsDragging && !ReferenceEquals(Mouse.Captured, _owner))
        {
            _logger.Information("A tag's drag was cancelled because the mouse was taken, so its place is unchanged");
            Finish();
            _ended();
        }
    }

    private void Finish()
    {
        _start = null;
        _from = _to = _pressed = -1;
        _press.Release();
    }
}
