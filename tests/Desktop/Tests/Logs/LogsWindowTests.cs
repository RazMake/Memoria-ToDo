using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>What the Logs window refuses when it is created, and that creating it makes no window. These run with no log.</summary>
[Unit]
public class LogsWindowCreateTests
{
    private readonly LogsServices _services = new();

    private ILogsWindow Create(bool source = true, bool theme = true, bool closeKeys = true, bool time = true, bool logger = true) =>
        LogsWindow.Create(
            source ? _services.Source : null!, theme ? _services.Theme : null!, closeKeys ? _services.CloseWindowKeys : null!,
            time ? _services.Time : null!, logger ? _services.Logger : null!);

    [Fact]
    public void Create_rejects_a_missing_log_source_by_name() =>
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => Create(source: false)).ParamName);

    [Fact]
    public void Create_rejects_a_missing_theme_by_name() =>
        Assert.Equal("theme", Assert.Throws<ArgumentNullException>(() => Create(theme: false)).ParamName);

    [Fact]
    public void Create_rejects_missing_Close_window_keys_by_name() =>
        Assert.Equal("closeWindowKeys", Assert.Throws<ArgumentNullException>(() => Create(closeKeys: false)).ParamName);

    [Fact]
    public void Create_rejects_a_missing_clock_by_name() =>
        Assert.Equal("time", Assert.Throws<ArgumentNullException>(() => Create(time: false)).ParamName);

    [Fact]
    public void Create_rejects_a_missing_logger_by_name() =>
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() => Create(logger: false)).ParamName);

    [Fact]
    public void Create_reads_nothing()
    {
        Create();

        Assert.Equal(0, _services.Source.ReadCount);
    }

    [WpfFact]
    public void A_new_window_shows_nothing_and_reads_nothing_until_Show()
    {
        using var windows = new OpenWindows();

        Create();

        Assert.Empty(windows.Visible());
        Assert.Equal(0, _services.Source.ReadCount);
    }
}

/// <summary>The Logs window's common scenarios on its one screen, with a fake log source. Filters and their edge cases are in the unit tests of the model.</summary>
[UX]
[Collection(ScreenTests.Collection)]
public class LogsWindowTests
{
    private readonly LogsServices _services = new();

    [WpfFact]
    public void Opening_shows_the_entries_newest_first_and_says_how_many_show()
    {
        _services.With(
            LogsServices.Entry("2026-03-14 10:00", message: "Oldest"),
            LogsServices.Entry("2026-03-14 12:00", message: "Newest"),
            LogsServices.Entry("2026-03-14 11:00", message: "Middle"));

        using var screen = new LogsScreen(_services);

        Assert.Equal(["Newest", "Middle", "Oldest"], screen.RowMessages);
        Assert.Contains(screen.Texts, text => text.Contains("Showing 3 of 3 entries", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Typing_in_the_quick_search_leaves_only_the_rows_that_match()
    {
        _services.With(
            LogsServices.Entry("2026-03-14 10:00", message: "Pushed three files"),
            LogsServices.Entry("2026-03-14 11:00", message: "Fetched nothing"));
        using var screen = new LogsScreen(_services);

        screen.Search("PUSHED");

        Assert.Equal(["Pushed three files"], screen.RowMessages);
    }

    [WpfFact]
    public void Showing_again_brings_the_same_window_forward_without_reading_the_log_again()
    {
        _services.With(LogsServices.Entry("2026-03-14 10:00", message: "Once"));
        using var screen = new LogsScreen(_services);
        var window = screen.Window;

        screen.View.Show();

        Assert.Same(window, screen.Window);
        Assert.Equal(1, _services.Source.ReadCount);
        Assert.Equal(["Once"], screen.RowMessages);
    }

    [WpfFact]
    public void Refresh_reads_the_log_again_and_shows_the_new_entries_above_the_old_ones()
    {
        _services.With(LogsServices.Entry("2026-03-14 10:00", message: "Old"));
        using var screen = new LogsScreen(_services);
        _services.Source.Entries.Add(LogsServices.Entry("2026-03-14 11:00", message: "New"));

        screen.ClickRefresh(2);

        Assert.Equal(["New", "Old"], screen.RowMessages);
    }
}
