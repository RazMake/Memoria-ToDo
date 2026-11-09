using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What a field looks like on screen, measured from its pixels, so a test sees styling without knowing which text control
/// the field uses. "Ink" is any pixel that differs from the field's background, which is the pixel in its bottom-right
/// corner just inside the border; the field's border is left out. Shared test setup.
/// </summary>
internal sealed class RenderedField
{
    private const int Inset = 3;
    private const int InkThreshold = 60;
    private const int ColorTolerance = 20;
    private const int MinColorPixels = 5;

    private readonly byte[] pixels;
    private readonly int width;
    private readonly int height;
    private readonly (byte B, byte G, byte R) background;

    private RenderedField(FrameworkElement field)
    {
        field.UpdateLayout();
        width = (int)Math.Ceiling(field.ActualWidth);
        height = (int)Math.Ceiling(field.ActualHeight);
        // A DrawingVisual puts the field's own top-left corner at the picture's: rendering the field directly keeps its offset in the window.
        // The white underneath is what a field with no background of its own shows, as when the test window has no theme.
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(field), null, new Rect(0, 0, width, height));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var corner = ((height - 1 - Inset) * width + width - 1 - Inset) * 4;
        background = (pixels[corner], pixels[corner + 1], pixels[corner + 2]);
    }

    /// <summary>Completes once the dispatcher has run everything queued before it, which includes styling that follows typing.</summary>
    public static Task Settled() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher
            .InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle).Task;

    /// <summary>Takes a picture of the field as it shows now.</summary>
    public static RenderedField Of(FrameworkElement field) => new(field);

    /// <summary>The picture of the named field in the window.</summary>
    public static RenderedField Of(DependencyObject root, string fieldName) => Of(EditorFields.Field(root, fieldName));

    /// <summary>How much text there is to see: the summed difference of every pixel from the background. Dimmer text gives less.</summary>
    public long TotalInk()
    {
        long total = 0;
        ForEachInside((_, _, i) => total += Difference(i));
        return total;
    }

    /// <summary>The height in pixels from the highest to the lowest row that holds text.</summary>
    public int InkHeight()
    {
        var rows = InkPixels().Select(p => p.Y).ToList();
        return rows.Count == 0 ? 0 : rows.Max() - rows.Min() + 1;
    }

    /// <summary>How far the top half of the text lies to the right of its bottom half, in pixels. Slanted text gives more.</summary>
    public double Slant()
    {
        var ink = InkPixels();
        var middle = (ink.Min(p => p.Y) + ink.Max(p => p.Y)) / 2.0;
        return ink.Where(p => p.Y < middle).Average(p => p.X) - ink.Where(p => p.Y >= middle).Average(p => p.X);
    }

    /// <summary>Whether some text is drawn in the color, such as <c>#d1242f</c>, give or take anti-aliasing.</summary>
    public bool ShowsColor(string hex)
    {
        var (r, g, b) = (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));
        var count = 0;
        ForEachInside((_, _, i) =>
        {
            if (Math.Abs(pixels[i + 2] - r) <= ColorTolerance
                && Math.Abs(pixels[i + 1] - g) <= ColorTolerance
                && Math.Abs(pixels[i] - b) <= ColorTolerance)
            {
                count++;
            }
        });
        return count >= MinColorPixels;
    }

    private List<(int X, int Y)> InkPixels()
    {
        var ink = new List<(int X, int Y)>();
        ForEachInside((x, y, i) =>
        {
            if (Difference(i) > InkThreshold)
            {
                ink.Add((x, y));
            }
        });
        return ink;
    }

    private int Difference(int i) =>
        (Math.Abs(pixels[i] - background.B) + Math.Abs(pixels[i + 1] - background.G) + Math.Abs(pixels[i + 2] - background.R)) / 3;

    private void ForEachInside(Action<int, int, int> visit)
    {
        for (var y = Inset; y < height - Inset; y++)
        {
            for (var x = Inset; x < width - Inset; x++)
            {
                visit(x, y, (y * width + x) * 4);
            }
        }
    }
}
