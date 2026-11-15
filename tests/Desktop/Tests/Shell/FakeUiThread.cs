namespace Memoria.ToDo.Desktop.Shell.Tests;

/// <summary>
/// A UI thread the test drives: work runs inline while <see cref="IsCurrent"/> is true, as when the caller already is
/// the UI thread; otherwise it waits in a queue until <see cref="RunQueued"/>, as work posted from another thread.
/// </summary>
internal sealed class FakeUiThread : IUiThread
{
    private readonly Queue<Action> queued = new();

    public bool IsCurrent { get; set; } = true;

    public void Post(Action work) => queued.Enqueue(work);

    public int QueuedCount => queued.Count;

    /// <summary>Runs, in order, the work posted so far, as the real UI thread does when it gets to it.</summary>
    public void RunQueued()
    {
        while (queued.TryDequeue(out var work))
        {
            work();
        }
    }
}
