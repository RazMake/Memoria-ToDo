using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// Sections of which exactly one is always open: the first one added, then whichever the user expands. The open
/// section fills the space the collapsed headings leave, and its content scrolls when it is taller.
/// </summary>
internal sealed class Accordion : Grid
{
    private readonly List<Expander> _sections = [];
    private Expander? _open;

    /// <summary>Adds a section with the heading and content; the first section added is open.</summary>
    public void Add(string heading, UIElement content)
    {
        var section = new Expander
        {
            Header = ThemedText.Heading(heading, new Thickness(0)),
            Content = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            },
            IsExpanded = false,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, _sections.Count == 0 ? 0 : 8, 0, 0),
        };
        section.Expanded += (_, e) =>
        {
            if (e.OriginalSource == section)
            {
                Open(section);
            }
        };
        section.Collapsed += (_, e) =>
        {
            if (e.OriginalSource == section && section == _open)
            {
                section.IsExpanded = true;
            }
        };
        SetRow(section, _sections.Count);
        RowDefinitions.Add(new RowDefinition());
        _sections.Add(section);
        Children.Add(section);
        if (_open is null)
        {
            section.IsExpanded = true;
        }
        else
        {
            RowDefinitions[^1].Height = GridLength.Auto;
        }
    }

    private void Open(Expander section)
    {
        _open = section;
        for (var i = 0; i < _sections.Count; i++)
        {
            _sections[i].IsExpanded = _sections[i] == section;
            RowDefinitions[i].Height = _sections[i] == section ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        }
    }
}
