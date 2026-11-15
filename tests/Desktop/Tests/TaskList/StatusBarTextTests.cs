using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>The words the status bar shows: the Sync button's tooltip and the app's version.</summary>
[Unit]
public class StatusBarTextTests
{
    [Fact]
    public void The_Sync_tooltip_names_the_F5_key_while_no_sync_runs()
    {
        Assert.Equal("Sync (F5)", StatusBarText.SyncTooltip(working: false));
    }

    [Fact]
    public void The_Sync_tooltip_says_Syncing_while_a_sync_runs()
    {
        Assert.Equal("Syncing…", StatusBarText.SyncTooltip(working: true));
    }

    [Fact]
    public void The_version_is_the_word_Version_and_the_apps_version()
    {
        Assert.Equal("Version " + AppVersion.Value, StatusBarText.Version);
    }
}
