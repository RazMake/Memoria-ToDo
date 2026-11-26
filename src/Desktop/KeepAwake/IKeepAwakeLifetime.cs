namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>What Shell uses.</summary>
public interface IKeepAwakeLifetime
{
    /// <summary>
    /// Reads the setting and the lock state, sets the request when the setting is on and the session unlocked, and follows locks
    /// from now on. Called once, on the UI thread. A setting it can't read, a lock state Windows can't give, or a request Windows
    /// refuses shows in <see cref="IKeepAwakeStatus.State"/>, never as a failure.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops following locks and clears the request. Repeated calls do nothing. Logs a failure at Error before throwing it,
    /// as every lifetime's cleanup does.
    /// </summary>
    void Stop();
}
