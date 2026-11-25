using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class PointerSendCheckTests
{
    private const nint Owner = 0x10;
    private const nint Own = 0x20;
    private const nint Child = 0x21;
    private const nint Other = 0x99;
    private static readonly Win32.Point Point = new() { X = 123, Y = 456 };
    private static readonly string Move = PointerTrace.Label(2, "away");

    private static bool IsChild(nint window, nint parent) => parent == Own && window == Child;

    private static void Verify(bool onScreen = true, nint? hit = null, nint target = Own, nint owner = Owner, nint foreground = Owner) =>
        PointerSendCheck.Verify(Move, Point, onScreen, target, hit ?? Own, IsChild, owner, foreground);

    [Fact]
    public void Allows_a_point_in_a_windows_own_element_while_that_window_is_in_front()
    {
        Verify(target: Own, owner: Own, foreground: Own);
    }

    [Fact]
    public void Allows_a_point_in_a_child_window_of_the_elements_window()
    {
        Verify(hit: Child);
    }

    [Fact]
    public void Allows_an_element_in_a_popup_while_the_window_that_owns_it_is_in_front()
    {
        Verify(target: Own, owner: Owner, foreground: Owner);
    }

    [Fact]
    public void Refuses_a_point_off_screen_naming_the_move_and_the_point()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Verify(onScreen: false));

        Assert.Contains(Move, refusal.Message);
        Assert.Contains("123", refusal.Message);
        Assert.Contains("456", refusal.Message);
    }

    [Fact]
    public void Refuses_a_point_another_window_covers_naming_the_move()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Verify(hit: Other));

        Assert.Contains(Move, refusal.Message);
    }

    [Fact]
    public void Refuses_a_point_no_window_covers()
    {
        Assert.Throws<InvalidOperationException>(() => Verify(hit: 0));
    }

    [Fact]
    public void Refuses_a_child_of_another_window()
    {
        Assert.Throws<InvalidOperationException>(() => Verify(hit: Child, target: Other));
    }

    [Theory]
    [InlineData(0x99)]
    [InlineData(0)]
    public void Refuses_when_the_callers_window_is_not_in_front(int foreground)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Verify(foreground: foreground));

        Assert.Contains(Move, refusal.Message);
    }

    [Fact]
    public void Refuses_when_only_the_elements_own_window_is_in_front_and_it_is_not_the_callers()
    {
        Assert.Throws<InvalidOperationException>(() => Verify(target: Own, owner: Owner, foreground: Own));
    }
}