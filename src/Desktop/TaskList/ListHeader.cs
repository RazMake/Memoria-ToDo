using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The top of the list: the search band (the "Search:" label and the search box, edge to edge in the status bar's
/// shade), and under it the message line and the Undo bar. It shows the <see cref="ListFilter"/>, which follows the box as the user types.
/// </summary>
internal sealed class ListHeader : StackPanel
{
    private readonly SearchBand _search = new();
    private readonly MessageLine _message = new() { Margin = new Thickness(8, 8, 8, 0) };
    private readonly TextBlock _undoLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Border _undoBar = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(8), Margin = new Thickness(8, 8, 8, 0) };
    private readonly ListFilter _filter;

    public ListHeader(ListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filter = filter;
        _undoBar.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        _search.TextChanged += (_, _) => _filter.SearchText = _search.Text;
        _search.EdgeRowRequested += (_, last) => EdgeRowRequested?.Invoke(this, last);
        _filter.Changed += (_, _) => ShowFilter();

        var undo = new Button { Content = "Undo" };
        AutomationProperties.SetName(undo, "Undo");
        undo.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Children.Add(_undoLabel);
        bar.Children.Add(undo);
        _undoBar.Child = bar;

        Children.Add(_search);
        Children.Add(_message);
        Children.Add(_undoBar);
    }

    public event EventHandler? UndoClicked;

    /// <summary>Raised on Down (false) or Up (true) in the search box, to select the top or bottom row.</summary>
    public event EventHandler<bool>? EdgeRowRequested;

    public void FocusSearch() => _search.FocusText();

    /// <summary>Shows the message, or hides the line when it is null or empty.</summary>
    public void ShowMessage(string? message) => _message.Show(message);

    /// <summary>Shows the Undo bar with what it undoes, or hides it when <paramref name="label"/> is null.</summary>
    public void ShowUndo(string? label)
    {
        _undoLabel.Text = label ?? "";
        _undoBar.Visibility = label is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // Shows the filter as it now is, whoever changed it: the user here, or a followed link.
    private void ShowFilter()
    {
        if (_search.Text != _filter.SearchText)
        {
            _search.Text = _filter.SearchText;
        }
    }
}
