using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.MCPServer;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The AI assistants row of the Behavior section: the check box that lets assistants in with the Examples… button on its line,
/// the help line, the status line, and the message of a failed switch. It shows what <see cref="AiAssistantsSetting"/> says and
/// follows the server's changes while the window is open. The assistants' lines are the Examples dialog's, which the button asks for.
/// </summary>
internal sealed class AiAssistantsRow : StackPanel
{
    private readonly AiAssistantsSetting _setting;
    private readonly IMcpServerStatus _server;
    private readonly CheckBox _box = new() { Content = "Let AI assistants on this PC use Memoria-ToDo" };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 4, 0, 0) };
    private readonly TextBlock _problem = ThemedText.Message();
    private bool _refreshing;

    /// <param name="showExamples">Shows the Examples dialog over the given window, the one this row is in.</param>
    public AiAssistantsRow(AiAssistantsSetting setting, IMcpServerStatus server, Action<Window> showExamples)
    {
        _setting = setting;
        _server = server;
        _box.Checked += (_, _) => Switch(true);
        _box.Unchecked += (_, _) => Switch(false);
        var examples = ConfigureButton.Make("Examples…", new Thickness(12, 0, 0, 0));
        examples.Click += (_, _) => showExamples(Window.GetWindow(this)!);

        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(_box);
        line.Children.Add(examples);
        Children.Add(line);
        Children.Add(ThemedText.Help(
            "Assistants such as GitHub Copilot, Claude, and Codex can read and change your tasks, contacts, and settings. "
            + "Each asks you before it uses a tool, as its own settings say.",
            new Thickness(28, 2, 0, 0)));
        Children.Add(_status);
        Children.Add(_problem);

        Refresh();
        Loaded += (_, _) =>
        {
            _server.Changed -= OnChanged; // So that a row loaded again isn't told twice.
            _server.Changed += OnChanged;
            Refresh();
        };
        Unloaded += (_, _) => _server.Changed -= OnChanged;
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
