namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Configure's external setup actions, implemented by App and called on the UI thread.</summary>
public interface ISetupActions
{
    /// <summary>Copies the displayed public key, never private-key data. A clipboard failure propagates.</summary>
    void CopyPublicKey(string publicKey);

    /// <summary>Opens Sync's supplied add-key page in the default browser. A launch failure propagates.</summary>
    void OpenKeyPage(Uri address);
}
