using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// A wrapping row of tag badges that takes the tags' colors from <see cref="TagLook"/> and redraws when they or the theme
/// change. An optional element ends the row, wrapping with the badges.
/// </summary>
internal sealed class BadgeList : ItemsControl
{
    private readonly UIElement? _last;
    private IReadOnlyList<string> _tags = [];

    public BadgeList(TagLook look, FrameworkElementFactory badge, Action<string> clicked, UIElement? last = null)
    {
        _last = last;
        ClickableItems.Attach(this, badge, clicked);
        look.Follow(this, Redraw);
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
            ItemsSource = _last is null
                ? value
                : new CompositeCollection { new CollectionContainer { Collection = value }, _last };
        }
    }

    // Drawing the badges again takes the last element out of the row for a moment, which would drop the cursor from it.
    private void Redraw()
    {
        var hadCursor = _last?.IsKeyboardFocused == true;
        Items.Refresh();
        if (hadCursor)
        {
            KeyboardFocus.MoveTo(_last!);
        }
    }
}
