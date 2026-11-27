using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The Windows message box, through WPF's MessageBox. Making it shows nothing.</summary>
public sealed class WindowsMessageBox : IMessageBox
{
    /// <inheritdoc />
    public void Show(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        MessageBox.Show(text, AppName.Value, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
