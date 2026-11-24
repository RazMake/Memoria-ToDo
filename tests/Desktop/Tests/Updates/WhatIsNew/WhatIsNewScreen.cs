using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.Updates.Tests.WhatIsNew;

/// <summary>
/// Opens What is new the way a user does, from a window, and drives it. The dialog is modal, so <see cref="ShowWhatIsNew"/> queues
/// what to do with the dialog to run once it shows, then makes the call that doesn't return until the dialog closes. The dialog is
/// the window owned by the screen's window that loads, and the screen fails the test when it isn't titled "What is new".
/// Shared test setup. Dispose it to close every window.
/// </summary>
internal sealed class WhatIsNewScreen : IDisposable
{
    public const string Title = "What is new";

    private readonly OpenWindows windows = new();

    public WhatIsNewScreen(UpdatesHarness app, bool dark = false)
    {
        App = app;
        // Small and centered on the screen, so that even a small screen fits the dialog centered on it, without moving it.
        Owner = new Window { Width = 300, Height = 200, Title = "Configure", WindowStartupLocation = WindowStartupLocation.CenterScreen };
        OpenWindows.UseTheme(Owner, dark);
        Owner.Show();
    }

    public UpdatesHarness App { get; }

    public Window Owner { get; }

    /// <summary>The dialog as it was when it showed; null when it never showed.</summary>
    public Window? Dialog { get; private set; }

    /// <summary>Whether the call that opened the dialog had returned while the dialog showed: false for a modal dialog.</summary>
    public bool ReturnedWhileShowing { get; private set; }

    /// <summary>The texts the dialog displayed when it showed, buttons and text boxes included.</summary>
    public IReadOnlyList<string> Texts { get; private set; } = [];

    /// <summary>Whether the dialog was centered on its owner when it showed.</summary>
    public bool Centered { get; private set; }

    /// <summary>
    /// Calls What is new on the owner and runs <paramref name="act"/> on the dialog once it shows. When the call shows no dialog,
    /// <paramref name="act"/> never runs. A dialog <paramref name="act"/> leaves open is closed, so the call returns.
    /// </summary>
    public void ShowWhatIsNew(Action<Window> act)
    {
        var returned = false;
        Exception? failure = null;
        void ActWhenShown(Window dialog)
        {
            try
            {
                if (dialog.Title != Title)
                {
                    throw new InvalidOperationException($"A dialog titled \"{dialog.Title}\" opened, not \"{Title}\".");
                }

                Dialog = dialog;
                ReturnedWhileShowing = returned;
                Texts = [.. OpenWindows.TextsIn(dialog), .. OpenWindows.Descendants(dialog).OfType<System.Windows.Controls.TextBox>().Select(box => box.Text)];
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
            failure ??= new TimeoutException("The dialog that What is new opened never finished loading.");
            foreach (var window in windows.Visible().Where(w => w.Owner == Owner))
            {
                window.Close();
            }
        };
        WhenLoaded.Remove(Owner);
        WhenLoaded.Add(Owner, ActWhenShown);
        timeout.Start();
        try
        {
            App.Updates.ShowWhatIsNew(Owner);
            returned = true;
        }
        finally
        {
            timeout.Stop();
            WhenLoaded.Remove(Owner);
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public void Dispose() => windows.Dispose();

    private static readonly ConditionalWeakTable<Window, Action<Window>> WhenLoaded = [];

    static WhatIsNewScreen()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is Window { Owner: { } owner } window && WhenLoaded.TryGetValue(owner, out var act))
            {
                WhenLoaded.Remove(owner);
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
