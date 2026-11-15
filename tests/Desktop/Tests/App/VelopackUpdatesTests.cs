using System.Reflection;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Testing;
using Velopack;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The Velopack adapter, through <see cref="IAppInstall"/>. This test process isn't installed by Velopack, so it behaves as
/// the app run from a build folder: it never reaches the network.
/// </summary>
[Integration]
public class VelopackUpdatesTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static readonly NewerVersion Newer = new("9.9.9", [new ReleaseNotes("9.9.9", "Notes.")]);

    private static IAppInstall NotInstalled()
    {
        // Prepare the process as the composition root does (Program.Main); Velopack sets its process-wide locator here.
        VelopackApp.Build().Run();
        return new VelopackUpdates("https://github.com/RazMake/Memoria-ToDo");
    }

    [Fact]
    public void Is_not_installed_when_run_from_a_build_folder()
    {
        Assert.False(NotInstalled().IsInstalled);
    }

    [Fact]
    public void Gives_the_version_the_app_was_built_with_when_run_from_a_build_folder()
    {
        var built = typeof(VelopackUpdates).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

        Assert.Equal(built, NotInstalled().Version);
    }

    [Fact]
    public async Task Finds_no_newer_version_and_completes_when_the_app_is_not_installed()
    {
        // An update check over the network would take longer than a check of the local install; the limit only bounds a failure.
        var found = await NotInstalled().CheckAsync(CancellationToken.None).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    [Fact]
    public async Task Downloads_nothing_and_completes_when_the_app_is_not_installed()
    {
        await NotInstalled().DownloadAsync(Newer, CancellationToken.None).WaitAsync(Limit, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Starts_no_updater_and_does_not_fail_when_the_app_is_not_installed()
    {
        var install = NotInstalled();

        install.ApplyAfterExit(Newer, quiet: true);
        install.ApplyAfterExit(Newer, quiet: false);
    }
}
