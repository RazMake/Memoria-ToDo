using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Sections of which exactly one is always open: the first at first, then whichever the user picks by its title.
/// Each section is a rounded box. The open one fills the space the other titles leave, starts right under its title,
/// and scrolls when its content is taller. A title is a button, so Tab reaches it and Enter or Space opens its section.
/// Titles show like heading 1, in the theme's color.
/// </summary>
internal sealed class Accordion : Grid
{
    private readonly AccordionState _state;
    private readonly RowDefinition[] _rows;
    private readonly ScrollViewer[] _contents;
    private readonly TextBlock[] _titles;
    private readonly ITheme _theme;

    public Accordion(ITheme theme, params (string Heading, UIElement Content)[] sections)
    {
        _theme = theme;
        _state = new AccordionState(sections.Length);
        _rows = new RowDefinition[sections.Length];
        _contents = new ScrollViewer[sections.Length];
        _titles = new TextBlock[sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            var section = Section(i, sections[i].Heading, sections[i].Content);
            section.Margin = new Thickness(0, i == 0 ? 0 : 8, 0, 0);
            _rows[i] = new RowDefinition();
            RowDefinitions.Add(_rows[i]);
            SetRow(section, i);
            Children.Add(section);
        }

        Show();
        // The follower stays alive through its event subscriptions on this accordion and the theme.
        _ = new ThemeFollower(this, theme, ShowTitles);
    }

    private Border Section(int index, string heading, UIElement content)
    {
        _titles[index] = new TextBlock
        {
            Text = heading,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.Bold,
        };
        var title = new Button
        {
            Content = _titles[index],
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        title.Click += (_, _) => Select(index);
        title.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                Select(index);
            }
        };
        // Top-aligned on purpose: the theme's controls center themselves, and a section's content must start under
        // its title, not in the middle of the window.
        if (content is FrameworkElement element)
        {
            element.VerticalAlignment = VerticalAlignment.Top;
        }

        _contents[index] = new ScrollViewer
        {
            Content = content,
            Margin = new Thickness(0, 8, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var inside = new Grid
        {
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() },
        };
        SetRow(_contents[index], 1);
        inside.Children.Add(title);
        inside.Children.Add(_contents[index]);
        return ThemedBox.Section(inside);
    }

    // Heading 1's look for the theme: its color, bold, and its multiple of the window's normal text size.
    private void ShowTitles()
    {
        var color = (Color)ColorConverter.ConvertFromString(TextLooks.Of(_theme.IsDark).HeadingColor(1)!);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var size = TextLooks.HeadingScale(1) * TextElement.GetFontSize(this);
        foreach (var title in _titles)
        {
            title.Foreground = brush;
            title.FontSize = size;
        }
    }

    private void Select(int index)
    {
        if (_state.Select(index))
        {
            Show();
        }
    }

    private void Show()
    {
        for (var i = 0; i < _rows.Length; i++)
        {
            var open = i == _state.Open;
            _rows[i].Height = open ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            _contents[i].Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
