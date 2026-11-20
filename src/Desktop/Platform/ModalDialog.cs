using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Opens WPF dialogs without a white first frame.</summary>
public static class ModalDialog
{
    /// <summary>
    /// Shows the window as a modal dialog and returns when it closes. A window shown for the first time stays
    /// invisible on the screen until WPF has drawn it. Logs its choices at Debug through
    /// <paramref name="logger"/>, the caller's own.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Called off the window's UI thread.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Windows can't cloak the window.</exception>
    public static void Show(Window dialog, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        dialog.VerifyAccess();

        var firstShow = FirstShowCloak.IsFirstShow(dialog);
        logger.Debug("Showing the dialog {Window}; FirstShow {FirstShow}", dialog.GetType().Name, firstShow);
        if (firstShow)
        {
            FirstShowCloak.Start(dialog, logger);
        }

        dialog.ShowDialog();
    }
}
