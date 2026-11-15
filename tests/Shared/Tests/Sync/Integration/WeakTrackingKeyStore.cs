using System.Runtime.CompilerServices;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// A key store that hands Sync a fresh wrapper for every opened key and remembers each wrapper only weakly, so a test can
/// ask whether Sync let go of everything that held the key once it had stopped. Keeps no strong reference of its own.
/// </summary>
internal sealed class WeakTrackingKeyStore(IDeviceKeyStore inner) : IDeviceKeyStore
{
    private readonly List<WeakReference> opened = [];

    public bool HasKey => inner.HasKey;

    public IDeviceSigningKey Generate() => inner.Generate();

    public IDeviceSigningKey Open()
    {
        var wrapper = new Wrapper(inner.Open());
        lock (opened)
            opened.Add(new WeakReference(wrapper));
        return wrapper;
    }

    /// <summary>
    /// Collects garbage once, finalizes, collects again, and returns how many opened keys are still reachable.
    /// Call it from a place that holds no key itself, after Sync has stopped.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int OpenedKeysStillReachable()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        lock (opened)
            return opened.Count(reference => reference.IsAlive);
    }

    private sealed class Wrapper(IDeviceSigningKey key) : IDeviceSigningKey
    {
        public ReadOnlyMemory<byte> PublicKeyBlob => key.PublicKeyBlob;

        public byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm) => key.Sign(data, algorithm);

        public void Dispose() => key.Dispose();
    }
}
