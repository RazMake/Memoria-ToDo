using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Collapsible sections that all start collapsed; expanding one collapses the others.</summary>
internal sealed class Accordion : StackPanel
{
    private readonly List<Expander> _sections = [];

    /// <summary>Adds a section with the heading and content, collapsed.</summary>
    public void Add(string heading, UIElement content)
    {
        var section = new Expander
        {
            Header = ThemedText.Heading(heading, new Thickness(0)),
            Content = content,
            IsExpanded = false,
            Margin = new Thickness(0, _sections.Count == 0 ? 0 : 8, 0, 0),
        };
        section.Expanded += (_, e) =>
        {
            if (e.OriginalSource != section)
            {
                return;
            }

            foreach (var other in _sections.Where(other => other != section))
            {
                other.IsExpanded = false;
            }
        };
        _sections.Add(section);
        Children.Add(section);
    }
}
