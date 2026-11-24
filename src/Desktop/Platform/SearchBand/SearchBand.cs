using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The "Search:" label and its box, named "Search" for screen readers, edge to edge in the status bar's shade.</summary>
public sealed class SearchBand : Border
{
    private readonly TextBox _box = new() { Width = 180, VerticalAlignment = VerticalAlignment.Center };

    public SearchBand()
    {
        AutomationProperties.SetName(_box, "Search");
        _box.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
        // Preview phase, so a TextBox's own Up/Down handling can't consume the keys first. An attached list or a class-level handler
        // that used the key marks it handled, and WPF then doesn't call this handler.
        _box.PreviewKeyDown += (_, e) =>
        {
            var key = EventKey.Of(e);
            if (Keyboard.Modifiers == ModifierKeys.None && key is Key.Down or Key.Up)
            {
                e.Handled = true;
                EdgeRowRequested?.Invoke(this, key == Key.Up);
            }
        };

        var label = new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(label);
        row.Children.Add(_box);
        Padding = new Thickness(8, 4, 8, 4);
        Child = row;
        SetResourceReference(BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
    }

    /// <summary>The search box itself, for a part that works on any text box. Its text is <see cref="Text"/>.</summary>
    public TextBox Box => _box;

    /// <summary>The box's text.</summary>
    public string Text
    {
        get => _box.Text;
        set => _box.Text = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Raised whenever the box's text changes, by the user or through <see cref="Text"/>.</summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Raised on Down (false) or Up (true) in the box with no modifier key held, when no handler before the band's used the key,
    /// to select the top or bottom row. The key goes no further.
    /// </summary>
    public event EventHandler<bool>? EdgeRowRequested;

    /// <summary>Moves focus to the box and selects its text.</summary>
    public void FocusText()
    {
        _box.Focus();
        _box.SelectAll();
    }
}
