using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>What EscCloses rejects before it touches a window. It needs no window, so it is a unit test.</summary>
[Unit]
public class EscClosesTests
{
    [Fact]
    public void Attach_rejects_a_missing_window()
    {
        Assert.Throws<ArgumentNullException>(() => EscCloses.Attach(null!));
    }
}
