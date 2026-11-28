namespace Memoria.ToDo.Shared.Sync;

/// <summary>Turns an operational exception into the failure kind and details a status shows.</summary>
internal static class FailureReport
{
    /// <summary>Git failures keep their kind, local storage failures are <see cref="SyncFailure.LocalStorage"/>, anything else is unexpected.</summary>
    public static (SyncFailure Kind, string Details) Of(Exception ex) => ex switch
    {
        GitFailureException git => (git.Kind, git.Message),
        _ when IsLocalStorage(ex) => (SyncFailure.LocalStorage, Describe(ex)),
        _ => (SyncFailure.Unexpected, Describe(ex)),
    };

    /// <summary>Whether the exception means this device's own storage couldn't be read or written, as <see cref="Of"/> classifies it.</summary>
    public static bool IsLocalStorage(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException;

    /// <summary>Whether <see cref="OfLocal"/> reports the exception as local storage: <see cref="IsLocalStorage"/> plus unusable saved content.</summary>
    public static bool IsLocal(Exception ex) => ex is InvalidDataException || IsLocalStorage(ex);

    public static string Describe(Exception ex) => string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;

    /// <summary>Like <see cref="Of"/>, for work on the device's own files, where unusable saved content is local storage trouble.</summary>
    public static (SyncFailure Kind, string Details) OfLocal(Exception ex) =>
        IsLocal(ex) ? (SyncFailure.LocalStorage, Describe(ex)) : Of(ex);

    /// <summary>The <see cref="OfLocal"/> classification as a Git failure that keeps the exception as its cause.</summary>
    public static GitFailureException ToLocalFailure(Exception ex)
    {
        var (kind, details) = OfLocal(ex);
        return new GitFailureException(kind, details, ex);
    }
}
