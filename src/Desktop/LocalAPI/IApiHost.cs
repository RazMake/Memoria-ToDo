namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The web server behind the lifetime, which unit tests replace with a fake: the real one runs Kestrel on the named pipe and only
/// integration tests start it. Stub for the red run; the coder writes the Kestrel implementation.
/// </summary>
internal interface IApiHost
{
    /// <summary>Binds the pipe and starts serving; completes once it listens. Throws when it can't bind, such as when the name is taken.</summary>
    Task StartAsync();

    /// <summary>
    /// Stops accepting connections, ends open streams, and waits for running calls until <paramref name="giveUp"/> is cancelled, then
    /// throws <see cref="OperationCanceledException"/>, as Kestrel's stop does.
    /// </summary>
    Task StopAsync(CancellationToken giveUp);
}
