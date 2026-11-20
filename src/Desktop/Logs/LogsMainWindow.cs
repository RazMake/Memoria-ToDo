using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// The Logs window: the search band and the filter bar on top, the list in the middle, the selected entry's whole text below it,
/// and the status line at the bottom. It shows what its <see cref="LogsModel"/> holds after every change.
/// </summary>
internal sealed class LogsMainWindow : Window
{
    // The details pane opens at its smallest; the user drags the splitter to grow it.
    private const double DetailsMinHeight = 65;

    private readonly LogsModel _model;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _closing = new();
    private readonly SearchBand _search = new();
    private readonly LogFilterBar _filters;
    private readonly MessageLine _message = new() { Margin = new Thickness(8, 4, 8, 0) };
    private readonly LogListView _list;
    private readonly TextBox _details = new()
    {
        IsReadOnly = true,
        IsReadOnlyCaretVisible = true,
        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        BorderThickness = new Thickness(0),
    };
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4, 8, 4), TextTrimming = TextTrimming.CharacterEllipsis };

    public LogsMainWindow(ILogSource source, ITheme theme, TimeProvider time, ILogger logger)
    {
        _logger = logger;
        _model = new LogsModel(source, time, logger);
        _filters = new LogFilterBar(_model);
        _list = new LogListView(theme);

        Title = "Logs";
        Width = 1100;
        Height = 720;
        MinWidth = 640;
        MinHeight = 400;
        AutomationProperties.SetName(_details, "Details");

        var top = new StackPanel();
        top.Children.Add(_search);
        top.Children.Add(_filters);
        top.Children.Add(_message);
        var bottom = new Border { Child = _status };
        bottom.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        var layout = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        layout.Children.Add(top);
        layout.Children.Add(bottom);
        layout.Children.Add(ListAndDetails());
        Content = layout;

        _search.TextChanged += (_, _) =>
        {
            _model.SearchText = _search.Text;
            Render();
        };
        _search.EdgeRowRequested += (_, last) => _list.SelectEdgeRow(last);
        _filters.Changed += (_, _) => Render();
        _filters.RefreshRequested += (_, _) => Reload();
        _list.SelectionChanged += (_, _) => ShowDetails();
        _list.CopyRequested += (_, rows) => Copy(rows);
        Loaded += (_, _) => _search.FocusText();
        PreviewKeyDown += (_, e) =>
        {
            if (EventKey.Of(e) == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                Reload();
            }
        };
        EscCloses.Attach(this);
        EventHandler themeChanged = (_, _) => _list.ShowThemeColors();
        theme.Changed += themeChanged;
        Closed += (_, _) =>
        {
            theme.Changed -= themeChanged;
            _closing.Cancel();
            _closing.Dispose();
        };
        Render();
    }

    /// <summary>Reads the log again, showing "Loading…" meanwhile and the entries when done. Does nothing while a read runs.</summary>
    public async void Reload()
    {
        if (_model.IsLoading)
        {
            return;
        }

        try
        {
            var load = _model.LoadAsync(_closing.Token);
            Render();
            await load;
            Render();
        }
        catch (OperationCanceledException)
        {
            // The window closed while it read; there is nothing to show.
        }
    }

    private Grid ListAndDetails()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DetailsMinHeight), MinHeight = DetailsMinHeight });
        var splitter = new GridSplitter
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Focusable = false,
            IsTabStop = false,
        };
        splitter.SetResourceReference(BackgroundProperty, "CardStrokeColorDefaultBrush");
        Grid.SetRow(_list, 0);
        Grid.SetRow(splitter, 1);
        Grid.SetRow(_details, 2);
        grid.Children.Add(_list);
        grid.Children.Add(splitter);
        grid.Children.Add(_details);
        return grid;
    }

    private void Render()
    {
        _filters.Show();
        _list.Show(_model.Rows, _model.ListMessage);
        _status.Text = _model.StatusText;
        ShowDetails();
    }

    private void ShowDetails() =>
        _details.Text = _list.SelectedCount switch
        {
            0 => "",
            1 => _list.Selected[0].Details,
            var count => $"{count} entries selected. Press Ctrl+C to copy them.",
        };

    private void Copy(IReadOnlyList<LogRow> rows)
    {
        try
        {
            Clipboard.SetText(_model.CopyText(rows));
            _message.Show(null);
        }
        catch (COMException failure)
        {
            // Another program holds the clipboard.
            _logger.Warning(failure, "The selected log entries could not be copied");
            _message.Show($"Couldn't copy: {failure.Message}");
        }
    }
}
