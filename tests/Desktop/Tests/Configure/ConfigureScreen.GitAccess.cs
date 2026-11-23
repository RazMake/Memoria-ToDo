using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// How a test finds the Git Access and Repository controls: buttons and text blocks by their text, the card that holds
/// an element, and where an element sits in the window. Shared test setup.
/// </summary>
internal sealed partial class ConfigureScreen
{
    /// <summary>Every button in the window, shown or not.</summary>
    public IReadOnlyList<Button> Buttons => [.. OpenWindows.Descendants(Window).OfType<Button>()];

    /// <summary>The one button with the text, shown or not.</summary>
    public Button ButtonNamed(string text) => Buttons.Single(button => button.Content as string == text);

    /// <summary>Whether a button with the text exists and shows.</summary>
    public bool ShowsButton(string text) => Buttons.Any(button => button.Content as string == text && button.IsVisible);

    /// <summary>The one text block with exactly the text, such as a card's title or a message.</summary>
    public TextBlock TitleBlock(string text) =>
        OpenWindows.Descendants(Window).OfType<TextBlock>().Single(block => OpenWindows.TextOf(block) == text);

    /// <summary>Whether the element sits inside the card titled <paramref name="cardTitle"/>, the box around its title.</summary>
    public bool IsInCard(DependencyObject element, string cardTitle)
    {
        DependencyObject card = TitleBlock(cardTitle);
        while (card is not Border)
        {
            card = LogicalTreeHelper.GetParent(card) ?? throw new InvalidOperationException($"The title '{cardTitle}' sits in no box.");
        }

        for (var up = element; up is not null; up = LogicalTreeHelper.GetParent(up))
        {
            if (ReferenceEquals(up, card))
            {
                return true;
            }
        }

        return false;
    }
}
