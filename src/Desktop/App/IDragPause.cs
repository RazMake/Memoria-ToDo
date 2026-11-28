using System.Windows.Input;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The pause that tells the description web view a drag has ended: a timer that ticks once its pause has passed with
/// no <see cref="Start"/> since, and the state of the mouse button the drag is made with. Used on the UI thread.
/// </summary>
internal interface IDragPause
{
    /// <summary>Raised once the pause has passed.</summary>
    event EventHandler? Tick;

    /// <summary>Starts the pause again from its beginning.</summary>
    void Start();

    /// <summary>Cancels the pause, if one is running.</summary>
    void Stop();

    /// <summary>Whether the mouse button a drag is made with is held down now.</summary>
    bool IsButtonDown { get; }
}

/// <summary>
/// The pause on the WPF dispatcher's clock. The button is WPF's <c>Mouse.LeftButton</c>, which WPF takes from Windows'
/// state of the primary button, the one that drags, also when the user swaps the buttons.
/// </summary>
internal sealed class DispatcherDragPause : IDragPause
{
    // The browser raises no event when a drag ends, so a drag ends once its box's style has been quiet this long.
    // Drag steps come about 30 ms apart.
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer _timer = new() { Interval = Pause };

    public DispatcherDragPause() => _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Tick;

    public bool IsButtonDown => Mouse.LeftButton == MouseButtonState.Pressed;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
