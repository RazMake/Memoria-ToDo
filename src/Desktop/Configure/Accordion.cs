using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Sections of which exactly one is always open: the first at first, then whichever the user picks by its title.
/// Each section is a rounded box. The open one fills the space the other titles leave, starts right under its title,
/// and scrolls when its content is taller. A title is a button, so Tab reaches it and Enter or Space opens its section.
/// </summary>
internal sealed class Accordion : Grid
{
    private readonly AccordionState _state;
    private readonly RowDefinition[] _rows;
    private readonly ScrollViewer[] _contents;

    public Accordion(params (string Heading, UIElement Content)[] sections)
    {
        _state = new AccordionState(sections.Length);
        _rows = new RowDefinition[sections.Length];
        _contents = new ScrollViewer[sections.Length];
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
    }

    private Border Section(int index, string heading, UIElement content)
    {
        var title = new Button
        {
            Content = ThemedText.Heading(heading, new Thickness(0)),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
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
        _contents[index] = new ScrollViewer
        {
            Content = content,
            Margin = new Thickness(0, 8, 0, 0),
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
