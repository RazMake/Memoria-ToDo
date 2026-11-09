using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>A wrapping row of items, each drawn by a template whose buttons report the item they sit in.</summary>
internal static class ClickableItems
{
    /// <summary>
    /// An items control that lays its string items out in a wrapping row, each drawn as <paramref name="itemVisual"/>.
    /// Clicking a button inside an item calls <paramref name="clicked"/> with that item.
    /// </summary>
    public static ItemsControl Create(FrameworkElementFactory itemVisual, Action<string> clicked)
    {
        var items = new ItemsControl();
        Attach(items, itemVisual, clicked);
        return items;
    }

    /// <summary>Makes an existing items control behave as <see cref="Create"/> describes, for a control that is itself the list.</summary>
    public static void Attach(ItemsControl items, FrameworkElementFactory itemVisual, Action<string> clicked) =>
        AttachByPosition(items, itemVisual, position => clicked((string)items.Items[position]));

    /// <summary>
    /// Like <see cref="Attach"/>, but calls <paramref name="clicked"/> with the clicked item's position, which tells apart
    /// items that are equal.
    /// </summary>
    public static void AttachByPosition(ItemsControl items, FrameworkElementFactory itemVisual, Action<int> clicked)
    {
        items.ItemTemplate = new DataTemplate { VisualTree = itemVisual };
        items.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
        items.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(items, source) is { } container)
            {
                clicked(items.ItemContainerGenerator.IndexFromContainer(container));
            }
        }));
    }

    /// <summary>A button in an item's template whose content is the item itself.</summary>
    public static FrameworkElementFactory ItemButton()
    {
        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(ContentControl.ContentProperty, new Binding("."));
        button.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 4));
        button.SetValue(Control.PaddingProperty, new Thickness(8, 2, 8, 2));
        return button;
    }
}
