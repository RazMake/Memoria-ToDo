namespace Memoria.ToDo.Shared.Sync;

/// <summary>Turns an operational exception into the failure kind and details a status shows.</summary>
internal static class FailureReport
{
    /// <summary>Git failures keep their kind, local storage failures are <see cref="SyncFailure.LocalStorage"/>, anything else is unexpected.</summary>
    public static (SyncFailure Kind, string Details) Of(Exception ex) => ex switch
    {
        GitFailureException git => (git.Kind, git.Message),
        IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException => (SyncFailure.LocalStorage, Describe(ex)),
        _ => (SyncFailure.Unexpected, Describe(ex)),
    };

    public static string Describe(Exception ex) => string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;

    /// <summary>Like <see cref="Of"/>, for work on the device's own files, where unusable saved content is local storage trouble.</summary>
    public static (SyncFailure Kind, string Details) OfLocal(Exception ex) =>
        ex is InvalidDataException ? (SyncFailure.LocalStorage, Describe(ex)) : Of(ex);
}
