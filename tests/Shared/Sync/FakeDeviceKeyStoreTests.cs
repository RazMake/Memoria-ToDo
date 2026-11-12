using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>The shared fake key storage promises one identical key to every store, even when tests run in parallel.</summary>
[Unit]
public sealed class FakeDeviceKeyStoreTests
{
    private const int Stores = 12;

    [Fact]
    public async Task Gives_every_store_the_same_key_when_stores_are_first_opened_at_the_same_time()
    {
        var barrier = new Barrier(Stores);
        var results = new TaskCompletionSource<byte[]>[Stores];
        var threads = new Thread[Stores];
        for (var i = 0; i < Stores; i++)
        {
            var store = new FakeDeviceKeyStore();
            store.SeedKey();
            var result = results[i] = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            threads[i] = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    byte[] blob;
                    using (var handle = store.Open())
                        blob = handle.PublicKeyBlob.ToArray();
                    result.SetResult(blob);
                }
                catch (Exception error)
                {
                    result.SetException(error);
                }
            })
            { IsBackground = true };
            threads[i].Start();
        }

        var all = Task.WhenAll(results.Select(r => r.Task));
        byte[][] blobs;
        try
        {
            blobs = await all.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            barrier.Dispose();
        }

        Assert.All(blobs, blob => Assert.Equal(blobs[0], blob));
    }
}
