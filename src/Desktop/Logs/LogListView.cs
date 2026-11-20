using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// The list of rows, or the text that stands in its place, such as "Loading…". Only the rows on screen are drawn, so the list
/// stays smooth through a whole log. Each row is named by its message's first line for screen readers. Ctrl+C asks for the
/// selected rows to be copied.
/// </summary>
internal sealed class LogListView : Grid
{
    private readonly Rows _list = new();
    private readonly TextBlock _message = new()
    {
        Margin = new Thickness(12),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed,
    };

    public LogListView()
    {
        var columns = new GridView();
        AddColumn(columns, "Time", nameof(LogRow.TimeText), 170);
        AddColumn(columns, "Severity", nameof(LogRow.SeverityCell), 100);
        AddColumn(columns, "Component", nameof(LogRow.Component), 130);
        AddColumn(columns, "Message", nameof(LogRow.Message), 900);
        _list.View = columns;
        _list.SelectionMode = SelectionMode.Extended;
        AutomationProperties.SetName(_list, "Log entries");
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        VirtualizingPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(_list, ScrollUnit.Pixel);
        _list.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        _list.CommandBindings.Add(new CommandBinding(
            ApplicationCommands.Copy,
            (_, _) => CopyRequested?.Invoke(this, Selected),
            (_, e) => e.CanExecute = _list.SelectedItems.Count > 0));
        Children.Add(_list);
        Children.Add(_message);
    }

    /// <summary>Raised when the selection changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised when the user copies, with the selected rows in the order the list shows them.</summary>
    public event EventHandler<IReadOnlyList<LogRow>>? CopyRequested;

    public int SelectedCount => _list.SelectedItems.Count;

    /// <summary>The selected rows in the order the list shows them, newest first.</summary>
    public IReadOnlyList<LogRow> Selected => [.. _list.SelectedItems.Cast<LogRow>().Order(LogRow.NewestFirst)];

    /// <summary>Shows the rows, or the message instead of them when there is one.</summary>
    public void Show(IReadOnlyList<LogRow> rows, string? message)
    {
        _message.Text = message ?? "";
        _message.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        _list.Visibility = message is null ? Visibility.Visible : Visibility.Collapsed;
        if (!ReferenceEquals(_list.ItemsSource, rows))
        {
            _list.ItemsSource = rows;
        }
    }

    /// <summary>Selects the first row, or the last, and puts the focus on it. Does nothing when no row shows.</summary>
    public void SelectEdgeRow(bool last)
    {
        if (_list.Items.Count == 0)
        {
            return;
        }

        var at = last ? _list.Items.Count - 1 : 0;
        _list.SelectedIndex = at;
        _list.ScrollIntoView(_list.Items[at]);
        (_list.ItemContainerGenerator.ContainerFromIndex(at) as IInputElement)?.Focus();
    }

    private static void AddColumn(GridView columns, string header, string property, double width) =>
        columns.Columns.Add(new GridViewColumn { Header = header, Width = width, DisplayMemberBinding = new Binding(property) });

    // Names each row after its message for screen readers; the rows have no template of their own to name.
    private sealed class Rows : ListView
    {
        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            AutomationProperties.SetAutomationId(element, "Row");
            AutomationProperties.SetName(element, ((LogRow)item).Message);
        }
    }
}
