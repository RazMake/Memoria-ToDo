using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Configure's Show part: its window, opened one at a time by the platform's <see cref="SingleWindow"/>.</summary>
internal sealed class ConfigureShow : IConfigureWindow
{
    private readonly SingleWindow _window = new(NewWindow);

    public void Show() => _window.Show();

    private static Window NewWindow() => new()
    {
        Title = "Configure",
        SizeToContent = SizeToContent.WidthAndHeight,
        MinWidth = 320,
        Content = new TextBlock
        {
            Text = "Configure is coming soon.",
            Margin = new Thickness(24),
        },
    };
}
