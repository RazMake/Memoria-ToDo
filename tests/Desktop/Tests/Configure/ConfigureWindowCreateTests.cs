using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>What <see cref="ConfigureWindow.Create"/> requires: every part, the platform's Close window keys included.</summary>
[Unit]
public class ConfigureWindowCreateTests
{
    [Fact]
    public void Rejects_missing_Close_window_keys()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ConfigureWindow.Create(
            new FakeStartWithWindows(), new FakeSharedTaskSettings(), new FakeSyncService(), new FakeSetupActions(),
            null!, new FakeTheme(), new FakeUpdateStatus()));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }

    [Fact]
    public void Rejects_a_missing_theme()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ConfigureWindow.Create(
            new FakeStartWithWindows(), new FakeSharedTaskSettings(), new FakeSyncService(), new FakeSetupActions(),
            new CloseWindowKeys(), null!, new FakeUpdateStatus()));

        Assert.Equal("theme", failure.ParamName);
    }

    [Fact]
    public void Rejects_a_missing_update_status()
    {
        var failure = Assert.Throws<ArgumentNullException>(() => ConfigureWindow.Create(
            new FakeStartWithWindows(), new FakeSharedTaskSettings(), new FakeSyncService(), new FakeSetupActions(),
            new CloseWindowKeys(), new FakeTheme(), null!));

        Assert.Equal("updates", failure.ParamName);
    }
}
