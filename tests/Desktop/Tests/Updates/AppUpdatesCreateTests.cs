using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>What Create refuses, and that it checks nothing yet.</summary>
[Unit]
public class AppUpdatesCreateTests
{
    private const string DeviceFolder = @"C:\device";

    // Each case omits one required dependency.
    private static IAppUpdates CreateWithNull(int argument) => AppUpdates.Create(
        argument == 0 ? null! : new FakeAppInstall(),
        argument == 1 ? null! : new FakeFileSystem(),
        argument == 2 ? null! : DeviceFolder,
        argument == 3 ? null! : new FakeTimeProvider(),
        argument == 4 ? null! : new FakeTheme(),
        argument == 5 ? null! : () => new FakeDescriptionWebView(),
        argument == 6 ? null! : new FakeDefaultBrowser(),
        argument == 7 ? null! : new CloseWindowKeys(),
        argument == 9 ? null! : new FakeDragPointer(),
        argument == 8 ? null! : Logger.None);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Rejects_a_null_argument(int argument) =>
        Assert.ThrowsAny<ArgumentException>(() => CreateWithNull(argument));

    [Fact]
    public void Rejects_a_device_folder_that_is_not_a_full_path() =>
        Assert.ThrowsAny<ArgumentException>(() => AppUpdates.Create(
            new FakeAppInstall(), new FakeFileSystem(), @"device\folder", new FakeTimeProvider(), new FakeTheme(),
            () => new FakeDescriptionWebView(), new FakeDefaultBrowser(), new CloseWindowKeys(), new FakeDragPointer(), Logger.None));

    [Fact]
    public void Checks_nothing_until_it_starts()
    {
        var install = new FakeAppInstall();

        AppUpdates.Create(install, new FakeFileSystem(), DeviceFolder, new FakeTimeProvider(), new FakeTheme(),
            () => new FakeDescriptionWebView(), new FakeDefaultBrowser(), new CloseWindowKeys(), new FakeDragPointer(), Logger.None);

        Assert.Equal(0, install.CheckCount);
    }
}
