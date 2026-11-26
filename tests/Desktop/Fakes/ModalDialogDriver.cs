using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// Drives a modal dialog a UX test opens from an owner window. The dialog is modal, so <see cref="Run"/> queues what to do with
/// the dialog to run once it shows, then makes the call that doesn't return until the dialog closes. The dialog is the window owned
/// by the owner that loads, and the run fails the test when it isn't titled as expected. Not a fake of a port: shared test setup.
/// </summary>
public sealed class ModalDialogDriver(Window owner, OpenWindows windows)
{
    /// <summary>The dialog as it was when it showed; null when it never showed.</summary>
    public Window? Dialog { get; private set; }

    /// <summary>Whether the call that opened the dialog had returned while the dialog showed: false for a modal dialog.</summary>
    public bool ReturnedWhileShowing { get; private set; }

    /// <summary>The texts the dialog displayed when it showed, buttons and text boxes included.</summary>
    public IReadOnlyList<string> Texts { get; private set; } = [];

    /// <summary>Whether the dialog was centered on its owner when it showed.</summary>
    public bool Centered { get; private set; }

    /// <summary>
    /// Calls <paramref name="open"/>, which opens the dialog titled <paramref name="title"/> from the owner, and runs
    /// <paramref name="act"/> on the dialog once it shows. When the call shows no dialog, <paramref name="act"/> never runs.
    /// A dialog <paramref name="act"/> leaves open is closed, so the call returns.
    /// </summary>
    public void Run(string title, Action open, Action<Window> act)
    {
        var returned = false;
        Exception? failure = null;
        void ActWhenShown(Window dialog)
        {
            try
            {
                if (dialog.Title != title)
                {
                    throw new InvalidOperationException($"A dialog titled \"{dialog.Title}\" opened, not \"{title}\".");
                }

                Dialog = dialog;
                ReturnedWhileShowing = returned;
                Texts = [.. OpenWindows.TextsIn(dialog), .. OpenWindows.Descendants(dialog).OfType<TextBox>().Select(box => box.Text)];
                Centered = IsCentered(dialog);
                act(dialog);
            }
            catch (Exception e)
            {
                failure = e;
            }
            finally
            {
                if (dialog.IsVisible)
                {
                    dialog.Close(); // So that the call returns and the test reports why.
                }
            }
        }

        // The dialog's Loaded event says it showed. The timeout only bounds a failure: it closes a dialog that never loaded.
        var timeout = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TestWait.ForSignal };
        timeout.Tick += (_, _) =>
        {
            timeout.Stop();
            failure ??= new TimeoutException($"The dialog \"{title}\" never finished loading.");
            foreach (var window in windows.Visible().Where(w => w.Owner == owner))
            {
                window.Close();
            }
        };
        WhenLoaded.Remove(owner);
        WhenLoaded.Add(owner, ActWhenShown);
        timeout.Start();
        try
        {
            open();
            returned = true;
        }
        finally
        {
            timeout.Stop();
            WhenLoaded.Remove(owner);
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static readonly ConditionalWeakTable<Window, Action<Window>> WhenLoaded = [];

    static ModalDialogDriver()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is Window { Owner: { } dialogOwner } window && WhenLoaded.TryGetValue(dialogOwner, out var act))
            {
                WhenLoaded.Remove(dialogOwner);
                act(window);
            }
        }));
    }

    private static bool IsCentered(Window dialog)
    {
        var owner = dialog.Owner;
        return owner is not null
            && Math.Abs(dialog.Left + dialog.ActualWidth / 2 - (owner.Left + owner.ActualWidth / 2)) < 2
            && Math.Abs(dialog.Top + dialog.ActualHeight / 2 - (owner.Top + owner.ActualHeight / 2)) < 2;
    }
}
