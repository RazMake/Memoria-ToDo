using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// The card's element: the content's heading in the theme's first heading color, then its fields and help line. Nothing on it takes a click or the focus.
/// The card shows in a popup that never takes the focus, so keys go to the window that holds the card's owner; the card watches that
/// window's Ctrl, from when it loads into the popup to when it unloads.
/// </summary>
internal sealed class ContactCardView : Border
{
    private readonly ContactCardContent _content;
    private readonly StackPanel _panel = new();
    private readonly ITheme _theme;
    private Window? _window;
    private Brush? _headingBrush;
    private TextBlock? _heading;

    public ContactCardView(ContactCardContent content, ITheme theme)
    {
        _content = content;
        _theme = theme;
        MaxWidth = 360;
        Padding = new Thickness(12);
        BorderThickness = new Thickness(1);
        Child = _panel;
        // A popup isn't in its owner's visual tree, so these come from the application's theme, which follows Windows' setting.
        SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        SetResourceReference(BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
        UpdateHeadingBrush();
        Render();
        // The follower stays alive through its event subscriptions on the card and the theme.
        _ = new ThemeFollower(this, theme, () =>
        {
            UpdateHeadingBrush();
            _heading!.Foreground = _headingBrush;
        });
        Loaded += (_, _) => Watch();
        Unloaded += (_, _) => Unwatch();
    }

    // Where the card is: in a window, or in the popup that shows it, directly or inside a scroller, by an element of a window.
    private Window? HostWindow()
    {
        if (Window.GetWindow(this) is { } window)
        {
            return window;
        }

        DependencyObject? up = Parent;
        while (up is not null and not Popup)
        {
            up = LogicalTreeHelper.GetParent(up);
        }

        return (up as Popup)?.PlacementTarget is { } target ? Window.GetWindow(target) : null;
    }

    private void Watch()
    {
        Unwatch();
        _window = HostWindow();
        if (_window is null)
        {
            return;
        }

        _window.PreviewKeyDown += CtrlChanged;
        _window.PreviewKeyUp += CtrlChanged;
    }

    private void Unwatch()
    {
        if (_window is { } window)
        {
            window.PreviewKeyDown -= CtrlChanged;
            window.PreviewKeyUp -= CtrlChanged;
            _window = null;
        }
    }

    private void CtrlChanged(object sender, KeyEventArgs e)
    {
        if (ModifierKey.Of(EventKey.Of(e)) == ModifierKeys.Control)
        {
            Show(IsCtrlHeld());
        }
    }

    /// <summary>Whether Ctrl is held now.</summary>
    public static bool IsCtrlHeld() => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

    private void Show(bool allDetails)
    {
        if (_content.AllDetails != allDetails)
        {
            _content.AllDetails = allDetails;
            Render();
        }
    }

    private void UpdateHeadingBrush()
    {
        var color = (Color)ColorConverter.ConvertFromString(TextLooks.Of(_theme.IsDark).HeadingColor(1));
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _headingBrush = brush;
    }

    private void Render()
    {
        _panel.Children.Clear();
        _heading = DetailBlocks.Heading(_content.Heading);
        _heading.Foreground = _headingBrush;
        _panel.Children.Add(_heading);
        _panel.Children.Add(DetailBlocks.GroupHeading(_content.GroupHeading));
        foreach (var field in _content.Fields)
        {
            _panel.Children.Add(DetailBlocks.Label(field.Label));
            foreach (var value in field.Values)
            {
                _panel.Children.Add(DetailBlocks.Value(value.Text));
            }
        }

        var help = DetailBlocks.Label(_content.Help);
        help.Margin = new Thickness(0, 12, 0, 0);
        _panel.Children.Add(help);
    }
}
