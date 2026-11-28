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

/// <summary>
/// The Logs window's common paths on its one screen, with a fake log source. The model's filters, rows, status line and edge cases
/// are in the unit tests of the model.
/// </summary>
[UX("Logs")]
[Collection(ScreenTests.Collection)]
public class LogsWindowTests
{
    private readonly LogsServices _services = new();

    [WpfFact]
    public void Open_the_log_narrow_it_with_the_quick_search_and_refresh_to_see_a_new_entry()
    {
        _services.With(
            LogsServices.Entry("2026-03-14 10:00", message: "Pushed three files"),
            LogsServices.Entry("2026-03-14 12:00", message: "Newest"),
            LogsServices.Entry("2026-03-14 11:00", message: "Fetched nothing"));
        using var screen = new LogsScreen(_services);

        Assert.Equal(["Newest", "Fetched nothing", "Pushed three files"], screen.RowMessages);
        Assert.Contains(screen.Texts, text => text.Contains("Showing 3 of 3 entries", StringComparison.Ordinal));

        screen.Search("PUSHED");

        Assert.Equal(["Pushed three files"], screen.RowMessages);

        screen.Search("");

        Assert.Equal(["Newest", "Fetched nothing", "Pushed three files"], screen.RowMessages);

        _services.Source.Entries.Add(LogsServices.Entry("2026-03-14 13:00", message: "Brand new"));
        screen.ClickRefresh(2);

        Assert.Equal(["Brand new", "Newest", "Fetched nothing", "Pushed three files"], screen.RowMessages);
    }

    [WpfFact]
    public void Showing_the_open_window_again_brings_the_same_window_forward_without_reading_the_log_again()
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
    public void Picking_each_time_range_shows_only_the_date_boxes_it_uses()
    {
        using var screen = new LogsScreen(_services);

        Assert.False(screen.ShowsBound("From"));
        Assert.False(screen.ShowsBound("To"));

        screen.ChoosePreset("Last hour");

        Assert.True(screen.ShowsBound("From"));
        Assert.False(screen.ShowsBound("To"));

        screen.ChoosePreset("Today");

        Assert.True(screen.ShowsBound("From"));
        Assert.False(screen.ShowsBound("To"));

        screen.ChoosePreset("Custom");

        Assert.True(screen.ShowsBound("From"));
        Assert.True(screen.ShowsBound("To"));

        screen.ChoosePreset("All");

        Assert.False(screen.ShowsBound("From"));
        Assert.False(screen.ShowsBound("To"));
    }
}
