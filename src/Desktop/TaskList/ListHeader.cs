using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The top of the list: the search box, the tag filter, the message line, and the Undo bar. It shows the
/// <see cref="ListFilter"/> and puts what the user types or chooses into it.
/// </summary>
internal sealed class ListHeader : StackPanel
{
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 8, 0), MinWidth = 200 };
    private readonly ComboBox _allTags = new() { MinWidth = 140, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _clearTag = new() { Content = "Clear tag", Visibility = Visibility.Collapsed };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly TextBlock _undoLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Border _undoBar = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(8) };
    private readonly ListFilter _filter;
    private bool _syncing;
    private string? _reChosen;

    public ListHeader(ITasks tasks, ITagColors tagColors, ITheme theme, ListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(filter);
        _filter = filter;
        _message.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        _undoBar.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        AutomationProperties.SetName(_search, "Search");
        AutomationProperties.SetName(_allTags, "All tags");
        _allTags.ItemTemplate = TagBadgeTemplate.For(tagColors, theme);
        var chips = new TopTags(tasks, tagColors, theme);
        AutomationProperties.SetName(chips, "Tag filter");
        chips.TagChosen += (_, chosen) => _filter.ToggleTag(chosen);
        _allTags.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _allTags.SelectedItem is string chosen)
            {
                _reChosen = null; // The selection changed, so this commit is handled here, once.
                _filter.ToggleTag(chosen);
            }
        };
        // Choosing the tag that is already chosen changes no selection, so no SelectionChanged says so. Note the choice when the
        // mouse is released on that item, or a key commits it, and apply it once the list has closed (Escape closes it unnoted).
        _allTags.DropDownOpened += (_, _) => _reChosen = null;
        _allTags.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (_allTags.IsDropDownOpen && e.OriginalSource is DependencyObject source &&
                ItemsControl.ContainerFromElement(_allTags, source) is ComboBoxItem item)
            {
                NoteIfAlreadyChosen(_allTags.ItemContainerGenerator.ItemFromContainer(item));
            }
        }), handledEventsToo: true);
        _allTags.PreviewKeyDown += (_, e) =>
        {
            if (_allTags.IsDropDownOpen && CommitsSelection(e))
            {
                // Home and Down move only the highlight; the selection stays on the chosen tag until a key commits.
                NoteIfAlreadyChosen(_allTags.Items.Cast<object>().FirstOrDefault(tag =>
                    _allTags.ItemContainerGenerator.ContainerFromItem(tag) is ComboBoxItem { IsHighlighted: true }) ?? _allTags.SelectedItem);
            }
        };
        _allTags.DropDownClosed += (_, _) =>
        {
            if (_reChosen is { } tag)
            {
                _reChosen = null;
                _filter.ToggleTag(tag);
            }
        };
        _clearTag.Click += (_, _) => _filter.ClearTag();
        _search.TextChanged += (_, _) => _filter.SearchText = _search.Text;
        _filter.Changed += (_, _) => ShowFilter();

        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        filters.Children.Add(_search);
        filters.Children.Add(chips);
        filters.Children.Add(_allTags);
        filters.Children.Add(_clearTag);

        var undo = new Button { Content = "Undo" };
        AutomationProperties.SetName(undo, "Undo");
        undo.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Children.Add(_undoLabel);
        bar.Children.Add(undo);
        _undoBar.Child = bar;

        Margin = new Thickness(8);
        Children.Add(filters);
        Children.Add(_message);
        Children.Add(_undoBar);
    }

    public event EventHandler? UndoClicked;

    /// <summary>Closes the all-tags list and returns true, or returns false, doing nothing, when it is not open.</summary>
    public bool CloseAllTagsList()
    {
        if (!_allTags.IsDropDownOpen)
        {
            return false;
        }

        _allTags.IsDropDownOpen = false;
        return true;
    }

    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
    }

    /// <summary>Lists every tag in use in the all-tags list.</summary>
    public void ShowTags(IReadOnlyList<string> tags)
    {
        _syncing = true;
        _allTags.ItemsSource = tags;
        _allTags.SelectedItem = _filter.TagFilter;
        _syncing = false;
    }

    /// <summary>Shows the message, or hides the line when it is null or empty.</summary>
    public void ShowMessage(string? message)
    {
        _message.Text = message ?? "";
        _message.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the Undo bar with what it undoes, or hides it when <paramref name="label"/> is null.</summary>
    public void ShowUndo(string? label)
    {
        _undoLabel.Text = label ?? "";
        _undoBar.Visibility = label is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // The keys that close an open combo box list and commit the highlighted item, as the native ComboBox reads them.
    private static bool CommitsSelection(KeyEventArgs e)
    {
        var alt = (e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0;
        return EventKey.Of(e) switch
        {
            Key.Enter => true,
            Key.F4 => !alt,
            Key.Up or Key.Down => alt,
            _ => false,
        };
    }

    private void NoteIfAlreadyChosen(object? item) => _reChosen = item is string tag && _filter.IsChosen(tag) ? tag : null;

    // Shows the filter as it now is, whoever changed it: the user here, or a followed link.
    private void ShowFilter()
    {
        if (_search.Text != _filter.SearchText)
        {
            _search.Text = _filter.SearchText;
        }

        var tag = _filter.TagFilter;
        _clearTag.Visibility = tag is null ? Visibility.Collapsed : Visibility.Visible;
        _syncing = true;
        _allTags.SelectedItem = tag;
        _syncing = false;
    }
}
