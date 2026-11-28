using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests.StatusBar;

/// <summary>
/// An <see cref="UpdateStatusBar"/> in a showing window with the Fluent theme, on the shared fake of the update status,
/// read as a user sees it. Shared test setup, not a fake of a port. Dispose it to close the window.
/// </summary>
internal sealed class StatusBarScreen : IDisposable
{
    private readonly OpenWindows windows = new();

    public StatusBarScreen(UIElement? start = null, bool dark = false)
    {
        Bar = new UpdateStatusBar(Status, start);
        Window = new Window { Width = 500, Height = 200, Content = new Grid { Children = { Bar } } };
        OpenWindows.UseTheme(Window, dark);
        Window.Show();
        Window.UpdateLayout();
    }

    public FakeUpdateStatus Status { get; } = new();

    public UpdateStatusBar Bar { get; }

    public Window Window { get; }

    /// <summary>The state changes while the window is open, as when a check ends.</summary>
    public void Publish(UpdateStage stage, string? newVersion)
    {
        Status.Publish(new UpdateState(stage, "2.1.0", newVersion, null));
        Window.UpdateLayout();
    }

    /// <summary>The text blocks in the bar, as text; the update text is one of them.</summary>
    public IReadOnlyList<string> Texts =>
        [.. OpenWindows.Descendants(Bar).OfType<TextBlock>().Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    /// <summary>The text block of the update, such as "Update available: 2.2.0" or "Updating to 2.2.0".</summary>
    public TextBlock UpdateText =>
        OpenWindows.Descendants(Bar).OfType<TextBlock>().Single(b => OpenWindows.TextOf(b).StartsWith("Updat", StringComparison.Ordinal));

    /// <summary>The buttons in the bar.</summary>
    public IReadOnlyList<Button> Buttons => [.. OpenWindows.Descendants(Bar).OfType<Button>()];

    /// <summary>The button that is the update text: the one that holds it.</summary>
    public Button UpdateButton => Buttons.Single(b => OpenWindows.Descendants(b).Contains(UpdateText) || b.Content == UpdateText);

    /// <summary>The part of the update text that is bold.</summary>
    public string BoldText => string.Concat(Leaves(UpdateText.Inlines).Where(run => run.FontWeight == FontWeights.Bold).Select(run => run.Text));

    /// <summary>The color of the update text's first part.</summary>
    public Color TextColor => ((SolidColorBrush)Leaves(UpdateText.Inlines).First().Foreground).Color;

    /// <summary>The color of a brush of the window's theme, such as SystemFillColorCautionBrush.</summary>
    public Color ThemeColor(string brushKey) => ((SolidColorBrush)Window.FindResource(brushKey)).Color;

    /// <summary>The window's x of the element's left edge.</summary>
    public double LeftOf(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Window).X;

    /// <summary>The window's x of the element's right edge.</summary>
    public double RightOf(FrameworkElement element) => element.TranslatePoint(new Point(element.ActualWidth, 0), Window).X;

    /// <summary>Gives the keyboard focus to the element, as Tab does.</summary>
    public void Focus(UIElement element)
    {
        Window.Activate();
        Keyboard.Focus(element);
        Assert.True(element.IsKeyboardFocused, "The window couldn't give the element the keyboard focus.");
    }

    public void Dispose() => windows.Dispose();

    private static IEnumerable<Run> Leaves(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run)
            {
                yield return run;
            }
            else if (inline is Span span)
            {
                foreach (var leaf in Leaves(span.Inlines))
                {
                    yield return leaf;
                }
            }
        }
    }
}
