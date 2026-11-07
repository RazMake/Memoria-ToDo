using Memoria.ToDo.Desktop.Shell;
using Velopack;
using Velopack.Sources;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Downloads newer versions from the GitHub Releases of the repository, for Velopack to apply at the next start.</summary>
internal sealed class VelopackUpdates : IAppUpdates
{
    private readonly UpdateManager _manager;

    public VelopackUpdates(string repositoryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        _manager = new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: false));
    }

    public async Task DownloadNewerVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_manager.IsInstalled)
        {
            return;
        }

        // The check has no cancellation token, so a cancel stops the wait for it.
        var update = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (update is null)
        {
            return;
        }

        await _manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken).ConfigureAwait(false);
    }
}
