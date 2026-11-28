namespace Memoria.ToDo.Shared.Sync;

/// <summary>Collects the completion tasks of transport commands so the coordinator can await them.</summary>
internal sealed class TransportCleanup : ITransportCleanup
{
    private readonly object gate = new();
    private readonly List<Task> pending = [];

    public void Add(Task completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        lock (gate)
        {
            pending.Add(completion);
        }
    }

    /// <summary>Completes when every added task, including tasks added while waiting, has ended.</summary>
    public async Task WhenAllAsync()
    {
        while (true)
        {
            Task[] batch;
            lock (gate)
            {
                pending.RemoveAll(t => t.IsCompleted);
                if (pending.Count == 0)
                {
                    return;
                }

                batch = [.. pending];
            }

            // Only the ending matters here: a failure already surfaced through the pass that owned the command.
            await Task.WhenAny(Task.WhenAll(batch)).ConfigureAwait(false);
        }
    }
}
