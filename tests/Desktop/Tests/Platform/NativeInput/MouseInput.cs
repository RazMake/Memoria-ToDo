using System.Windows;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Sends real mouse button presses and releases through Windows.</summary>
[RealInput]
internal static class MouseInput
{
    public static Win32.Point Press(Point screen)
    {
        var pixel = new Win32.Point { X = (int)Math.Round(screen.X), Y = (int)Math.Round(screen.Y) };
        NativeInput.Send(NativeInput.Move(pixel), NativeInput.LeftButton(down: true));
        return pixel;
    }

    public static void Release() => NativeInput.Send(NativeInput.LeftButton(down: false));
}
