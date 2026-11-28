namespace Memoria.ToDo.Desktop.Updates;

/// <summary>The installed copy and the releases it can update to.</summary>
public interface IAppInstall
{
    /// <summary>The running version, such as "2.1.0": Velopack's when installed, otherwise the build's.</summary>
    string Version { get; }

    /// <summary>False when the app runs from a build folder.</summary>
    bool IsInstalled { get; }

    /// <summary>Checks GitHub Releases. Null when there's no newer version. Fails when the check fails.</summary>
    Task<NewerVersion?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Downloads a version the latest check found. Fails when the download fails or the check didn't find it.</summary>
    Task DownloadAsync(NewerVersion version, CancellationToken cancellationToken);

    /// <summary>
    /// Starts Velopack's updater, which waits for the app to end, applies the downloaded version, and starts
    /// the app again: as at sign-in and with no window when <paramref name="quiet"/>, otherwise as a start by hand.
    /// </summary>
    void ApplyAfterExit(NewerVersion version, bool quiet);
}
