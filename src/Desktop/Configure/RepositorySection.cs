using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Sync settings section's content: the current repository's address, local path, and commit identity, and a
/// Configure button
/// whose menu shows the controls to clone a repository or to use a clone the user already has.
/// </summary>
internal sealed class RepositorySection : StackPanel
{
    private readonly IRepositoryFolder _repository;
    private readonly TextBlock _remote = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _folder = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _identity = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _url = new() { Margin = new Thickness(0, 0, 0, 5) };
    private readonly TextBox _cloneFolder = new();
    private readonly TextBox _existingFolder = new();
    private readonly Button _clone = new() { Content = "Clone", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _use = new() { Content = "Use this clone", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel _clonePanel = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _existingPanel = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _message = ThemedText.Message();

    public RepositorySection(IRepositoryFolder repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _clone.Click += async (_, _) => await CloneAsync();
        _use.Click += async (_, _) => await UseExistingAsync();

        _clonePanel.Children.Add(Label("Repository URL"));
        _clonePanel.Children.Add(_url);
        _clonePanel.Children.Add(Label("Folder to clone into"));
        _clonePanel.Children.Add(FolderRow(_cloneFolder));
        _clonePanel.Children.Add(_clone);
        _existingPanel.Children.Add(Label("Folder of your clone"));
        _existingPanel.Children.Add(FolderRow(_existingFolder));
        _existingPanel.Children.Add(_use);

        Children.Add(ThemedText.Help("Your tasks are kept in a git repository.", new Thickness(0, 0, 0, 8)));
        Children.Add(_remote);
        Children.Add(_folder);
        Children.Add(_identity);
        Children.Add(ConfigureButton());
        Children.Add(_clonePanel);
        Children.Add(_existingPanel);
        Children.Add(_message);
        _ = ShowCurrentAsync();
    }

    private Button ConfigureButton()
    {
        var button = new Button { Content = "Configure", HorizontalAlignment = HorizontalAlignment.Left };
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        menu.Items.Add(Choice("Clone a repo", _clonePanel));
        menu.Items.Add(Choice("Use existing repo clone", _existingPanel));
        button.ContextMenu = menu;
        button.Click += (_, _) => menu.IsOpen = true;
        return button;
    }

    private MenuItem Choice(string header, StackPanel panel)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            _clonePanel.Visibility = _existingPanel.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Visible;
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

    private async Task CloneAsync()
    {
        var url = _url.Text.Trim();
        var folder = _cloneFolder.Text.Trim();
        if (url.Length == 0 || folder.Length == 0)
        {
            ThemedText.ShowMessage(_message, "Enter the repository's URL and the folder to clone it into.");
            return;
        }

        await ChangeAsync(() => _repository.CloneAsync(url, folder, CancellationToken.None));
    }

    private async Task UseExistingAsync()
    {
        var folder = _existingFolder.Text.Trim();
        if (folder.Length == 0)
        {
            ThemedText.ShowMessage(_message, "Enter or browse to the folder of your clone.");
            return;
        }

        await ChangeAsync(() =>
        {
            _repository.UseExisting(folder);
            return Task.CompletedTask;
        });
    }

    // Hides the controls at once and runs the change; on success shows the new repository, on failure shows the
    // controls again with why.
    private async Task ChangeAsync(Func<Task> change)
    {
        var shown = _clonePanel.Visibility == Visibility.Visible ? _clonePanel : _existingPanel;
        _clonePanel.Visibility = _existingPanel.Visibility = Visibility.Collapsed;
        ThemedText.ShowMessage(_message, null);
        try
        {
            await change();
        }
        catch (Exception error)
        {
            shown.Visibility = Visibility.Visible;
            ThemedText.ShowMessage(_message, error.Message);
            return;
        }

        await ShowCurrentAsync();
    }

    private async Task ShowCurrentAsync()
    {
        Show(_folder, "Local Path:", _repository.Current() ?? "None");
        Show(_remote, "Repository:", "…");
        Show(_identity, "Identity:", "…");
        try
        {
            Show(_remote, "Repository:", await _repository.RemoteAsync(CancellationToken.None) ?? "None");
            Show(_identity, "Identity:", await _repository.IdentityAsync(CancellationToken.None) ?? "None");
        }
        catch (Exception error)
        {
            if (_remote.Text.EndsWith('…'))
            {
                Show(_remote, "Repository:", "Unknown");
            }

            Show(_identity, "Identity:", "Unknown");
            ThemedText.ShowMessage(_message, error.Message);
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
