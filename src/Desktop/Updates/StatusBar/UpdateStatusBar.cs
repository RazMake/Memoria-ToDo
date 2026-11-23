using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// The status bar Add and Task View show along their bottom edge. <c>start</c>, when given, takes all the width the update
/// text leaves, and the bar always shows; without it, the bar shows only while a version is available or updating.
/// The update text sits at the bar's right end, with a 5 pixel gap on its left. While an update is available the text is a
/// button that opens What is new over the window holding the bar.
/// </summary>
public sealed class UpdateStatusBar : Border
{
    private const double UpdateTextGap = 5;

    private readonly IUpdateStatus _updates;
    private readonly UIElement? _start;
    private readonly DockPanel _panel = new();
    private FrameworkElement? _updateText;

    public UpdateStatusBar(IUpdateStatus updates, UIElement? start)
    {
        ArgumentNullException.ThrowIfNull(updates);
        _updates = updates;
        _start = start;
        Padding = new Thickness(8, 2, 8, 2);
        SetResourceReference(BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        Child = _panel;
        // The last child fills what the update text leaves, so it is the start; without a start, the update text keeps its right edge.
        _panel.LastChildFill = start is not null;
        if (start is not null)
        {
            // The update text is added before the start so the start fills, but Tab reaches the start first.
            KeyboardNavigation.SetTabIndex(start, 0);
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
            _updateText.Margin = new Thickness(UpdateTextGap, 0, 0, 0);
            DockPanel.SetDock(_updateText, Dock.Right);
            KeyboardNavigation.SetTabIndex(_updateText, 1);
            _panel.Children.Insert(0, _updateText);
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
        button.Click += (_, _) => _updates.ShowWhatIsNew(Window.GetWindow(this));
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
