using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// The status bar Add and Task View show along their bottom edge. <c>start</c>, when given, sits at the bar's start,
/// and the bar always shows; without it, the bar shows only while a version is available or updating.
/// </summary>
public sealed class UpdateStatusBar : Border
{
    private readonly IUpdateStatus _updates;
    private readonly UIElement? _start;
    private readonly DockPanel _panel = new() { LastChildFill = false };
    private UIElement? _updateText;

    public UpdateStatusBar(IUpdateStatus updates, UIElement? start)
    {
        ArgumentNullException.ThrowIfNull(updates);
        _updates = updates;
        _start = start;
        Padding = new Thickness(8, 4, 8, 4);
        Child = _panel;
        if (start is not null)
        {
            DockPanel.SetDock(start, Dock.Left);
            _panel.Children.Add(start);
        }

        Refresh();
        updates.Changed += OnChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => updates.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _updates.Changed -= OnChanged; // So that a bar loaded again isn't told twice.
        _updates.Changed += OnChanged;
        Refresh();
    }

    private void Refresh()
    {
        if (_updateText is not null)
        {
            _panel.Children.Remove(_updateText);
        }

        var state = _updates.State;
        _updateText = state.Stage switch
        {
            UpdateStage.Available => AvailableButton(state.NewVersion!),
            UpdateStage.Updating => TextOf("TextFillColorSecondaryBrush", ("Updating to ", false), (state.NewVersion!, true)),
            _ => null,
        };
        if (_updateText is not null)
        {
            DockPanel.SetDock(_updateText, Dock.Right);
            _panel.Children.Add(_updateText);
        }

        Visibility = _start is not null || _updateText is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button AvailableButton(string version)
    {
        var button = new Button
        {
            Content = TextOf("SystemFillColorCautionBrush", ("Update available", true), ($": {version}", false)),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        button.Click += (_, _) => _updates.UpdateNow();
        return button;
    }

    // The runs' color is a theme resource reference, so it follows the theme.
    private static TextBlock TextOf(string colorKey, params (string Text, bool Bold)[] parts)
    {
        var block = new TextBlock();
        foreach (var (text, bold) in parts)
        {
            var run = new Run(text);
            if (bold)
            {
                run.FontWeight = FontWeights.Bold;
            }

            run.SetResourceReference(TextElement.ForegroundProperty, colorKey);
            block.Inlines.Add(run);
        }

        return block;
    }
}
