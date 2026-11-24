namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// SyncNowAsync can't start a pass: no repository is selected, the device has no key yet,
/// or the selected repository contains the device folder. The message says which.
/// </summary>
public sealed class SyncNotReadyException : InvalidOperationException
{
    public SyncNotReadyException(string message) : base(message)
    {
    }
}
