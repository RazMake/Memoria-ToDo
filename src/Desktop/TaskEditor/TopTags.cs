using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The most-used tags as colored badges, each a button that reports its tag. Follows the tags in use while on screen.</summary>
public sealed class TopTags : UserControl
{
    private readonly ITasks _tasks;
    private readonly BadgeList _badges;

    public TopTags(ITasks tasks, ITagColors tagColors, ITheme theme)
        : this(tasks ?? throw new ArgumentNullException(nameof(tasks)), new TagLook(tagColors, theme))
    {
    }

    private TopTags(ITasks tasks, TagLook look)
    {
        _tasks = tasks;
        _badges = new BadgeList(look, TagBadgeVisuals.Clickable(look), tag => TagChosen?.Invoke(this, tag));
        Content = _badges;
        Show();
        Loaded += (_, _) =>
        {
            _tasks.Changed -= OnTasksChanged;
            _tasks.Changed += OnTasksChanged;
            Show();
        };
        Unloaded += (_, _) => _tasks.Changed -= OnTasksChanged;
    }

    /// <summary>Raised with the tag whose badge was clicked.</summary>
    public event EventHandler<string>? TagChosen;

    private void Show() => _badges.Tags = MostUsedTags.Of(_tasks, []);

    private void OnTasksChanged(object? sender, EventArgs e) => OnElementThread.Run(this, Show);
}
