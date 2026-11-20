using System.Windows;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Opens WPF dialogs without a white first frame.</summary>
public static class ModalDialog
{
    /// <summary>
    /// Shows the window as a modal dialog and returns when it closes. A window shown for the first time stays
    /// invisible on the screen until WPF has drawn it.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called off the window's UI thread.</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Windows can't cloak the window.</exception>
    public static void Show(Window dialog)
    {
        dialog.VerifyAccess();

        if (FirstShowCloak.IsFirstShow(dialog))
        {
            FirstShowCloak.Start(dialog);
        }

        dialog.ShowDialog();
    }
}
