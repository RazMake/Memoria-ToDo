using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Tells the user about an error nobody handled.</summary>
internal static class ErrorBox
{
    public static void Show(Exception error) =>
        MessageBox.Show(error.Message, AppName.Value, MessageBoxButton.OK, MessageBoxImage.Error);
}
