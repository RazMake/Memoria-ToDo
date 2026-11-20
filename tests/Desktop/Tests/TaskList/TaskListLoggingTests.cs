using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using System.Windows.Input;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What Task View logs through the logger it was given: each failure it catches and shows is also logged at Error with the
/// exception, including store failures, which go through the shared store-failure handling with the window's logger.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListLoggingTests
{
    [WpfFact]
    public void A_link_the_browser_cannot_open_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("No browser is installed.");
        var services = new ListServices();
        services.Browser.Failure = failure;
        services.Add("Buy milk", "See the shop");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.Web.ClickLink("https://example.com/shop");

        Assert.Contains(services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }

    [WpfFact]
    public void A_save_the_store_refuses_is_logged_as_an_error_with_the_exception_through_the_windows_logger()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Two litres. ");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Click("Save");

        Assert.Contains(services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.Exception is not null);
    }

    [WpfFact]
    public void A_window_that_works_logs_no_problem()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk");

        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        services.Log.Events.AssertNoProblems();
    }
}
