using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The menu the + button opens: the tags to add as colored badges, then Custom. Keys move a highlight over the items without
/// taking focus from the + button; a click or <see cref="ChooseHighlighted"/> reports the item.
/// </summary>
internal sealed class TagMenu : StackPanel
{
    private readonly TagLook _look;
    private readonly List<(Button Item, string? Tag)> _items = [];
    private int _highlighted = -1;

    public TagMenu(TagLook look)
    {
        _look = look;
        AutomationProperties.SetAutomationId(this, "TagMenu");
        Visibility = Visibility.Collapsed;
        look.Follow(this, Repaint);
    }

    /// <summary>Raised with the tag whose item was chosen.</summary>
    public event Action<string>? TagChosen;

    /// <summary>Raised when the Custom item was chosen.</summary>
    public event Action? CustomChosen;

    public bool IsOpen => Visibility == Visibility.Visible;

    /// <summary>Shows the menu with the tags and Custom, none highlighted.</summary>
    public void Open(IReadOnlyList<string> tags)
    {
        Children.Clear();
        _items.Clear();
        _highlighted = -1;
        foreach (var tag in tags)
        {
            var item = TagBadgeVisuals.ButtonFor(tag, _look.FillOf(tag));
            AutomationProperties.SetAutomationId(item, $"MenuTag {tag}");
            Add(item, tag);
        }

        if (tags.Count > 0)
        {
            Children.Add(new Separator());
        }

        var custom = new Button
        {
            Template = TagBadgeVisuals.FlatButton,
            Content = "Custom…",
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetAutomationId(custom, "MenuCustom");
        Add(custom, null);
        Visibility = Visibility.Visible;
    }

    /// <summary>Hides the menu. Returns false, doing nothing, when it was closed.</summary>
    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        Visibility = Visibility.Collapsed;
        return true;
    }

    public void HighlightNext() => Highlight(Math.Min(_highlighted + 1, _items.Count - 1));

    public void HighlightPrevious() => Highlight(Math.Max(_highlighted - 1, 0));

    /// <summary>Chooses the highlighted item. Returns false, doing nothing, when none is highlighted.</summary>
    public bool ChooseHighlighted()
    {
        if (_highlighted < 0)
        {
            return false;
        }

        Choose(_items[_highlighted].Tag);
        return true;
    }

    private void Repaint()
    {
        foreach (var (item, tag) in _items)
        {
            if (tag is not null)
            {
                TagBadgeVisuals.Paint(item, _look.FillOf(tag));
            }
        }
    }

    private void Add(Button item, string? tag)
    {
        item.BorderThickness = new Thickness(2);
        item.BorderBrush = Brushes.Transparent;
        item.Click += (_, _) => Choose(tag);
        item.Focusable = false;
        Children.Add(item);
        _items.Add((item, tag));
    }

    private void Highlight(int position)
    {
        if (_highlighted >= 0)
        {
            _items[_highlighted].Item.BorderBrush = Brushes.Transparent;
        }

        _highlighted = position;
        _items[position].Item.BorderBrush = SystemColors.HighlightBrush;
    }

    private void Choose(string? tag)
    {
        Close();
        if (tag is null)
        {
            CustomChosen?.Invoke();
        }
        else
        {
            TagChosen?.Invoke(tag);
        }
    }
}
