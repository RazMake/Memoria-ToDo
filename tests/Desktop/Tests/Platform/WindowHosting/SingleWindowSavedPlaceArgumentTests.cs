using System.Windows;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>Single window's constructor that takes a saved place: its arguments, with functions that make no window, so none shows.</summary>
[Unit]
public class SingleWindowSavedPlaceArgumentTests
{
    private static SavedWindowPlacement Placement() =>
        new(new FakeFileSystem(), @"C:\Memoria\Device\place.json", "The window's place", Logger.None);

    [WpfFact]
    public void Refuses_a_null_function()
    {
        Assert.Throws<ArgumentNullException>(() => new SingleWindow(null!, Placement(), Logger.None));
    }

    [WpfFact]
    public void Refuses_a_null_saved_place()
    {
        Assert.Throws<ArgumentNullException>(() => new SingleWindow(() => new Window(), null!, Logger.None));
    }

    [WpfFact]
    public void Refuses_a_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new SingleWindow(() => new Window(), Placement(), null!));
    }

    [WpfFact]
    public void Makes_no_window_until_Show()
    {
        var made = 0;

        _ = new SingleWindow(() => { made++; return new Window(); }, Placement(), Logger.None);

        Assert.Equal(0, made);
    }
}
