using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// How a test reads the update card above Configure's sections: the box that holds the "Current version" line, the texts,
/// buttons, and check box in it, and its edge and background. The state is the shared fake of the update status.
/// </summary>
internal sealed partial class ConfigureScreen
{
    private const string CurrentVersionStart = "Current version";

    /// <summary>The shared fake of the update status the window shows; set its state before <see cref="Open"/>, or publish while open.</summary>
    public FakeUpdateStatus Updates => services.Updates;

    /// <summary>The state changes while the window is open, as when a check ends.</summary>
    public void PublishUpdate(UpdateStage stage, string? newVersion, string? failure = null)
    {
        Updates.Publish(new UpdateState(stage, "2.1.0", newVersion, failure));
        Settle();
    }

    /// <summary>The box that holds the update card's lines: the nearest border around the "Current version" line.</summary>
    public Border Card
    {
        get
        {
            Settle();
            DependencyObject element = CardBlocks(Window).First(block => OpenWindows.TextOf(block).StartsWith(CurrentVersionStart, StringComparison.Ordinal));
            while (element is not Border)
            {
                element = LogicalTreeHelper.GetParent(element) ?? throw new InvalidOperationException("The card's lines sit in no box.");
            }

            return (Border)element;
        }
    }

    /// <summary>The texts the card shows, in text blocks, top to bottom.</summary>
    public IReadOnlyList<string> CardTexts =>
        [.. CardBlocks(Card).Where(block => block.IsVisible).Select(OpenWindows.TextOf).Where(text => text.Length > 0)];

    /// <summary>The visible text block of the card that starts with the words.</summary>
    public TextBlock CardBlock(string start) =>
        CardBlocks(Card).Single(block => block.IsVisible && OpenWindows.TextOf(block).StartsWith(start, StringComparison.Ordinal));

    /// <summary>The part of the card's text block that is bold.</summary>
    public static string BoldPart(TextBlock block) =>
        string.Concat(Leaves(block.Inlines).Where(run => run.FontWeight == FontWeights.Bold).Select(run => run.Text));

    /// <summary>The color the text block paints its first part with.</summary>
    public static Color ColorOf(TextBlock block)
    {
        var first = Leaves(block.Inlines).FirstOrDefault();
        return ((SolidColorBrush)(first?.Foreground ?? block.Foreground)).Color;
    }

    /// <summary>The button of the card with the text, or null when the card has none; it may be hidden.</summary>
    public Button? CardButton(string text) =>
        OpenWindows.Descendants(Card).OfType<Button>().SingleOrDefault(button => button.Content as string == text);

    /// <summary>Whether the card shows a button with the text.</summary>
    public bool CardShowsButton(string text) => CardButton(text) is { IsVisible: true };

    /// <summary>The Auto update check box in the card.</summary>
    public CheckBox AutoUpdateBox =>
        OpenWindows.Descendants(Card).OfType<CheckBox>().Single(box => box.Content as string == "Auto update");

    /// <summary>Switches the Auto update check box as a click does.</summary>
    public void SwitchAutoUpdate()
    {
        OpenWindows.Click(AutoUpdateBox);
        Settle();
    }

    /// <summary>The color of a brush of the window's theme, such as SystemFillColorCautionBackgroundBrush.</summary>
    public Color ThemeColor(string brushKey) => ((SolidColorBrush)Window.FindResource(brushKey)).Color;

    /// <summary>The card's background color, or null when it has none.</summary>
    public Color? CardBackground => Card.Background is SolidColorBrush { Color.A: > 0 } brush ? brush.Color : null;

    /// <summary>The window's y of the card's bottom edge.</summary>
    public double CardBottom => Card.TranslatePoint(new Point(0, Card.ActualHeight), Window).Y;

    /// <summary>The window's y of the title's top edge.</summary>
    public double TitleTop(string heading) => Title(heading).TranslatePoint(new Point(0, 0), Window).Y;

    private static IEnumerable<TextBlock> CardBlocks(DependencyObject root) => OpenWindows.Descendants(root).OfType<TextBlock>();

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
