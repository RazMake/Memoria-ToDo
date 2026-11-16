using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The top of the list: the search band (the "Search:" label, the search box, and its button, edge to edge in the status bar's
/// shade), and under it the message line and the Undo bar. It shows the <see cref="ListFilter"/>. The search runs only when
/// the user presses Enter in the box or clicks the button.
/// </summary>
internal sealed class ListHeader : StackPanel
{
    private readonly TextBox _search = new() { Margin = new Thickness(0, 0, 4, 0), MinWidth = 160, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(8, 8, 8, 0) };
    private readonly TextBlock _undoLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Border _undoBar = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(8), Margin = new Thickness(8, 8, 8, 0) };
    private readonly ListFilter _filter;

    public ListHeader(ListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filter = filter;
        _message.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        _undoBar.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        AutomationProperties.SetName(_search, "Search");
        _search.PreviewKeyDown += (_, e) =>
        {
            if (EventKey.Of(e) == Key.Enter)
            {
                e.Handled = true;
                RunSearch();
            }
        };
        _filter.Changed += (_, _) => ShowFilter();

        var runSearch = new Button
        {
            Content = "\uE721",
            FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            ToolTip = "Search (Enter)",
        };
        AutomationProperties.SetName(runSearch, "Run search");
        runSearch.Click += (_, _) => RunSearch();

        // The search box takes the width the label and the button leave.
        var label = new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_search, 1);
        Grid.SetColumn(runSearch, 2);
        row.Children.Add(label);
        row.Children.Add(_search);
        row.Children.Add(runSearch);
        var band = new Border { Padding = new Thickness(8, 4, 8, 4), Child = row };
        band.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");

        var undo = new Button { Content = "Undo" };
        AutomationProperties.SetName(undo, "Undo");
        undo.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Children.Add(_undoLabel);
        bar.Children.Add(undo);
        _undoBar.Child = bar;

        Children.Add(band);
        Children.Add(_message);
        Children.Add(_undoBar);
    }

    public event EventHandler? UndoClicked;

    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
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

    private void RunSearch() => _filter.SearchText = _search.Text;

    // Shows the filter as it now is, whoever changed it: the user here, or a followed link.
    private void ShowFilter()
    {
        if (_search.Text != _filter.SearchText)
        {
            _search.Text = _filter.SearchText;
        }
    }
}
