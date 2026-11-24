using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.MCPServer;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The content of Application's AI assistants card: the check box that lets assistants in, the status line, and each
/// assistant's name, read-only lines, and Copy button. It shows what <see cref="AiAssistantsSetting"/> says and follows the
/// server's changes while the window is open.
/// </summary>
internal sealed class AiAssistantsCard : StackPanel
{
    private readonly AiAssistantsSetting _setting;
    private readonly IMcpServerStatus _server;
    private readonly CheckBox _box = new() { Content = "Let AI assistants on this PC use Memoria-ToDo", Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _problem = ThemedText.Message();
    private bool _refreshing;

    public AiAssistantsCard(AiAssistantsSetting setting, IMcpServerStatus server)
    {
        _setting = setting;
        _server = server;
        _box.Checked += (_, _) => Switch(true);
        _box.Unchecked += (_, _) => Switch(false);

        Children.Add(_box);
        Children.Add(ThemedText.Help(
            "Assistants such as GitHub Copilot, Claude, and Codex can read and change your tasks, contacts, and settings. "
            + "Each asks you before it uses a tool, as its own settings say.",
            new Thickness(28, 2, 0, 0)));
        Children.Add(_status);
        foreach (var connection in setting.Connections)
        {
            AddAssistant(connection);
        }

        Children.Add(_problem);

        Refresh();
        Loaded += (_, _) =>
        {
            _server.Changed -= OnChanged; // So that a card loaded again isn't told twice.
            _server.Changed += OnChanged;
            Refresh();
        };
        Unloaded += (_, _) => _server.Changed -= OnChanged;
    }

    private void AddAssistant(McpConnection connection)
    {
        var lines = new TextBox
        {
            Text = connection.Text,
            IsReadOnly = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var copy = ConfigureButton.Make("Copy", new Thickness(0, 6, 0, 0));
        copy.Click += (_, _) =>
        {
            _setting.Copy(connection);
            ThemedText.ShowMessage(_problem, _setting.Problem);
        };
        Children.Add(new TextBlock
        {
            Text = connection.Assistant,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 0),
        });
        Children.Add(lines);
        Children.Add(copy);
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        _status.Text = _setting.Status;
        if (_setting.StatusIsFailure)
        {
            _status.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        }
        else
        {
            _status.ClearValue(TextBlock.ForegroundProperty);
        }

        _refreshing = true;
        _box.IsChecked = _setting.IsOn;
        _refreshing = false;
    }

    private void Switch(bool on)
    {
        if (_refreshing)
        {
            return;
        }

        _setting.Set(on);
        ThemedText.ShowMessage(_problem, _setting.Problem);
        Refresh();
    }
}
