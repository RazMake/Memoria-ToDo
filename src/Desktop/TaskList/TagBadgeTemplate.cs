using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Shows an item that is a tag name as the Task editor's badge for it, in the tag's color for the current theme.</summary>
internal static class TagBadgeTemplate
{
    public static DataTemplate For(ITagColors tagColors, ITheme theme)
    {
        var badge = new FrameworkElementFactory(typeof(ContentPresenter));
        badge.SetBinding(ContentPresenter.ContentProperty, new Binding { Converter = new ToBadge(tagColors, theme) });
        return new DataTemplate { VisualTree = badge };
    }

    // Each use gets its own badge control, as one control can't show in the list and in the box that shows the choice.
    private sealed class ToBadge(ITagColors tagColors, ITheme theme) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new TagBadges(tagColors, theme) { Tags = [(string)value] };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
