using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The menu the pill opens, drawn from <see cref="TagMenuModel"/>: a popup under the pill with the top tags as colored
/// badges, Search, and Custom…, and a second popup beside Search for its matches. It takes its colors from the theme's
/// resources and never takes the keyboard from the pill unless the cursor goes into the Search box.
/// </summary>
internal sealed class TagMenu : Popup
{
    private const string Text = "TextFillColorPrimaryBrush";
    private const string DimmedText = "TextFillColorTertiaryBrush";

    private readonly TagMenuModel _model;
    private readonly TagLook _look;
    private readonly UIElement _owner;
    private readonly Border _frame = PopupFrame.Make();
    private readonly Border _submenuFrame = PopupFrame.Make();
    private readonly StackPanel _items = new() { MinWidth = 180 };
    private readonly StackPanel _tags = new();
    private readonly Separator _separator = new();
    private readonly Border _searchRow = new() { BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent };
    private readonly TextBox _search = new() { MinWidth = 120 };
    private readonly Button _custom = new()
    {
        Template = TagBadgeVisuals.FlatButton,
        Content = "Custom…",
        BorderThickness = new Thickness(2),
        BorderBrush = Brushes.Transparent,
        Padding = new Thickness(8, 2, 8, 2),
        HorizontalAlignment = HorizontalAlignment.Left,
        Focusable = false,
    };
    private readonly Popup _submenu = new() { Placement = PlacementMode.Right, StaysOpen = true };
    private readonly StackPanel _matches = new();
    private List<string>? _drawnMatches;
    private readonly KeyMap _searchKeys = new();

    public TagMenu(TagMenuModel model, TagLook look, UIElement owner)
    {
        _model = model;
        _look = look;
        _owner = owner;
        Placement = PlacementMode.Bottom;
        PlacementTarget = owner;
        StaysOpen = true;

        AutomationProperties.SetAutomationId(_items, "TagMenu");
        AutomationProperties.SetAutomationId(_search, "MenuSearch");
        AutomationProperties.SetAutomationId(_custom, "MenuCustom");
        AutomationProperties.SetAutomationId(_matches, "SearchMatches");
        _custom.SetResourceReference(Control.ForegroundProperty, Text);
        _custom.Click += (_, _) => model.ChooseCustom();

        var label = new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        label.SetResourceReference(TextBlock.ForegroundProperty, Text);
        var row = new DockPanel { Margin = new Thickness(2) };
        DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label);
        row.Children.Add(_search);
        _searchRow.Child = row;

        _items.Children.Add(_tags);
        _items.Children.Add(_separator);
        _items.Children.Add(_searchRow);
        _items.Children.Add(_custom);
        _items.Children.Add(_submenu);
        _frame.Child = _items;
        Child = _frame;

