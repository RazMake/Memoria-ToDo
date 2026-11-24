using System.Windows;
using Memoria.ToDo.Desktop.Platform.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Tests;

/// <summary>
/// A test outside the screen collection, which this class is on purpose, fails at once when it starts the platform test
/// program, and when it ends after a window of its own loaded, which closes as it loads. Each failure names its class and the
/// attribute it needs. A screen test that does either passes, as every screen test shows when the whole suite runs.
/// </summary>
[Unit]
public class ScreenCollectionGuardTests
{
    [Fact]
    public void Fails_a_test_outside_the_screen_collection_that_asks_to_use_the_screen_naming_its_class_and_the_attribute()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ScreenTests.RequireScreenTest("paints a banner"));

        Assert.Contains(nameof(ScreenCollectionGuardTests), error.Message);
        Assert.Contains("[Collection(ScreenTests.Collection)]", error.Message);
        Assert.Contains("paints a banner", error.Message);
    }

    // The tests below give the handler and the hook a never-shown window and a record of their own: a real window loading
    // here would reach the globally registered handler and count against the test that showed it.

    private static string RunningTestId => TestContext.Current.Test!.UniqueID;

    [WpfFact]
    public void Closes_a_window_that_loads_in_a_test_outside_the_screen_collection()
    {
        var window = new Window();
        var closed = false;
        window.Closed += (_, _) => closed = true;

        ScreenCollectionGuard.Refuse(window, new WindowRefusals());

        Assert.True(closed);
    }

    [WpfFact]
    public void Records_a_window_that_loads_outside_the_screen_collection_against_the_running_test_naming_its_class_and_the_attribute()
    {
        var refusals = new WindowRefusals();

        ScreenCollectionGuard.Refuse(new Window(), refusals);

        var recorded = Assert.Single(refusals.Take(RunningTestId));
        Assert.Contains(nameof(ScreenCollectionGuardTests), recorded);
        Assert.Contains("[Collection(ScreenTests.Collection)]", recorded);
        Assert.Contains("shows a window", recorded);
    }

    [WpfFact]
    public void Records_a_failure_of_a_refused_windows_own_closing_code_with_the_refusal_instead_of_throwing()
    {
        var refusals = new WindowRefusals();
        var window = new Window();
        window.Closing += (_, _) => throw new InvalidOperationException("the window's own closing code failed");

        var error = Record.Exception(() => ScreenCollectionGuard.Refuse(window, refusals));

        Assert.Null(error);
        var recorded = Assert.Single(refusals.Take(RunningTestId));
        Assert.Contains("[Collection(ScreenTests.Collection)]", recorded);
        Assert.Contains(nameof(ScreenCollectionGuardTests), recorded);
        Assert.Contains("the window's own closing code failed", recorded);
    }

    [Fact]
    public void Fails_a_test_after_it_ends_when_a_refusal_was_recorded_under_its_ID_with_the_checks_message()
    {
        var refusals = new WindowRefusals();
        refusals.Record("test-1", "A test outside the screen collection shows a window. Running test class: One.");

        var error = Assert.Throws<InvalidOperationException>(() => FailTestsThatShowedRefusedWindows.ThrowIfRefused("test-1", refusals));

        Assert.Contains("A test outside the screen collection shows a window. Running test class: One.", error.Message);
    }

    [Fact]
    public void Does_not_fail_a_test_with_no_refusal_recorded_under_its_ID()
    {
        var refusals = new WindowRefusals();

        FailTestsThatShowedRefusedWindows.ThrowIfRefused("test-1", refusals);
    }

    [Fact]
    public void Leaves_another_tests_refusal_for_that_tests_hook()
    {
        var refusals = new WindowRefusals();
        refusals.Record("test-1", "refusal of one");
        refusals.Record("test-2", "refusal of two");

        Assert.Throws<InvalidOperationException>(() => FailTestsThatShowedRefusedWindows.ThrowIfRefused("test-1", refusals));

        var error = Assert.Throws<InvalidOperationException>(() => FailTestsThatShowedRefusedWindows.ThrowIfRefused("test-2", refusals));
        Assert.Contains("refusal of two", error.Message);
        Assert.DoesNotContain("refusal of one", error.Message);
    }

    [Fact]
    public void Fails_the_run_at_its_end_with_every_refusal_no_test_took_naming_each_class()
    {
        var refusals = new WindowRefusals();
        refusals.Record("test-1", "Running test class: First.Class.");
        refusals.Record("test-2", "Running test class: Second.Class.");
        refusals.Record("test-3", "Running test class: Taken.Class.");
        refusals.Take("test-3");

        var error = Assert.Throws<InvalidOperationException>(refusals.ThrowUnclaimed);

        Assert.Contains("First.Class", error.Message);
        Assert.Contains("Second.Class", error.Message);
        Assert.DoesNotContain("Taken.Class", error.Message);
    }

    [Fact]
    public void Fails_the_run_at_its_end_for_a_window_that_loaded_with_no_test_running()
    {
        var refusals = new WindowRefusals();
        refusals.Record(null, "Running test class: no test class.");

        var error = Assert.Throws<InvalidOperationException>(refusals.ThrowUnclaimed);

        Assert.Contains("no test class", error.Message);
    }

    [Fact]
    public void Ends_the_run_cleanly_when_every_refusal_was_taken()
    {
        var refusals = new WindowRefusals();
        refusals.Record("test-1", "refusal");
        refusals.Take("test-1");

        refusals.ThrowUnclaimed();
    }

    [Fact]
    public void Fails_a_test_outside_the_screen_collection_before_it_starts_the_platform_test_program()
    {
        var started = default(HostProcess);

        var error = Record.Exception(() => started = HostProcess.Start("end-app"));
        started?.Dispose(); // Only a missing check lets the program start; it must not outlive this test.

        var refused = Assert.IsType<InvalidOperationException>(error);
        Assert.Contains(nameof(ScreenCollectionGuardTests), refused.Message);
        Assert.Contains("[Collection(ScreenTests.Collection)]", refused.Message);
    }
}
