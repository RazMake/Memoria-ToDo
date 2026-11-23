using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>A wrapping row of tag badges that takes the tags' colors from <see cref="TagLook"/> and redraws when they or the theme change.</summary>
internal sealed class BadgeList : ItemsControl
{
    private IReadOnlyList<string> _tags = [];

    public BadgeList(TagLook look, FrameworkElementFactory badge, Action<string> clicked)
    {
        ClickableItems.Attach(this, badge, clicked);
        look.Follow(this, () => Items.Refresh());
        Tags = [];
    }

    /// <summary>The tags shown, in order.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _tags = value;
            ItemsSource = value;
        }
    }
}