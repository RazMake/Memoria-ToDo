using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// How tests find and read the tag parts, by automation id, so they don't depend on the controls the parts are built from.
/// A badge is the element with id <c>Badge {tag}</c>: its background is the color's value and the text block in it has the
/// color's text color. A badge's remove button has id <c>Remove {tag}</c>. The + button has id <c>AddTag</c>. The menu has
/// id <c>TagMenu</c> and is showing only while open; its items are buttons with ids <c>MenuTag {tag}</c> and <c>MenuCustom</c>.
/// The 30 colors are toggle buttons in <c>TagColorGrid</c> with ids <c>Color {name}</c>, checked when picked. Shared test setup.
/// </summary>
internal static class TagControls
{
    public const string AddTag = EditorFields.AddTag;
    public const string Menu = "TagMenu";
    public const string MenuCustom = "MenuCustom";
    public const string ColorGrid = "TagColorGrid";

    private const string BadgePrefix = "Badge ";
    private const string RemovePrefix = "Remove ";
    private const string MenuTagPrefix = "MenuTag ";
    private const string ColorPrefix = "Color ";

    /// <summary>The tags of the badges below <paramref name="root"/>, in the order they show.</summary>
    public static IReadOnlyList<string> BadgeTags(DependencyObject root) =>
        [.. WithIdPrefix(root, BadgePrefix).Select(element => IdOf(element)[BadgePrefix.Length..])];

    /// <summary>The badge for the tag.</summary>
    public static FrameworkElement Badge(DependencyObject root, string tag) => Part(root, BadgePrefix + tag);

    /// <summary>The badge's value and text color as "#rrggbb", read from what the badge paints.</summary>
    public static TagFill FillOf(DependencyObject root, string tag)
    {
        var badge = Badge(root, tag);
        var background = badge switch
        {
            Border border => border.Background,
            Control control => control.Background,
            Panel panel => panel.Background,
            _ => throw new InvalidOperationException($"{badge.GetType().Name} has no background."),
        };
        var text = Visuals(badge).OfType<TextBlock>().FirstOrDefault()?.Foreground
            ?? (badge as Control)?.Foreground
            ?? throw new InvalidOperationException("The badge shows no text.");
        return new TagFill(Hex(background), Hex(text));
    }

    /// <summary>The fill the tag color has in the theme, as Rendering decides.</summary>
    public static TagFill Expected(TagColor color, bool dark) => dark ? color.Dark : color.Light;

    /// <summary>The remove button of the tag's badge.</summary>
    public static Button RemoveButton(DependencyObject root, string tag) => (Button)Part(root, RemovePrefix + tag);

    /// <summary>The tags whose remove buttons exist, in the order they show.</summary>
    public static IReadOnlyList<string> RemovableTags(DependencyObject root) =>
        [.. WithIdPrefix(root, RemovePrefix).Select(element => IdOf(element)[RemovePrefix.Length..])];

    /// <summary>The + button.</summary>
    public static Button Plus(DependencyObject root) => (Button)Part(root, AddTag);

    /// <summary>Whether the menu is showing.</summary>
    public static bool MenuIsOpen(DependencyObject root) => TextFields.FindShowing(root, Menu) is not null;

    /// <summary>The tags the open menu lists, in order; empty while it is closed.</summary>
    public static IReadOnlyList<string> MenuTags(DependencyObject root) =>
        MenuIsOpen(root)
            ? [.. WithIdPrefix(Part(root, Menu), MenuTagPrefix).Select(element => IdOf(element)[MenuTagPrefix.Length..])]
            : [];

    /// <summary>The menu's button for the tag.</summary>
    public static Button MenuItem(DependencyObject root, string tag) => (Button)Part(root, MenuTagPrefix + tag);

    /// <summary>The fill the menu's button for the tag paints now: its background and its text color.</summary>
    public static TagFill MenuFillOf(DependencyObject root, string tag)
    {
        var item = MenuItem(root, tag);
        return new TagFill(Hex(item.Background), Hex(item.Foreground));
    }

