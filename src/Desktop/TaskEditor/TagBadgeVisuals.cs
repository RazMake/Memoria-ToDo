using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// What a tag badge is made of: a rounded fill in the tag's color with its name in the color's text color. Badges are drawn
/// by item templates, so a list redraws them all by refreshing its items; the fill is looked up again for each.
/// </summary>
internal static class TagBadgeVisuals
{
    private static readonly CornerRadius Rounded = new(10);

    // A template belongs to the thread that made it, so each UI thread builds its own.
    [ThreadStatic]
    private static ControlTemplate? _flatButton;

    /// <summary>A button with no chrome of its own: its background, border, and padding are all it draws around its content.</summary>
    public static ControlTemplate FlatButton => _flatButton ??= FlatButtonTemplate();

    /// <summary>A badge that only shows the tag.</summary>
    public static FrameworkElementFactory Plain(TagLook look)
    {
        var badge = Badge(look);
        badge.AppendChild(Name(look));
        return badge;
    }

    /// <summary>A badge with a button after the tag's name that removes the tag.</summary>
    public static FrameworkElementFactory Removable(TagLook look)
    {
        var remove = new FrameworkElementFactory(typeof(Button));
        remove.SetValue(Control.TemplateProperty, FlatButton);
        remove.SetValue(ContentControl.ContentProperty, "×");
        remove.SetValue(Control.PaddingProperty, new Thickness(4, 0, 0, 0));
        remove.SetValue(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        remove.SetBinding(AutomationProperties.AutomationIdProperty, new Binding(".") { StringFormat = "Remove {0}" });
        remove.SetBinding(AutomationProperties.NameProperty, new Binding(".") { StringFormat = "Remove {0}" });
        remove.SetBinding(Control.ForegroundProperty, TextOf(look));

        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.AppendChild(Name(look));
        row.AppendChild(remove);

        var badge = Badge(look);
        badge.AppendChild(row);
        return badge;
    }

    /// <summary>A badge button for the tag, outside a list, in the fill.</summary>
    public static Button ButtonFor(string tag, TagFill fill)
    {
        var button = new Button
        {
            Template = FlatButton,
            Content = tag,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Paint(button, fill);
        return button;
    }

    /// <summary>Gives a badge button from <see cref="ButtonFor"/> the fill.</summary>
    public static void Paint(Button button, TagFill fill)
    {
        button.Background = TagLook.Paint(fill.Background);
        button.Foreground = TagLook.Paint(fill.Text);
    }

    private static FrameworkElementFactory Badge(TagLook look)
    {
        var badge = new FrameworkElementFactory(typeof(Border));
        badge.SetValue(Border.CornerRadiusProperty, Rounded);
        badge.SetValue(Border.PaddingProperty, new Thickness(8, 2, 8, 2));
        badge.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 6));
        badge.SetBinding(AutomationProperties.AutomationIdProperty, new Binding(".") { StringFormat = "Badge {0}" });
        badge.SetBinding(Border.BackgroundProperty, BackgroundOf(look));
        return badge;
    }

    private static FrameworkElementFactory Name(TagLook look)
    {
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding("."));
        name.SetBinding(TextBlock.ForegroundProperty, TextOf(look));
        name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        return name;
    }

    private static Binding BackgroundOf(TagLook look) => new(".") { Converter = new FillPart(look, fill => fill.Background) };

    private static Binding TextOf(TagLook look) => new(".") { Converter = new FillPart(look, fill => fill.Text) };

    private static ControlTemplate FlatButtonTemplate()
    {
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var frame = new FrameworkElementFactory(typeof(Border));
        frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        frame.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        frame.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        frame.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        frame.SetValue(Border.CornerRadiusProperty, Rounded);
        frame.AppendChild(content);
        return new ControlTemplate(typeof(Button)) { VisualTree = frame };
    }

    // The tag's fill for the current theme, as one part of it, painted.
    private sealed class FillPart(TagLook look, Func<TagFill, string> part) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            TagLook.Paint(part(look.FillOf((string)value)));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
