using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The look of a popup's frame: the theme's surface and outline.</summary>
internal static class PopupFrame
{
    private const string Surface = "SolidBackgroundFillColorQuarternaryBrush";
    private const string Outline = "SurfaceStrokeColorDefaultBrush";

    public static Border Make()
    {
        var frame = new Border { BorderThickness = new System.Windows.Thickness(1), Padding = new System.Windows.Thickness(4), CornerRadius = new System.Windows.CornerRadius(4) };
        frame.SetResourceReference(Border.BackgroundProperty, Surface);
        frame.SetResourceReference(Border.BorderBrushProperty, Outline);
        return frame;
    }
}
