using System.Windows;
using Xunit;

namespace Memoria.ToDo.Desktop;

/// <summary>
/// Makes every window the test process shows check, once WPF reports it loaded, that the running test is a screen test.
/// WPF reports a load from a dispatcher, where a throw would end the test process, so a refused window is closed and recorded.
/// </summary>
internal static class ScreenCollectionGuard
{
    // Registered by the class constructor, so the WPF warm-up (WarmUpWpfFirstUses) can run it last on its own STA thread, and
    // nothing registers in a process that only loads this assembly, such as the hang-dump watcher.
    static ScreenCollectionGuard() =>
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Refuse((Window)sender, WindowRefusals.Shared)));

    /// <summary>
    /// When the running test isn't a screen test, hides and closes <paramref name="window"/> and records the refusal in
    /// <paramref name="refusals"/> under the test's unique ID, with any failure of the window's own closing code. Never throws.
    /// </summary>
    internal static void Refuse(Window window, WindowRefusals refusals)
    {
        if (ScreenTests.RefusalOfNonScreenTest("shows a window") is not { } refusal)
        {
            return;
        }

        try
        {
            // Hiding first keeps the window off the screen when its own Closing code cancels the close.
            window.Hide();
            window.Close();
        }
        catch (Exception closingFailure)
        {
            refusal += Environment.NewLine + "Closing the refused window then failed: " + closingFailure;
        }

        refusals.Record(TestContext.Current.Test?.UniqueID, refusal);
    }
}
