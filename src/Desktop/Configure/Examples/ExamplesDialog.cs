using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Connect an AI assistant dialog that the AI assistants row's Examples… button opens: modal and owned by Configure, with a
/// line on where the text goes, then each assistant's name, its read-only lines, and a Copy button, and the message of a failed copy.
/// </summary>
internal static class ExamplesDialog
{
    /// <summary>
    /// Shows the dialog over <paramref name="owner"/> with the assistants the setting lists now, and returns when it closes.
    /// A Copy goes through <paramref name="setting"/>, which logs a failed one; the dialog shows why and stays open.
    /// </summary>
    public static void Show(Window owner, AiAssistantsSetting setting, CloseWindowKeys closeWindowKeys, ILogger logger)
    {
        var message = ThemedText.Message();
        var content = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
        content.Children.Add(new TextBlock
        {
            Text = "Paste an assistant's text into its MCP settings file, or run it in a terminal. "
                + "Connect an AI assistant, in the app's user guide, says where each file is.",
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (var connection in setting.Connections)
        {
            AddAssistant(content, connection, () =>
            {
                setting.Copy(connection);
                ThemedText.ShowMessage(message, setting.CopyProblem);
            });
        }

        content.Children.Add(message);

        var close = ConfigureButton.Make("Close", new Thickness(12));
        close.IsCancel = true;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content,
        };
        var layout = new Grid { RowDefinitions = { new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
        Grid.SetRow(close, 1);
        layout.Children.Add(scroll);
        layout.Children.Add(close);

        var window = new Window
        {
            Title = "Connect an AI assistant",
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 640,
            Height = 560,
            ShowInTaskbar = false,
            Content = layout,
        };
        close.Click += (_, _) => window.Close();
        closeWindowKeys.Attach(window);
        EscCloses.Attach(window);
        ModalDialog.Show(window, logger);
    }

    private static void AddAssistant(StackPanel content, McpConnection connection, Action copy)
    {
        var button = ConfigureButton.Make("Copy", new Thickness(0, 6, 0, 0));
        button.Click += (_, _) => copy();
        content.Children.Add(new TextBlock { Text = connection.Assistant, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
        content.Children.Add(new TextBox
        {
            Text = connection.Text,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        content.Children.Add(button);
    }
}
