using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Tells the user about an error nobody handled.</summary>
internal static class ErrorBox
{
    public static void Show(Exception error) =>
        MessageBox.Show(error.Message, AppName.Value, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Shows the error and adds that the app keeps running and the details are in the log.</summary>
    public static void ShowAndKeepRunning(Exception error) =>
        MessageBox.Show(
            $"{error.Message}{Environment.NewLine}{Environment.NewLine}The app keeps running. The details are in the log.",
            AppName.Value,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
}
