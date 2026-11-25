using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// How tests find and read the tag parts, by automation id, so they don't depend on the controls the parts are built from.
/// A badge (read-only, in TagBadges and a group heading) is the element with id <c>Badge {tag}</c>: its background is the
/// color's value and the text block in it has the color's text color. The tag control is a row of pills. The pill of a tag is
/// the focusable button with id <c>TagPill {tag}</c>: it holds one text block, the tag, and paints the tag's color. Its x is a
/// separate focusable button with id <c>ClearTag {tag}</c>. After them comes the add pill, the focusable button with id
/// <c>AddTagPill</c>, which says "No tag" or "+". The pills are in the order they show, left to right and line by line, whatever
/// order the controls were added in. The menu is a
/// popup whose content has id <c>TagMenu</c> and is showing only while open; its items are buttons with ids <c>MenuTag {tag}</c>
/// and <c>MenuCustom</c>, and the Search box, a text box labeled "Search:", has id <c>MenuSearch</c>. The matches submenu is a
/// popup whose content has id <c>SearchMatches</c>, showing only while Search holds text; its items are buttons with ids
/// <c>SearchMatch {tag}</c>, or one dimmed text block with id <c>SearchNoMatch</c> and no button. The 30 colors are toggle
/// buttons in <c>TagColorGrid</c> with ids <c>Color {name}</c>, checked when marked, and the grid shows only while the New
/// tag box (id <c>Tags</c>) holds text. Shared test setup.
/// </summary>
internal static class TagControls
{
    public const string AddPillId = EditorFields.AddTagPill;
    public const string Menu = "TagMenu";
    public const string MenuCustom = "MenuCustom";
    public const string MenuSearch = "MenuSearch";
    public const string SearchMatchesList = "SearchMatches";
    public const string SearchNoMatch = "SearchNoMatch";
    public const string ColorGrid = "TagColorGrid";

    private const string BadgePrefix = "Badge ";
    private const string PillPrefix = EditorFields.TagPill + " ";
    private const string ClearPrefix = EditorFields.ClearTag + " ";
    private const string RemovePrefix = "Remove ";
    private const string MenuTagPrefix = "MenuTag ";
    private const string SearchMatchPrefix = "SearchMatch ";
    private const string ColorPrefix = "Color ";

    /// <summary>The tags of the badges below <paramref name="root"/>, in the order they show.</summary>
    public static IReadOnlyList<string> BadgeTags(DependencyObject root) =>
        [.. WithIdPrefix(root, BadgePrefix).Select(element => IdOf(element)[BadgePrefix.Length..])];

    /// <summary>The badge for the tag.</summary>
    public static FrameworkElement Badge(DependencyObject root, string tag) => Part(root, BadgePrefix + tag);

    /// <summary>The text block in the badge that shows the tag's name.</summary>
    public static TextBlock BadgeText(DependencyObject root, string tag) =>
        Visuals(Badge(root, tag)).OfType<TextBlock>().First(text => text.Text == tag);

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

    /// <summary>The tags whose remove buttons exist, in the order they show. A read-only badge list has none.</summary>
    public static IReadOnlyList<string> RemovableTags(DependencyObject root) =>
        [.. WithIdPrefix(root, RemovePrefix).Select(element => IdOf(element)[RemovePrefix.Length..])];

    /// <summary>The tags of the pills below <paramref name="root"/>, in the order the pills show: line by line, left to right.</summary>
    public static IReadOnlyList<string> PillTags(DependencyObject root) =>
    [
        .. WithIdPrefix(root, PillPrefix).OfType<Button>()
            .OrderBy(pill => Math.Round(Corner(root, pill).Y)).ThenBy(pill => Corner(root, pill).X)
            .Select(pill => IdOf(pill)[PillPrefix.Length..]),
    ];

    /// <summary>The id of the tag's pill, the element the keyboard focus is on while the cursor is in that pill.</summary>
    public static string PillId(string tag) => PillPrefix + tag;

    /// <summary>The id of the x of the tag's pill.</summary>
    public static string ClearId(string tag) => ClearPrefix + tag;

    /// <summary>The pill of the tag.</summary>
    public static Button TagPill(DependencyObject root, string tag) => (Button)Part(root, PillPrefix + tag);

    /// <summary>The x of the tag's pill, which removes the tag.</summary>
    public static Button ClearButton(DependencyObject root, string tag) => (Button)Part(root, ClearPrefix + tag);

    /// <summary>The add pill, after the last tag's.</summary>
    public static Button AddPill(DependencyObject root) => (Button)Part(root, AddPillId);

    /// <summary>The words the add pill shows: "No tag" or "+".</summary>
    public static string AddPillText(DependencyObject root) => TextOfPill(AddPill(root));

    /// <summary>The size the pill shows, with any scaling it or its parents apply: the distance between its corners, in the coordinates of <paramref name="root"/>.</summary>
    public static Size ShownSize(DependencyObject root, FrameworkElement element)
    {
        Layout(root);
        var visual = (Visual)root;
        var start = element.TransformToAncestor(visual).Transform(new Point(0, 0));
        var end = element.TransformToAncestor(visual).Transform(new Point(element.ActualWidth, element.ActualHeight));
        return new Size(end.X - start.X, end.Y - start.Y);
    }