    /// <summary>The tag of the menu item the keys have highlighted, or null when none is. A highlighted item has the system highlight color as its border.</summary>
    public static string? HighlightedMenuTag(DependencyObject root) =>
        MenuTags(root).SingleOrDefault(tag =>
            MenuItem(root, tag).BorderBrush is SolidColorBrush { Color: var c } && c == SystemColors.HighlightColor);

    /// <summary>The menu's Custom button.</summary>
    public static Button CustomItem(DependencyObject root) => (Button)Part(root, MenuCustom);

    /// <summary>Whether the tag box, which only Custom shows, is showing.</summary>
    public static bool CustomIsOpen(DependencyObject root) => TextFields.FindShowing(root, EditorFields.Tags) is not null;

    /// <summary>The color buttons in the grid, by color name.</summary>
    public static IReadOnlyList<(string Name, ToggleButton Button)> ColorButtons(DependencyObject root) =>
        [.. WithIdPrefix(Part(root, ColorGrid), ColorPrefix)
            .Select(element => (IdOf(element)[ColorPrefix.Length..], (ToggleButton)element))];

    /// <summary>The color button for the color name.</summary>
    public static ToggleButton ColorButton(DependencyObject root, string name) => (ToggleButton)Part(root, ColorPrefix + name);

    /// <summary>The value the color's button paints, as "#rrggbb".</summary>
    public static string SwatchOf(DependencyObject root, string name) => Hex(ColorButton(root, name).Background);

    /// <summary>The name of the picked color, or null when none is picked.</summary>
    public static string? PickedColor(DependencyObject root) =>
        ColorButtons(root).SingleOrDefault(color => color.Button.IsChecked == true).Name;

    /// <summary>Where the color's button sits in the grid, as its top-left corner relative to the grid.</summary>
    public static Point PositionOf(DependencyObject root, string name)
    {
        var grid = Part(root, ColorGrid);
        Layout(root);
        return ColorButton(root, name).TranslatePoint(new Point(0, 0), grid);
    }

    /// <summary>The element's bounds relative to <paramref name="root"/>, after layout.</summary>
    public static Rect BoundsIn(UIElement root, FrameworkElement element)
    {
        root.UpdateLayout();
        return new Rect(element.TranslatePoint(new Point(0, 0), root), new Size(element.ActualWidth, element.ActualHeight));
    }

    /// <summary>The closest panel that holds the element, the one that lays it out among its siblings.</summary>
    public static Panel PanelOf(DependencyObject element)
    {
        Layout(element);
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is Panel panel)
            {
                return panel;
            }
        }

        throw new InvalidOperationException("The element is not in a panel.");
    }

    /// <summary>The name of the element with an automation id that holds the keyboard focus now, found by walking up from it, or null.</summary>
    public static string? FocusedId()
    {
        for (var element = System.Windows.Input.Keyboard.FocusedElement as DependencyObject; element is not null;
             element = ParentOf(element))
        {
            var id = AutomationProperties.GetAutomationId(element);
            if (id.Length > 0)
            {
                return id;
            }
        }

        return null;
    }

    private static FrameworkElement Part(DependencyObject root, string id)
    {
        Layout(root);
        return TextFields.Find(root, id);
    }

    private static IEnumerable<FrameworkElement> WithIdPrefix(DependencyObject root, string prefix)
    {
        Layout(root);
        return Visuals(root).OfType<FrameworkElement>().Where(element => IdOf(element).StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string IdOf(DependencyObject element) => AutomationProperties.GetAutomationId(element);

    private static string Hex(Brush? brush) =>
        brush is SolidColorBrush { Color: var c } ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : throw new InvalidOperationException("Not a solid color.");

    private static void Layout(DependencyObject element)
    {
        if (element is UIElement ui)
        {
            ui.UpdateLayout();
        }
    }

    // Everything below the element in the visual tree, in the order it shows, plus the element's logical descendants that have no visual yet.
    private static IEnumerable<DependencyObject> Visuals(DependencyObject parent)
    {
        if (parent is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (var inner in Visuals(child))
                {
                    yield return inner;
                }
            }
        }
    }

    private static DependencyObject? ParentOf(DependencyObject element) =>
        (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);
}