        _submenu.PlacementTarget = _searchRow;
        _submenu.Child = _submenuFrame;
        _submenuFrame.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = SystemParameters.WorkArea.Height,
            Content = _matches,
        };

        _searchKeys
            .On(ModifierKeys.None, Key.Down, model.MoveDown)
            .On(ModifierKeys.None, Key.Up, model.MoveUp)
            .On(ModifierKeys.None, Key.Enter, model.Enter);
        _search.PreviewKeyDown += (_, e) => e.Handled = _searchKeys.TryRun(Keyboard.Modifiers, e.Key);
        _search.GotKeyboardFocus += (_, _) => model.FocusSearch();
        _search.TextChanged += (_, _) =>
        {
            if (model.IsOpen && model.SearchText != _search.Text)
            {
                model.SearchText = _search.Text;
            }
        };

        model.Changed += Render;
        look.Follow(_items, Fill);
    }

    /// <summary>Whether the element is part of the menu, its submenu, or the pill that opens it.</summary>
    public bool Covers(DependencyObject? element)
    {
        for (; element is not null; element = ParentOf(element))
        {
            if (ReferenceEquals(element, _frame) || ReferenceEquals(element, _submenuFrame) || ReferenceEquals(element, _owner))
            {
                return true;
            }
        }

        return false;
    }

    private void Render()
    {
        // The popup takes the cursor with it when it closes, so look before closing it.
        var cursorInside = _frame.IsKeyboardFocusWithin;
        // A closed popup keeps showing its content until the dispatcher next runs, so the content hides at once too.
        _frame.Visibility = _model.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        _submenuFrame.Visibility = _model.IsSubmenuOpen ? Visibility.Visible : Visibility.Collapsed;
        IsOpen = _model.IsOpen;
        _submenu.IsOpen = _model.IsSubmenuOpen;
        if (_search.Text != _model.SearchText)
        {
            _search.Text = _model.SearchText;
            _search.CaretIndex = _search.Text.Length;
        }

        if (!_model.IsOpen)
        {
            _drawnMatches = null;
            if (cursorInside)
            {
                KeyboardFocus.MoveTo(_owner);
            }

            return;
        }

        Fill(keepMatchButtons: true);
        if (_model.FocusedIndex == _model.SearchIndex)
        {
            if (!_search.IsKeyboardFocused)
            {
                KeyboardFocus.MoveTo(_search);
            }
        }
        else if (_search.IsKeyboardFocused)
        {
            KeyboardFocus.MoveTo(_owner);
        }
    }

    // The items as the model has them now, each tag in its color, the item the keys are on marked.
    private void Fill() => Fill(keepMatchButtons: false);

    private void Fill(bool keepMatchButtons)
    {
        if (!_model.IsOpen)
        {
            _drawnMatches = null;
            return;
        }

        _tags.Children.Clear();
        for (var i = 0; i < _model.TopTags.Count; i++)
        {
            var tag = _model.TopTags[i];
            _tags.Children.Add(TagButton(tag, "MenuTag ", i == _model.FocusedIndex, () => _model.ChooseTag(tag)));
        }

        _separator.Visibility = _model.TopTags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchRow.BorderBrush = Marker(_model.FocusedIndex == _model.SearchIndex);
        _custom.BorderBrush = Marker(_model.FocusedIndex == _model.CustomIndex);
        FillMatches(keepMatchButtons);
    }

    // The matches as the model has them. Moving the highlight over the same matches only moves the marker and keeps the
    // scroll; any other change, or a new look, draws the buttons again.
    private void FillMatches(bool keepButtons = false)
    {
        if (keepButtons && _model.Matches.Count > 0 && _drawnMatches is not null && _drawnMatches.SequenceEqual(_model.Matches))
        {
            for (var i = 0; i < _matches.Children.Count; i++)
            {
                var marked = i == _model.HighlightedMatch;
                ((Button)_matches.Children[i]).BorderBrush = Marker(marked);
                if (marked)
                {
                    ((Button)_matches.Children[i]).BringIntoView();
                }
            }

            return;
        }

        _drawnMatches = [.. _model.Matches];
        _matches.Children.Clear();
        if (_model.ShowsNoMatch)
        {
            var none = new TextBlock { Text = "No matching tags", Margin = new Thickness(8, 2, 8, 2) };
            none.SetResourceReference(TextBlock.ForegroundProperty, DimmedText);
            AutomationProperties.SetAutomationId(none, "SearchNoMatch");
            _matches.Children.Add(none);
            return;
        }

        for (var i = 0; i < _model.Matches.Count; i++)
        {
            var (tag, position) = (_model.Matches[i], i);
            _matches.Children.Add(TagButton(tag, "SearchMatch ", i == _model.HighlightedMatch, () => _model.ChooseMatch(position)));
        }
    }

    private Button TagButton(string tag, string idPrefix, bool marked, Action chosen)
    {
        var button = TagBadgeVisuals.ButtonFor(tag, _look.FillOf(tag));
        AutomationProperties.SetAutomationId(button, idPrefix + tag);
        button.BorderThickness = new Thickness(2);
        button.BorderBrush = Marker(marked);
        button.Focusable = false;
        button.Click += (_, _) => chosen();
        return button;
    }

    private static Brush Marker(bool marked) => marked ? SystemColors.HighlightBrush : Brushes.Transparent;

    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
