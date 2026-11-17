namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// The immutable endpoint and key observed during an SSH connection.
/// </summary>
public interface ISshHostKey
{
    string Host { get; }
    int Port { get; }
    string Algorithm { get; }

    /// <summary>SHA256: followed by the unpadded base64 SHA-256 digest of the SSH public-key blob.</summary>
    string Fingerprint { get; }

    /// <summary>True only for a custom host's unknown or changed key, and only while the saved trust loads; never for a built-in host.</summary>
    bool CanAccept { get; }
}
