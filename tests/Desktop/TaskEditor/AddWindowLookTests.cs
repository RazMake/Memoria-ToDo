using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Title and Description look like the Tags line, a WPF text box the theme styles: the same background, text color,
/// border, corner radius, padding, and focus look, in the light and the dark theme, and they follow the theme while
/// Add is open. A Title marked as missing paints its border in the theme's error color. A test sees a look only as
/// the properties of the field and of the border its template draws. The look under the pointer can't be reached
/// without a mouse, so no test covers it.
/// </summary>
[UX]
public class AddWindowLookTests
{
    private const string ErrorBrushKey = "SystemFillColorCriticalBrush";

    [WpfTheory]
    [InlineData(false, EditorFields.Title)]
    [InlineData(true, EditorFields.Title)]
    [InlineData(false, EditorFields.Description)]
    [InlineData(true, EditorFields.Description)]
    public void A_field_has_the_Tags_line_background_text_color_border_and_padding(bool dark, string field)
    {
        using var screen = Open(dark);

        AssertSameLook(screen, field);
    }

    [WpfTheory]
    [InlineData(false, EditorFields.Title)]
    [InlineData(true, EditorFields.Title)]
    [InlineData(false, EditorFields.Description)]
    [InlineData(true, EditorFields.Description)]
    public void A_field_has_the_Tags_line_rounded_corners(bool dark, string field)
    {
        using var screen = Open(dark);

        var tags = Frame(screen, EditorFields.Tags);
        var frame = Frame(screen, field);

        Assert.True(tags.CornerRadius != default, "The Tags line has no rounded corners to match.");
        Assert.Equal(tags.CornerRadius, frame.CornerRadius);
    }

    [WpfTheory]
    [InlineData(EditorFields.Title)]
    [InlineData(EditorFields.Description)]
    public void A_field_follows_the_theme_while_Add_is_open(string field)
    {
        using var screen = Open(dark: false);
        var lightBackground = ((Control)EditorFields.Field(screen.Window, field)).Background;

        screen.UseTheme(dark: true);

        AssertSameLook(screen, field);
        Assert.False(
            Looks.BrushesAreEqual(lightBackground, ((Control)EditorFields.Field(screen.Window, field)).Background),
            "The background didn't change with the theme.");
    }

    [WpfTheory]
    [InlineData(false, EditorFields.Title)]
    [InlineData(true, EditorFields.Title)]
    [InlineData(false, EditorFields.Description)]
    [InlineData(true, EditorFields.Description)]
    public void A_field_with_the_focus_looks_like_the_Tags_line_with_the_focus(bool dark, string field)
    {
        using var screen = Open(dark);
        screen.Focus(EditorFields.Tags);
        var tags = Look.Of(screen, EditorFields.Tags);
        screen.Focus(field);

        Look.Of(screen, field).AssertSameAs(tags);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_missing_Title_paints_its_border_in_the_theme_error_color(bool dark)
    {
        using var screen = Open(dark);

        screen.Save();

        Assert.True(Looks.BrushesAreEqual(
            (Brush)screen.Window.FindResource(ErrorBrushKey), screen.TitleBorderBrush));
    }

    [WpfFact]
    public void A_missing_Title_follows_the_theme_error_color_when_the_theme_switches()
    {
        using var screen = Open(dark: false);
        screen.Save();

        screen.UseTheme(dark: true);

        Assert.True(Looks.BrushesAreEqual(
            (Brush)screen.Window.FindResource(ErrorBrushKey), screen.TitleBorderBrush));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Editing_a_missing_Title_gives_it_the_Tags_line_border_again(bool dark)
    {
        using var screen = Open(dark);
        screen.Save();

        screen.Edit(EditorFields.Title, "Buy milk", 8);

        Assert.True(Looks.BrushesAreEqual(
            ((Control)EditorFields.Field(screen.Window, EditorFields.Tags)).BorderBrush, screen.TitleBorderBrush));
    }

    private static AddScreen Open(bool dark)
    {
        var screen = new AddScreen();
        screen.Show();
        screen.UseTheme(dark);
        return screen;
    }

    // Add opens with the Title focused, and a focused field looks different, so the focus goes to a button first.
    private static void AssertSameLook(AddScreen screen, string field)
    {
        Keyboard.Focus(screen.SaveButton);
        Look.Of(screen, field).AssertSameAs(Look.Of(screen, EditorFields.Tags));
    }

    private static Border Frame(AddScreen screen, string field)
    {
        screen.Window.UpdateLayout();
        return Look.FrameOf(EditorFields.Field(screen.Window, field));
    }

    /// <summary>What a reader sees of a field: its own properties and those of the border its template draws.</summary>
    private sealed record Look(
        Brush? Background, Brush? Foreground, Brush? BorderBrush, Thickness BorderThickness, Thickness Padding,
        Brush? FrameBackground, Brush? FrameBorderBrush, Thickness FrameBorderThickness)
    {
        public static Look Of(AddScreen screen, string field)
        {
            screen.Window.UpdateLayout();
            var control = (Control)EditorFields.Field(screen.Window, field);
            var frame = FrameOf(control);
            return new Look(
                control.Background, control.Foreground, control.BorderBrush, control.BorderThickness, control.Padding,
                frame.Background, frame.BorderBrush, frame.BorderThickness);
        }

        /// <summary>The first border the field's template draws, or the border around the field when the template has none.</summary>
        public static Border FrameOf(DependencyObject field)
        {
            var queue = new Queue<DependencyObject>([field]);
            while (queue.Count > 0)
            {
                var element = queue.Dequeue();
                if (element is Border border)
                {
                    return border;
                }

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                {
                    queue.Enqueue(VisualTreeHelper.GetChild(element, i));
                }
            }

            for (var parent = VisualTreeHelper.GetParent(field); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is Border outer)
                {
                    return outer;
                }
            }

            throw new InvalidOperationException("The field has no border.");
        }

        public void AssertSameAs(Look expected)
        {
            Assert.True(Looks.BrushesAreEqual(expected.Background, Background), "Background differs.");
            Assert.True(Looks.BrushesAreEqual(expected.Foreground, Foreground), "Text color differs.");
            Assert.True(Looks.BrushesAreEqual(expected.BorderBrush, BorderBrush), "Border brush differs.");
            Assert.Equal(expected.BorderThickness, BorderThickness);
            Assert.Equal(expected.Padding, Padding);
            Assert.True(Looks.BrushesAreEqual(expected.FrameBackground, FrameBackground), "Drawn background differs.");
            Assert.True(Looks.BrushesAreEqual(expected.FrameBorderBrush, FrameBorderBrush), "Drawn border brush differs.");
            Assert.Equal(expected.FrameBorderThickness, FrameBorderThickness);
        }
    }
}

/// <summary>Compares brushes by what they paint, since a theme may hand out equal brushes as different objects.</summary>
internal static class Looks
{
    public static bool BrushesAreEqual(Brush? a, Brush? b) => (a, b) switch
    {
        (SolidColorBrush x, SolidColorBrush y) => x.Color == y.Color && x.Opacity == y.Opacity,
        (LinearGradientBrush x, LinearGradientBrush y) =>
            x.StartPoint == y.StartPoint && x.EndPoint == y.EndPoint
            && x.GradientStops.Select(s => (s.Color, s.Offset)).SequenceEqual(y.GradientStops.Select(s => (s.Color, s.Offset))),
        _ => ReferenceEquals(a, b),
    };
}
