namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// A disposable signing handle, including a non-exportable Android key. Sync owns its lifetime.
/// Disposal releases the handle, never deletes the stored device key.
/// </summary>
public interface IDeviceSigningKey : IDisposable
{
    /// <summary>The immutable SSH wire-format public-key blob, not private-key data or a PEM document.</summary>
    ReadOnlyMemory<byte> PublicKeyBlob { get; }

    /// <summary>
    /// Signs the supplied data with RSA PKCS#1 v1.5 and the selected SHA-2 hash.
    /// Returns only the raw RSA signature, without an SSH wrapper. Runs on Sync's worker.
    /// </summary>
    byte[] Sign(ReadOnlySpan<byte> data, RsaSignatureAlgorithm algorithm);
}

public enum RsaSignatureAlgorithm
{
    Sha256,
    Sha512,
}
