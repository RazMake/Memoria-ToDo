using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The message line's behavior, measured on a bare element in a bare container that holds the theme resource the color
/// comes from: no window shows.
/// </summary>
[Unit]
public class MessageLineTests
{
    private const string CriticalKey = "SystemFillColorCriticalBrush";

    [WpfFact]
    public void A_new_line_has_no_text_and_takes_no_room()
    {
        var line = new MessageLine();

        Assert.Equal("", line.Text);
        Assert.Equal(Visibility.Collapsed, line.Visibility);
    }

    [WpfFact]
    public void Showing_a_text_shows_the_line_with_it()
    {
        var line = new MessageLine();

        line.Show("Could not save.");

        Assert.Equal("Could not save.", line.Text);
        Assert.Equal(Visibility.Visible, line.Visibility);
    }

    [WpfTheory]
    [InlineData(null)]
    [InlineData("")]
    public void Showing_no_text_hides_a_shown_line_and_clears_its_text(string? none)
    {
        var line = new MessageLine();
        line.Show("Could not save.");

        line.Show(none);

        Assert.Equal("", line.Text);
        Assert.Equal(Visibility.Collapsed, line.Visibility);
    }

    [WpfFact]
    public void Showing_another_text_replaces_the_first_and_showing_again_after_a_hide_shows_the_line_again()
    {
        var line = new MessageLine();
        line.Show("First.");

        line.Show("Second.");
        Assert.Equal("Second.", line.Text);

        line.Show(null);
        line.Show("Third.");

        Assert.Equal("Third.", line.Text);
        Assert.Equal(Visibility.Visible, line.Visibility);
    }

    [WpfFact]
    public void The_text_wraps()
    {
        Assert.Equal(TextWrapping.Wrap, new MessageLine().TextWrapping);
    }

    [WpfFact]
    public void The_text_takes_the_themes_critical_color_and_follows_it_when_the_theme_changes()
    {
        var light = new SolidColorBrush(Colors.Firebrick);
        var dark = new SolidColorBrush(Colors.Salmon);
        var line = new MessageLine();
        var container = new DockPanel();
        container.Resources[CriticalKey] = light;
        container.Children.Add(line);

        Assert.Same(light, line.Foreground);

        container.Resources[CriticalKey] = dark;

        Assert.Same(dark, line.Foreground);
    }

    [WpfFact]
    public void A_line_given_a_margin_keeps_it_through_showing_and_hiding()
    {
        var line = new MessageLine { Margin = new Thickness(8, 4, 8, 0) };

        line.Show("Could not save.");
        line.Show(null);

        Assert.Equal(new Thickness(8, 4, 8, 0), line.Margin);
    }
}
