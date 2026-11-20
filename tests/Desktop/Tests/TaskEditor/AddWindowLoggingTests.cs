using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What the Add window logs through the logger it was given: each failure it catches and shows is also logged at Error with
/// the exception. The window shows the message; the log keeps the cause.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class AddWindowLoggingTests
{
    [WpfFact]
    public void A_kept_draft_that_cannot_be_read_is_logged_as_an_error_with_the_exception()
    {
        using var screen = new AddScreen();
        screen.Services.KeepUnreadableDraft();

        screen.Show();

        Assert.Contains(screen.Services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.Exception is not null);
    }

    [WpfFact]
    public void A_save_that_fails_is_logged_as_an_error_with_the_exception()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");
        screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);

        screen.Save();

        Assert.Contains(screen.Services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.Exception is not null);
    }

    [WpfFact]
    public void A_window_that_works_logs_no_problem()
    {
        using var screen = new AddScreen();
        screen.Show();
        screen.Fill("Buy milk");

        screen.Save();

        screen.Services.Log.Events.AssertNoProblems();
    }
}
