using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Shell.Tests;

[Unit]
public class TrayAppStopTests
{
    [Fact]
    public void Stop_removes_the_tray_icon()
    {
        var app = new ShellHarness();
        app.Start();

        app.Lifetime.Stop();

        Assert.False(app.TrayIcon.IsShown);
    }
}
