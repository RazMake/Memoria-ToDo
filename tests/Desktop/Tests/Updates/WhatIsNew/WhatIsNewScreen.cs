using System.Windows;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.Updates.Tests.WhatIsNew;

/// <summary>
/// Opens What is new the way a user does, from a window, and drives it through the shared <see cref="ModalDialogDriver"/>.
/// The dialog is the window owned by the screen's window that loads, and the screen fails the test when it isn't titled
/// "What is new". Shared test setup. Dispose it to close every window.
/// </summary>
internal sealed class WhatIsNewScreen : IDisposable
{
    public const string Title = "What is new";

    private readonly OpenWindows windows = new();
    private readonly ModalDialogDriver driver;

    public WhatIsNewScreen(UpdatesHarness app, bool dark = false)
    {
        App = app;
        // Small and centered on the screen, so that even a small screen fits the dialog centered on it, without moving it.
        Owner = new Window { Width = 300, Height = 200, Title = "Configure", WindowStartupLocation = WindowStartupLocation.CenterScreen };
        OpenWindows.UseTheme(Owner, dark);
        Owner.Show();
        driver = new ModalDialogDriver(Owner, windows);
    }

    public UpdatesHarness App { get; }

    public Window Owner { get; }

    /// <summary>The dialog as it was when it showed; null when it never showed.</summary>
    public Window? Dialog => driver.Dialog;

    /// <summary>Whether the call that opened the dialog had returned while the dialog showed: false for a modal dialog.</summary>
    public bool ReturnedWhileShowing => driver.ReturnedWhileShowing;

    /// <summary>The texts the dialog displayed when it showed, buttons and text boxes included.</summary>
    public IReadOnlyList<string> Texts => driver.Texts;

    /// <summary>Whether the dialog was centered on its owner when it showed.</summary>
    public bool Centered => driver.Centered;

    /// <summary>
    /// Calls What is new on the owner and runs <paramref name="act"/> on the dialog once it shows. When the call shows no dialog,
    /// <paramref name="act"/> never runs. A dialog <paramref name="act"/> leaves open is closed, so the call returns.
    /// </summary>
    public void ShowWhatIsNew(Action<Window> act) => driver.Run(Title, () => App.Updates.ShowWhatIsNew(Owner), act);

    public void Dispose() => windows.Dispose();
}
