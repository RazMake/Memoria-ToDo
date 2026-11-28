namespace Memoria.ToDo.Shared.Sync;

/// <summary>A cached immutable value that raises <see cref="Changed"/> after each update, outside its lock.</summary>
internal sealed class Published<T>(T initial) where T : class
{
    private readonly object gate = new();
    private T current = initial;

    public event EventHandler? Changed;

    public T Current
    {
        get { lock (gate) { return current; } }
    }

    public void Update(Func<T, T> change)
    {
        lock (gate)
        {
            current = change(current);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
