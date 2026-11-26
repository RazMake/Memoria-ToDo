using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.KeepAwake;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Prevent screen saver row of the Behavior section: a check box that switches Keep awake at once, its help line, and one
/// message in the error color, for a failed save or for the failure Keep awake reports. It follows Keep awake's state while loaded.
/// </summary>
internal sealed class KeepAwakeRow : StackPanel
{
    private readonly IKeepAwakeStatus _keepAwake;
    private readonly ILogger _logger;
    private readonly CheckBox _box = new() { Content = "Prevent screen saver" };
    private readonly TextBlock _message = ThemedText.Message();
    private string? _saveProblem;
    private bool _refreshing;

    public KeepAwakeRow(IKeepAwakeStatus keepAwake, ILogger logger)
    {
        _keepAwake = keepAwake;
        _logger = logger;
        _box.Checked += (_, _) => Switch(true);
        _box.Unchecked += (_, _) => Switch(false);

        Children.Add(_box);
        Children.Add(ThemedText.Help(
            "Keeps the screen on while the app runs. Locking the PC pauses it until you unlock.", new Thickness(28, 2, 0, 0)));
        Children.Add(_message);

        Refresh();
        Loaded += (_, _) =>
        {
            _keepAwake.Changed -= OnChanged; // So that a row loaded again isn't told twice.
            _keepAwake.Changed += OnChanged;
            Refresh();
        };
        Unloaded += (_, _) => _keepAwake.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var state = _keepAwake.State;
        ThemedText.ShowMessage(_message, _saveProblem ?? state.Failure);
        _refreshing = true;
        _box.IsChecked = state.IsOn;
        _refreshing = false;
    }

    private void Switch(bool on)
    {
        if (_refreshing)
        {
            return;
        }

        try
        {
            _keepAwake.SetOn(on);
            _saveProblem = null;
        }
        catch (Exception error)
        {
            _logger.Error(error, "Switching Prevent screen saver {State} failed", on ? "on" : "off");
            _saveProblem = error.Message;
        }

        Refresh(); // Puts the box back after a failed save, and shows the message either way.
    }
}
