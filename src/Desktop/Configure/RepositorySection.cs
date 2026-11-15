using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The repository part of the Sync settings section: the current repository's address, local path, and commit
/// identity, and a Configure button whose menu shows the controls to clone a repository or to use a clone the user
/// already has. Both choices share one address box and one folder box.
/// </summary>
internal sealed class RepositorySection : StackPanel
{
    private readonly RepositoryChange _change;
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
        _change = new RepositoryChange(ui, Show);
        AutomationProperties.SetName(_address, "Repository address");
        AutomationProperties.SetName(_localFolder, "Local folder");
        _clone.Click += async (_, _) => await _change.ChangeAsync(_address.Text, _localFolder.Text);
        _use.Click += async (_, _) => await _change.ChangeAsync(_address.Text, _localFolder.Text);

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
    public void Refresh() => _change.Refresh();

    // Shows what the change holds: the lines, the controls of the choice, and the message.
    private void Show()
    {
        var lines = _change.Lines;
        ShowLine(_remote, "Repository:", lines.Address);
        ShowLine(_folder, "Local Path:", lines.Folder);
        ShowLine(_identity, "Identity:", lines.Identity);
        _clone.Visibility = _change.Choice == RepositoryChoice.UseExisting ? Visibility.Collapsed : Visibility.Visible;
        _use.Visibility = _change.Choice == RepositoryChoice.Clone ? Visibility.Collapsed : Visibility.Visible;
        _controls.Visibility = _change.ControlsVisible ? Visibility.Visible : Visibility.Collapsed;
        ThemedText.ShowMessage(_message, _change.Message);
    }

    private Button ConfigureButton()
    {
        var button = new Button { Content = "Configure", HorizontalAlignment = HorizontalAlignment.Left };
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        menu.Items.Add(Choice("Clone a repo", RepositoryChoice.Clone));
        menu.Items.Add(Choice("Use existing repo clone", RepositoryChoice.UseExisting));
        button.ContextMenu = menu;
        button.Click += (_, _) => menu.IsOpen = true;
        return button;
    }

    private MenuItem Choice(string header, RepositoryChoice choice)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => _change.Choose(choice);
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

    // Shows a read-only line: the field's name in bold, then its value.
    private static void ShowLine(TextBlock line, string name, string value)
    {
        line.Inlines.Clear();
        line.Inlines.Add(new Run(name) { FontWeight = FontWeights.Bold });
        line.Inlines.Add(new Run($" {value}"));
    }
}
