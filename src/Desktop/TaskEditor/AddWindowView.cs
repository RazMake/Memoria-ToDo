using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The Add window. Esc and the close button hide it, so the next Show reopens it at once. Application shutdown
/// still closes it: WPF ignores a cancelled Closing then.
/// </summary>
internal sealed class AddWindowView : Window, IAddWindow
{
    public AddWindowView()
    {
        Title = "Add new Task";
        Width = 480;
        MinHeight = 240;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Content = new TextBlock
        {
            Text = "Adding a task is coming soon.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24),
        };
    }

    void IAddWindow.Show() => WindowPlacement.ShowOnPointerScreen(this);

    bool IAddWindow.IsOpen => IsVisible;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = true;
        Hide();
    }
}
