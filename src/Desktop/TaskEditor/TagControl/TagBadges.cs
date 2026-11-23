using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>A row of colored badges that shows a task's tags and does nothing else. Used by the Task list.</summary>
public sealed class TagBadges : UserControl
{
    private readonly BadgeList _badges;

    public TagBadges(ITagColors tagColors, ITheme theme)
        : this(new TagLook(tagColors, theme))
    {
    }

    private TagBadges(TagLook look)
    {
        _badges = new BadgeList(look, TagBadgeVisuals.Plain(look), _ => { });
        Content = _badges;
    }

    /// <summary>The tags shown, in order.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _badges.Tags;
        set => _badges.Tags = value;
    }
}
