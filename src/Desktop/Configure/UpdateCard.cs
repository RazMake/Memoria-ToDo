using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The content of Application's Update card: the app's version, the new version while there is one, What is new, Update Now,
/// the Auto update check box, and the failure of an update. It shows what <see cref="IUpdateStatus"/> says, follows its
/// changes while the window is open, and calls it for each control. It has no box of its own: it says through
/// <see cref="CautionChanged"/> when its card should show the caution background.
/// </summary>
internal sealed class UpdateCard : StackPanel
{
    private readonly IUpdateStatus _updates;
    private readonly TextBlock _current = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _newVersion = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _whatIsNew = ConfigureButton.Make("What is new", new Thickness(0, 8, 8, 0));
    private readonly Button _updateNow = ConfigureButton.Make("Update Now", new Thickness(0, 8, 8, 0));
    private readonly CheckBox _autoUpdate = new() { Content = "Auto update", Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _installedOnly = ThemedText.Help("Updates work only in an installed copy.", new Thickness(28, 2, 0, 0));
    private readonly TextBlock _failure = ThemedText.Message();
    private readonly TextBlock _problem = ThemedText.Message();
    private bool _refreshing;

    /// <summary>Whether the card should stand out: a new version is available and not yet being installed.</summary>
    public bool Cautioned => _updates.State.Stage == UpdateStage.Available;

    /// <summary>Raised after each refresh, when <see cref="Cautioned"/> may have changed.</summary>
    public event Action? CautionChanged;

    public UpdateCard(IUpdateStatus updates)
    {
        _updates = updates;
        _whatIsNew.Click += (_, _) => _updates.ShowWhatIsNew(Window.GetWindow(this)!);
        _updateNow.Click += (_, _) => _updates.UpdateNow();
        _autoUpdate.Checked += (_, _) => Switch(true);
        _autoUpdate.Unchecked += (_, _) => Switch(false);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(_whatIsNew);
        buttons.Children.Add(_updateNow);
        Children.Add(_current);
        Children.Add(_newVersion);
        Children.Add(_failure);
        Children.Add(buttons);
        Children.Add(_autoUpdate);
        Children.Add(ThemedText.Help(
            "Checks for a new version each night around 3 AM, and installs it.", new Thickness(28, 2, 0, 0)));
        Children.Add(_installedOnly);
        Children.Add(_problem);

        Refresh();
        Loaded += (_, _) =>
        {
            _updates.Changed -= OnChanged; // So that a card loaded again isn't told twice.
            _updates.Changed += OnChanged;
            Refresh();
        };
        Unloaded += (_, _) => _updates.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var state = _updates.State;
        var installed = state.Stage != UpdateStage.NotInstalled;
        var updating = state.Stage == UpdateStage.Updating;
        var newer = state.Stage is UpdateStage.Available or UpdateStage.Updating;

        _current.Text = $"Current version: {state.CurrentVersion}";
        ShowVersionLine(state, newer);
        ThemedText.ShowMessage(_failure, state.Failure);
        _whatIsNew.Visibility = Shown(newer);
        _updateNow.Visibility = Shown(newer && !_updates.AutoUpdate);
        _updateNow.IsEnabled = !updating;
        _installedOnly.Visibility = Shown(!installed);

        _refreshing = true;
        _autoUpdate.IsChecked = installed && _updates.AutoUpdate;
        _refreshing = false;
        _autoUpdate.IsEnabled = installed && !updating;

        CautionChanged?.Invoke();
    }

    private void ShowVersionLine(UpdateState state, bool newer)
    {
        _newVersion.Visibility = Shown(newer);
        _newVersion.Inlines.Clear();
        if (newer)
        {
            _newVersion.Inlines.Add(new Run(state.Stage == UpdateStage.Available ? "Available new version: " : "Updating to: "));
            _newVersion.Inlines.Add(new Run(state.NewVersion) { FontWeight = FontWeights.Bold });
        }
    }

    private void Switch(bool on)
    {
        if (_refreshing)
        {
            return;
        }

        try
        {
            _updates.SetAutoUpdate(on);
            ThemedText.ShowMessage(_problem, null);
        }
        catch (Exception error)
        {
            ThemedText.ShowMessage(_problem, error.Message);
            _refreshing = true;
            _autoUpdate.IsChecked = _updates.AutoUpdate;
            _refreshing = false;
        }
    }

    private static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;
}
