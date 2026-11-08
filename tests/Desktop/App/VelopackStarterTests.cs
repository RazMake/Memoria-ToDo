using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// Velopack's starter and the sign-in argument. This can't run without a real installed copy, so it is skipped
/// and left to a manual check.
/// </summary>
[Integration]
public class VelopackStarterTests
{
    // Manual check, on a PC with an installed build (setup.exe from a release):
    //  1. Start the installed app by hand once, then exit it from the tray menu.
    //  2. Run "%LocalAppData%\MemoriaToDo\Memoria-ToDo.exe --at-sign-in" (the starter the Run key names).
    //     Expect the tray icon and no window.
    //  3. Exit it, then run the starter with no argument. Expect the tray icon and View Tasks.
    //  4. Install a newer release over it (or let the app download one and restart it), then repeat step 2.
    //     Expect the same: the starter still passes the argument on after an update.
    [Fact(Skip = "Needs a real installed copy; see the manual check in the comment above.")]
    public void The_starter_passes_the_sign_in_argument_on_also_after_an_update()
    {
        Assert.Fail("Manual check only.");
    }
}
