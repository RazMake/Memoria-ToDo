using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The PC's mouse as a drag pointer. Only what's checked before the mouse is touched can be tested: no test moves the real mouse or
/// starts a real drag, so every case here passes a missing argument that is rejected first.
/// </summary>
[Unit]
public class MousePointerTests
{
    [WpfFact]
    public void Rejects_a_missing_element_when_asked_for_the_position()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new MousePointer().PositionIn(null!));

        Assert.Equal("element", error.ParamName);
    }

    [WpfFact]
    public void Rejects_a_missing_source_when_starting_a_drag()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new MousePointer().StartDrag(null!, new DataObject()));

        Assert.Equal("source", error.ParamName);
    }
}
