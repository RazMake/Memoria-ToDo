namespace Memoria.ToDo.Android.Platform;

/// <summary>
/// What one kind of screen needs. Android makes each screen itself and passes it nothing, so the screen's module
/// sets this from its create method, before Android can make the screen, and the screen reads it.
/// </summary>
public sealed class ScreenServices<T>
    where T : class
{
    // Set runs in Application.OnCreate and Get from activities, both on the main thread.
    private T? _services;

    /// <summary>
    /// Sets what the screen needs, replacing what an earlier call set.
    /// Throws <see cref="ArgumentNullException"/> when <paramref name="services"/> is null.
    /// </summary>
    public void Set(T services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>
    /// What the screen needs. Throws <see cref="InvalidOperationException"/>, naming <typeparamref name="T"/>,
    /// when nothing was set.
    /// </summary>
    public T Get() =>
        _services ?? throw new InvalidOperationException($"{typeof(T).Name} was needed before its module's create method set it.");
}
