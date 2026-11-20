using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Platform;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>
/// Shows a contact's card in a plain window, as the platform's hover card shows it, and drives it the way a user does: Ctrl goes down
/// and up as key events reaching the window, with Ctrl held in the thread's keyboard state while it is down. Shared test setup.
/// </summary>
internal sealed class ContactCardScreen : IDisposable
{
    private readonly OpenWindows _windows = new();

    /// <param name="services">The contacts and the theme.</param>
    /// <param name="id">The ID the mention's address names.</param>
    /// <param name="ctrlHeld">Whether Ctrl is already held when the card opens.</param>
    public ContactCardScreen(ContactServices services, string id, bool ctrlHeld = false)
    {
        void Open()
        {
            Card = ContactCard.For(services.Contacts, services.Theme, ContactAddress.Of(id))
                ?? throw new InvalidOperationException($"No card for {id}.");
            var window = new Window { Content = Card, SizeToContent = SizeToContent.WidthAndHeight };
            window.Show();
            ScreenSizeLimit.Lift(window);
            OpenWindows.UseTheme(window, dark: false);
        }

        if (ctrlHeld)
        {
            HeldModifier.While(ModifierKeys.Control, Open);
        }
        else
        {
            Open();
        }
    }

    public FrameworkElement Card { get; private set; } = null!;

    public Window Window => _windows.Visible().Single();

    /// <summary>The texts the card shows, top to bottom.</summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            Refresh();
            return [.. Visuals(Card).OfType<TextBlock>().Where(block => block.IsVisible).OrderBy(block => block.TranslatePoint(new Point(0, 0), Window).Y)
                .Select(OpenWindows.TextOf).Where(text => text.Length > 0)];
        }
    }

    /// <summary>The block that shows the text.</summary>
    public TextBlock Block(string text)
    {
        Refresh();
        return Visuals(Card).OfType<TextBlock>().Where(block => block.IsVisible).Single(block => OpenWindows.TextOf(block) == text);
    }

    /// <summary>The card's height, which grows and shrinks with its fields.</summary>
    public double Height
    {
        get
        {
            Refresh();
            return Card.ActualHeight;
        }
    }

    /// <summary>Whether anything on the card takes a click or the focus: a button, a link, or an element that can be focused.</summary>
    public bool TakesAClickOrTheFocus
    {
        get
        {
            Refresh();
            var all = new[] { Card }.Concat(Visuals(Card).OfType<FrameworkElement>()).ToList();
            return all.Any(element => element.Focusable || element is ButtonBase)
                || Visuals(Card).OfType<TextBlock>().Any(block => block.Inlines.OfType<Hyperlink>().Any());
        }
    }

    public Color ThemeColor(string brushKey) => ((SolidColorBrush)Window.FindResource(brushKey)).Color;

    public static Color ColorOf(TextBlock block) => ((SolidColorBrush)block.Foreground).Color;

    /// <summary>Presses Ctrl: the key goes down while the keyboard state says Ctrl is held.</summary>
    public void PressCtrl()
    {
        HeldModifier.While(ModifierKeys.Control, () => SimulatedKey.Raise(Window, Key.LeftCtrl, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent));
        Refresh();
    }

    /// <summary>Releases Ctrl.</summary>
    public void ReleaseCtrl()
    {
        SimulatedKey.Raise(Window, Key.LeftCtrl, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent);
        Refresh();
    }

    public void Dispose() => _windows.Dispose();

    private void Refresh()
    {
        Window.UpdateLayout();
        Window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        Window.UpdateLayout();
    }
}