    /// <summary>The middle of the element, in the coordinates of <paramref name="root"/>, which is the control the tests hand the fake pointer's positions in.</summary>
    public static Point CenterIn(UIElement root, FrameworkElement element)
    {
        root.UpdateLayout();
        return element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), root);
    }

    /// <summary>The first tag's pill, or the add pill when the task has no tag: the one pill a task with at most one tag has had.</summary>
    public static Button PillButton(DependencyObject root) =>
        PillTags(root) is [var first, ..] ? TagPill(root, first) : AddPill(root);

    /// <summary>The words the first pill shows: the first tag, or the add pill's.</summary>
    public static string PillText(DependencyObject root) => TextOfPill(PillButton(root));

    /// <summary>The x of the first tag's pill. Throws when the task has no tag.</summary>
    public static Button ClearButton(DependencyObject root) => ClearButton(root, PillTags(root)[0]);

    /// <summary>Whether the task has a tag, so its first pill has an x.</summary>
    public static bool HasClearButton(DependencyObject root) => PillTags(root).Count > 0;

    /// <summary>The first tag, or null when the task has none and the add pill shows "No tag".</summary>
    public static string? PillTag(DependencyObject root) => PillTags(root).FirstOrDefault();

    /// <summary>The value and text color the first pill paints now, as "#rrggbb".</summary>
    public static TagFill PillFill(DependencyObject root) => FillOfPill(PillButton(root));

    /// <summary>The value and text color the tag's pill paints now, as "#rrggbb".</summary>
    public static TagFill PillFill(DependencyObject root, string tag) => FillOfPill(TagPill(root, tag));

    private static TagFill FillOfPill(Button pill)
    {
        var text = Visuals(pill).OfType<TextBlock>().FirstOrDefault(block => block.Text.Length > 0)?.Foreground ?? pill.Foreground;
        return new TagFill(Hex(pill.Background), Hex(text));
    }

    private static string TextOfPill(Button pill) =>
        OpenWindows.TextOf(Visuals(pill).OfType<TextBlock>().First(block => block.Text.Length > 0));

    private static Point Corner(DependencyObject root, FrameworkElement element)
    {
        Layout(root);
        return element.TransformToAncestor((Visual)root).Transform(new Point(0, 0));
    }

    /// <summary>Whether the menu is showing.</summary>
    public static bool MenuIsOpen(DependencyObject root) => TextFields.FindShowing(root, Menu) is not null;

    /// <summary>
    /// Whether the popup under the pill is open. A popup WPF closed itself, when its pill left the window, keeps its content
    /// showing, so <see cref="MenuIsOpen"/> still says true; this reads the popup's own state.
    /// </summary>
    public static bool MenuPopupIsOpen(DependencyObject root) =>
        OpenWindows.Descendants(root).OfType<Popup>()
            .Single(popup => popup.PlacementTarget is { } target && IsPill(AutomationProperties.GetAutomationId(target))).IsOpen;

    private static bool IsPill(string id) => id == AddPillId || id.StartsWith(PillPrefix, StringComparison.Ordinal);

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

    /// <summary>The Search text box in the open menu.</summary>
    public static TextBox SearchBox(DependencyObject root) => (TextBox)Part(root, MenuSearch);

    /// <summary>Whether the matches submenu is showing.</summary>
    public static bool SearchMatchesAreOpen(DependencyObject root) => TextFields.FindShowing(root, SearchMatchesList) is not null;

    /// <summary>The tags the open submenu lists as clickable matches, in order; empty while it is closed or shows no match.</summary>
    public static IReadOnlyList<string> SearchMatches(DependencyObject root) =>
        SearchMatchesAreOpen(root)
            ? [.. WithIdPrefix(Part(root, SearchMatchesList), SearchMatchPrefix).Select(element => IdOf(element)[SearchMatchPrefix.Length..])]
            : [];

    /// <summary>The submenu's button for the matching tag.</summary>
    public static Button SearchMatch(DependencyObject root, string tag) => (Button)Part(root, SearchMatchPrefix + tag);

    /// <summary>The tag of the match the keys highlighted, or null when none is. A highlighted match has the system highlight color as its border.</summary>
    public static string? HighlightedSearchMatch(DependencyObject root) =>
        WithIdPrefix(Part(root, SearchMatchesList), SearchMatchPrefix).OfType<Button>()
            .SingleOrDefault(button => button.BorderBrush is SolidColorBrush { Color: var c } && c == SystemColors.HighlightColor) is { } match
            ? IdOf(match)[SearchMatchPrefix.Length..]
            : null;

    /// <summary>Whether the submenu's list is taller than the space it has, so it scrolls.</summary>
    public static bool SearchMatchesScroll(DependencyObject root) => ScrollerOf(Part(root, SearchMatchesList)).ScrollableHeight > 0;

    /// <summary>Whether the whole button of the matching tag is inside the part of the submenu's list that shows.</summary>
    public static bool SearchMatchIsInView(DependencyObject root, string tag)
    {
        var button = SearchMatch(root, tag);
        var scroller = ScrollerOf(button);
        scroller.UpdateLayout();
        var top = button.TranslatePoint(new Point(0, 0), scroller).Y;
        return top >= -0.5 && top + button.ActualHeight <= scroller.ViewportHeight + 0.5;
    }

    private static ScrollViewer ScrollerOf(DependencyObject element)
    {
        for (var parent = element; parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer scroller)
            {
                return scroller;
            }
        }

        throw new InvalidOperationException("The element is not in a scroll viewer.");
    }

    /// <summary>The submenu's dimmed "No matching tags" line while it shows, or null. It is not a button, so it can't be clicked.</summary>
    public static TextBlock? NoMatchLine(DependencyObject root) =>
        TextFields.FindShowing(root, SearchNoMatch) as TextBlock;

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

    /// <summary>Whether the colors are showing, which they do only while the New tag box holds text.</summary>
    public static bool ColorsAreShowing(DependencyObject root) => TextFields.FindShowing(root, ColorGrid) is not null;

    /// <summary>The name of the marked color, the one the typed tag would get with Enter, or null when none is marked.</summary>
    public static string? MarkedColor(DependencyObject root) =>
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

    private static DependencyObject? ParentOf(DependencyObject element) =>
        (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);
}
