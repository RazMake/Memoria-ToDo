using Xunit;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Testing;
using Velopack;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The Velopack adapter, through <see cref="IAppUpdates"/>. This test process isn't installed by Velopack.</summary>
[Integration]
public class VelopackUpdatesTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Does_nothing_and_completes_when_the_app_is_not_installed()
    {
        // Prepare the process as the composition root does (Program.Main); Velopack sets its process-wide locator here.
        VelopackApp.Build().Run();
        IAppUpdates updates = new VelopackUpdates("https://github.com/RazMake/Memoria-ToDo");

        // An update check over the network would take longer than a check of the local install; the limit only bounds a failure.
        await updates.DownloadNewerVersionAsync(CancellationToken.None).WaitAsync(Limit, TestContext.Current.CancellationToken);
    }
}
