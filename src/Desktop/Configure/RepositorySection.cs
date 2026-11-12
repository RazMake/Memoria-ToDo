using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Memoria.ToDo.Shared.Sync;
using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The repository part of the Sync settings section: the current repository's address, local path, and commit
/// identity, and a Configure button whose menu shows the controls to clone a repository or to use a clone the user
/// already has. Both choices share one address box and one folder box.
/// </summary>
internal sealed class RepositorySection : StackPanel
{
    private readonly SyncUi _ui;
    private readonly TextBlock _remote = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _folder = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _identity = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _address = new() { Margin = new Thickness(0, 0, 0, 5) };
    private readonly TextBox _localFolder = new();
    private readonly Button _clone = new() { Content = "Clone", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _use = new() { Content = "Use this clone", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _controls = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _message = ThemedText.Message();

    public RepositorySection(SyncUi ui)
    {
        _ui = ui;
        AutomationProperties.SetName(_address, "Repository address");
        AutomationProperties.SetName(_localFolder, "Local folder");
        _clone.Click += async (_, _) => await ChangeAsync(
            (address, folder, token) => _ui.Sync.Setup.CloneAsync(address, folder, token));
        _use.Click += async (_, _) => await ChangeAsync(
            (address, folder, token) => _ui.Sync.Setup.UseExistingAsync(address, folder, token));

        _controls.Children.Add(Label("Repository address"));
        _controls.Children.Add(_address);
        _controls.Children.Add(Label("Local folder"));
        _controls.Children.Add(FolderRow(_localFolder));
        _controls.Children.Add(_clone);
        _controls.Children.Add(_use);

        Children.Add(ThemedText.Help("Your tasks are kept in a git repository.", new Thickness(0, 0, 0, 8)));
        Children.Add(_remote);
        Children.Add(_folder);
        Children.Add(_identity);
        Children.Add(ConfigureButton());
        Children.Add(_controls);
        Children.Add(_message);
        Refresh();
    }

    /// <summary>Shows the repository, folder, and identity the setup holds now.</summary>
    public void Refresh()
    {
        var state = _ui.Sync.Setup.State;
        Show(_remote, "Repository:", state.RepositoryAddress ?? "None");
        Show(_folder, "Local Path:", state.RepositoryFolder ?? "None");
        Show(_identity, "Identity:", state.DeviceName);
    }

    private Button ConfigureButton()
    {
        var button = new Button { Content = "Configure", HorizontalAlignment = HorizontalAlignment.Left };
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        menu.Items.Add(Choice("Clone a repo", _clone, _use));
        menu.Items.Add(Choice("Use existing repo clone", _use, _clone));
        button.ContextMenu = menu;
        button.Click += (_, _) => menu.IsOpen = true;
        return button;
    }

    private MenuItem Choice(string header, Button show, Button hide)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            show.Visibility = Visibility.Visible;
            hide.Visibility = Visibility.Collapsed;
            _controls.Visibility = Visibility.Visible;
            ThemedText.ShowMessage(_message, null);
        };
        return item;
    }

    private static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 0, 0, 4) };

    private static Grid FolderRow(TextBox folder)
    {
        var browse = new Button { Content = "Browse…", Margin = new Thickness(5, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder" };
            if (dialog.ShowDialog() == true)
            {
                folder.Text = dialog.FolderName;
            }
        };
        var row = new Grid
        {
            Margin = new Thickness(0, 0, 0, 5),
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } },
        };
        Grid.SetColumn(browse, 1);
        row.Children.Add(folder);
        row.Children.Add(browse);
        return row;
    }

    // Hides the controls at once, runs the change and then one sync; on failure shows the controls again with why.
    private async Task ChangeAsync(Func<string, string, CancellationToken, Task> change)
    {
        var address = _address.Text.Trim();
        var folder = _localFolder.Text.Trim();
        if (address.Length == 0 || folder.Length == 0)
        {
            ThemedText.ShowMessage(_message, "Enter the repository's address and the local folder.");
            return;
        }

        _controls.Visibility = Visibility.Collapsed;
        ThemedText.ShowMessage(_message, null);
        var failed = false;
        await _ui.RunAsync(
            async token =>
            {
                await change(address, folder, token);
                var status = await _ui.Sync.SyncNowAsync(token);
                if (status.State is SyncState.Error or SyncState.Offline)
                {
                    ThemedText.ShowMessage(_message, status.Details);
                }
            },
            error =>
            {
                failed = true;
                ThemedText.ShowMessage(_message, error);
            });
        if (failed)
        {
            _controls.Visibility = Visibility.Visible;
        }
    }

    // Shows a read-only line: the field's name in bold, then its value.
    private static void Show(TextBlock line, string name, string value)
    {
        line.Inlines.Clear();
        line.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
        line.Inlines.Add(new Run($" {value}"));
    }
}
