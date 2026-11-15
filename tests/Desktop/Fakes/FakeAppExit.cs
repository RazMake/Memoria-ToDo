using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IAppExit"/>. A test says whether a window is open and whether closing the windows works,
/// and reads the calls Updates made, in order. It ends nothing: a test calls <see cref="IUpdateLifetime.StopAsync"/> itself,
/// as Shell's exit does.
/// </summary>
public sealed class FakeAppExit : IAppExit
{
    private readonly object gate = new();
    private readonly List<string> calls = [];
    private readonly CountSignal ends = new();
    private readonly CountSignal closes = new();

    /// <inheritdoc />
    public bool AnyWindowOpen { get; set; }

    /// <summary>What <see cref="CloseWindows"/> answers; false when Task View stays open. Starts true.</summary>
    public bool CloseWindowsResult { get; set; } = true;

    /// <summary>The calls Updates made, in order: "CloseWindows" and "End".</summary>
    public IReadOnlyList<string> Calls
    {
        get { lock (gate) { return [.. calls]; } }
    }

    /// <summary>How many times the app was ended.</summary>
    public int EndCount => ends.Count;

    /// <summary>How many times the windows were asked to close.</summary>
    public int CloseWindowsCount => closes.Count;

    /// <inheritdoc />
    public bool CloseWindows()
    {
        lock (gate) { calls.Add("CloseWindows"); }
        closes.Increment();
        return CloseWindowsResult;
    }

    /// <inheritdoc />
    public void End()
    {
        lock (gate) { calls.Add("End"); }
        ends.Increment();
    }

    /// <summary>Completes once the app has been ended at least <paramref name="count"/> times.</summary>
    public Task WaitForEndsAsync(int count) => ends.WaitForAsync(count);

    /// <summary>Completes once the windows have been asked to close at least <paramref name="count"/> times.</summary>
    public Task WaitForCloseWindowsAsync(int count) => closes.WaitForAsync(count);
}
