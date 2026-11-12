namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// The immutable endpoint and key observed during an SSH connection, including trusted custom-host keys
/// that setup can forget.
/// </summary>
public interface ISshHostKey
{
    string Host { get; }
    int Port { get; }
    string Algorithm { get; }

    /// <summary>SHA256: followed by the unpadded base64 SHA-256 digest of the SSH public-key blob.</summary>
    string Fingerprint { get; }

    /// <summary>True only for a previously unknown custom-host key, not a built-in or changed trusted key.</summary>
    bool CanAccept { get; }
}
