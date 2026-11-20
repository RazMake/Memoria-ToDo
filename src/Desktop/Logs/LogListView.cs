using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// The list of rows, or the text that stands in its place, such as "Loading…". Only the rows on screen are drawn, so the list
/// stays smooth through a whole log. Each row is named by its message's first line for screen readers. Ctrl+C asks for the
/// selected rows to be copied.
/// </summary>
internal sealed class LogListView : Grid
{
    private readonly Rows _list;
    private readonly TextBlock _message = new()
    {
        Margin = new Thickness(12),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed,
    };

    public LogListView(ITheme theme)
    {
        _list = new Rows(theme)
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            ItemContainerStyle = RowStyle(),
        };
        _list.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        var columns = new GridView { ColumnHeaderContainerStyle = HeaderStyle() };
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

    /// <summary>Paints the rows on screen again in the theme's severity colors, after the theme switched.</summary>
    public void ShowThemeColors() => _list.Items.Refresh();

    private static void AddColumn(GridView columns, string header, string property, double width) =>
        columns.Columns.Add(new GridViewColumn { Header = header, Width = width, DisplayMemberBinding = new Binding(property) });

    // The Fluent theme has no look for a list with columns, so the header and the rows get theirs here.
    private static Style HeaderStyle()
    {
        var text = new FrameworkElementFactory(typeof(ContentPresenter));
        text.SetValue(MarginProperty, new Thickness(8, 6, 8, 6));
        text.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        text.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        frame.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        frame.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 1, 1));
        frame.AppendChild(text);

        var grip = new FrameworkElementFactory(typeof(Border));
        grip.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var gripper = new FrameworkElementFactory(typeof(Thumb), "PART_HeaderGripper");
        gripper.SetValue(WidthProperty, 8.0);
        gripper.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Right);
        gripper.SetValue(MarginProperty, new Thickness(0, 0, -4, 0));
        gripper.SetValue(CursorProperty, Cursors.SizeWE);
        gripper.SetValue(Control.TemplateProperty, new ControlTemplate(typeof(Thumb)) { VisualTree = grip });

        var root = new FrameworkElementFactory(typeof(Grid));
        root.AppendChild(frame);
        root.AppendChild(gripper);
        return new Style(typeof(GridViewColumnHeader))
        {
            Setters =
            {
                new Setter(Control.TemplateProperty, new ControlTemplate(typeof(GridViewColumnHeader)) { VisualTree = root }),
                new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CardBackgroundFillColorDefaultBrush")),
                new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorSecondaryBrush")),
            },
        };
    }

    private static Style RowStyle()
    {
        const string Back = "Back";
        const string Mark = "Mark";
        var back = new FrameworkElementFactory(typeof(Border), Back);
        back.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        back.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var mark = new FrameworkElementFactory(typeof(Border), Mark);
        mark.SetValue(WidthProperty, 3.0);
        mark.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        mark.SetValue(MarginProperty, new Thickness(0, 4, 0, 4));
        mark.SetValue(Border.CornerRadiusProperty, new CornerRadius(1.5));
        mark.SetValue(VisibilityProperty, Visibility.Collapsed);
        mark.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
        var cells = new FrameworkElementFactory(typeof(GridViewRowPresenter));
        cells.SetValue(GridViewRowPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        cells.SetValue(GridViewRowPresenterBase.ColumnsProperty, new TemplateBindingExtension(GridView.ColumnCollectionProperty));
        cells.SetValue(MarginProperty, new Thickness(0, 3, 0, 3));
        var root = new FrameworkElementFactory(typeof(Grid));
        root.AppendChild(back);
        root.AppendChild(mark);
        root.AppendChild(cells);

        var template = new ControlTemplate(typeof(ListViewItem)) { VisualTree = root };
        template.Triggers.Add(new Trigger
        {
            Property = IsMouseOverProperty,
            Value = true,
            Setters = { new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SubtleFillColorSecondaryBrush"), Back) },
        });
        template.Triggers.Add(new Trigger
        {
            Property = ListBoxItem.IsSelectedProperty,
            Value = true,
            Setters =
            {
                new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SubtleFillColorSecondaryBrush"), Back),
                new Setter(VisibilityProperty, Visibility.Visible, Mark),
            },
        });
        return new Style(typeof(ListViewItem))
        {
            Setters =
            {
                new Setter(Control.TemplateProperty, template),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(FocusVisualStyleProperty, null),
            },
        };
    }

    // Names each row after its message for screen readers, and paints errors red and warnings orange.
    private sealed class Rows(ITheme theme) : ListView
    {
        private static readonly Brush DarkError = Paint(0xFF, 0x6B, 0x6B);
        private static readonly Brush DarkWarning = Paint(0xFF, 0xA5, 0x3D);
        private static readonly Brush LightError = Paint(0xC4, 0x2B, 0x1C);
        private static readonly Brush LightWarning = Paint(0xC2, 0x58, 0x00);

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            var row = (LogRow)item;
            AutomationProperties.SetAutomationId(element, "Row");
            AutomationProperties.SetName(element, row.Message);
            var dark = theme.IsDark;
            var brush = row.Entry.Level switch
            {
                >= LogEventLevel.Error => dark ? DarkError : LightError,
                LogEventLevel.Warning => dark ? DarkWarning : LightWarning,
                _ => null,
            };
            if (brush is null)
            {
                element.ClearValue(ForegroundProperty);
            }
            else
            {
                element.SetValue(ForegroundProperty, brush);
            }
        }

        private static SolidColorBrush Paint(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
