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
            null!));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }
}
