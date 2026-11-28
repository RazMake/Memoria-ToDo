namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Gives one Git operation an open signing handle and a cleanup collector. When the operation ends, however it ends,
/// it awaits the collector and only then disposes the handle, on the worker.
/// </summary>
internal sealed class GitSession(OwnedWorker worker, IDeviceKeyStore keys, DeviceMetadata metadata, HostTrust trust, SetupModel setup)
{
    public async Task<T> RunAsync<T>(Func<GitConnection, Task<T>> operation)
    {
        var name = await worker.Run(metadata.LoadName).ConfigureAwait(false);
        var key = await worker.Run(() =>
        {
            var opened = keys.Open();
            setup.Observe(opened);
            return opened;
        }).ConfigureAwait(false);
        var cleanup = new TransportCleanup();
        try
        {
            var connection = new GitConnection(key, name, trust.Verify, cleanup);
            var running = await worker.Run(() => operation(connection)).ConfigureAwait(false);
            return await running.ConfigureAwait(false);
        }
        finally
        {
            await cleanup.WhenAllAsync().ConfigureAwait(false);
            await worker.Run(key.Dispose).ConfigureAwait(false);
        }
    }
}
