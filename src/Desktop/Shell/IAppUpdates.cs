namespace Memoria.ToDo.Desktop.Shell;

/// <summary>App updates, implemented by the composition root.</summary>
public interface IAppUpdates
{
    /// <summary>
    /// Downloads a newer version, if there is one, for the next start to apply. Does nothing when the app isn't
    /// installed. Fails when the check or download fails.
    /// </summary>
    Task DownloadNewerVersionAsync(CancellationToken cancellationToken);
}
