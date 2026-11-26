using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.KeepAwake.Tests;

/// <summary>Making Keep awake: it checks its arguments, and reads and asks nothing until it starts.</summary>
[Unit]
public class KeepAwakeCreateTests
{
    public static TheoryData<string> NullArguments => ["display", "session", "files", "deviceFolder", "logger"];

    [Theory]
    [MemberData(nameof(NullArguments))]
    public void Rejects_a_null_argument(string name)
    {
        var app = new KeepAwakeHarness();
        var logger = app.Log.CreateLogger();

        Assert.Throws<ArgumentNullException>(() => KeepAwake.Create(
            name == "display" ? null! : app.Display,
            name == "session" ? null! : app.Session,
            name == "files" ? null! : app.Files,
            name == "deviceFolder" ? null! : KeepAwakeHarness.DeviceFolder,
            name == "logger" ? null! : logger));
    }

    [Theory]
    [InlineData("")]
    [InlineData("device")]
    [InlineData(@"..\device")]
    public void Rejects_a_device_folder_that_is_not_fully_qualified(string folder)
    {
        var app = new KeepAwakeHarness();

        Assert.Throws<ArgumentException>(() => app.Create(folder));
    }

    [Fact]
    public void Reads_nothing_and_asks_Windows_nothing_until_it_starts()
    {
        var app = new KeepAwakeHarness();
        app.WriteSetting("on");
        app.Files.FailReadsUnder(KeepAwakeHarness.DeviceFolder);

        app.Create();

        Assert.Empty(app.Log.Events);
        Assert.Equal(0, app.Session.Reads);
        Assert.Empty(app.Display.SetCalls);
    }

    [Fact]
    public void Reports_off_before_it_starts()
    {
        var app = new KeepAwakeHarness();

        Assert.Equal(new KeepAwakeState(false, false, null), app.App.State);
    }
}
