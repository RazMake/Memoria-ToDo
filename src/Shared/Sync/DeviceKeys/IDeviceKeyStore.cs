namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// This device's secure RSA key storage, implemented by the app. These synchronous-only local operations
/// run on Sync's worker, never on a UI thread. No method exports private-key material.
/// Failures propagate rather than create a replacement key.
/// </summary>
public interface IDeviceKeyStore
{
    /// <summary>Whether a stored key exists, not whether it can be opened.</summary>
    bool HasKey { get; }

    /// <summary>Creates the designed device key. Throws InvalidOperationException if one already exists.</summary>
    IDeviceSigningKey Generate();

    /// <summary>Opens the existing key. A missing, unreadable, or unusable key fails without replacement.</summary>
    IDeviceSigningKey Open();
}
