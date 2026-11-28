using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// How tags look: each tag's color, kept or default, in the fill the current theme gets. A part that draws tags asks it for
/// fills, and follows its changes while on screen.
/// </summary>
internal sealed class TagLook
{
    private static readonly ConcurrentDictionary<string, Brush> Paints = new(StringComparer.Ordinal);

    private readonly ITagColors _tagColors;
    private readonly ITheme _theme;

    public TagLook(ITagColors tagColors, ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(tagColors);
        ArgumentNullException.ThrowIfNull(theme);
        _tagColors = tagColors;
        _theme = theme;
    }

    /// <summary>The tag's kept color, or its default when none is kept or the kept name isn't one this app knows, in the current theme.</summary>
    public TagFill FillOf(string tag) => FillOf(TagColorOf.Of(_tagColors, tag));

    /// <summary>The color's fill in the current theme.</summary>
    public TagFill FillOf(TagColor color) => _theme.IsDark ? color.Dark : color.Light;

    /// <summary>A frozen brush of a "#rrggbb" value, shared by every part that paints it.</summary>
    public static Brush Paint(string hex) =>
        Paints.GetOrAdd(hex, value =>
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            return brush;
        });

    /// <summary>
    /// Calls <paramref name="redraw"/> when the tag colors or the theme change, and each time <paramref name="element"/> is
    /// loaded, but only while it is loaded, so neither can keep it alive. A change on another thread is queued to the element's.
    /// </summary>
    public void Follow(FrameworkElement element, Action redraw)
    {
        void OnChanged(object? sender, EventArgs e) => OnElementThread.Run(element, redraw);
        WhileLoaded.Run(element, () =>
        {
            _tagColors.Changed += OnChanged;
            _theme.Changed += OnChanged;
            redraw();
        }, () =>
        {
            _tagColors.Changed -= OnChanged;
            _theme.Changed -= OnChanged;
        });
    }
}
