using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>
/// Single window with a saved place, with plain test windows on the real screen. The saved file is the fake file
/// system's; places are in the pixels Windows gives, read back with Windows' own window rectangle. Four paths: a
/// window reopens where it was closed, a window saved maximized reopens maximized, a place off every screen is
/// ignored, and a place that can't be saved doesn't stop the window from closing.
/// </summary>
[UX("WindowHosting")]
[Collection(ScreenTests.Collection)]
public class SingleWindowSavedPlacePathTests
{
    private const string Folder = @"C:\Memoria\Device";
    private const string Path = Folder + @"\place.json";
    private const int RoundingPixels = 2;

    private readonly FakeFileSystem _files = new();
    private readonly InMemoryLogSink _log = new();

    [WpfFact]
    public void A_window_opens_at_its_saved_place_keeps_it_while_open_and_is_saved_where_the_user_left_it()
    {
        var saved = PlaceOnPointerScreen(60, 50, 640, 420);
        Save(saved);
        using var made = new MadeWindows();
        var single = Single(made);

        single.Show();

        var window = Assert.Single(made.All);
        Assert.True(Win32.IsCloaked(Handle(window)), "The window must still be cloaked when it's placed.");
        AssertAt(saved, window);
        Assert.Equal(WindowState.Normal, window.WindowState);

        window.Left += 40;
        window.Top += 30;
        var moved = Win32.WindowRect(Handle(window));
        _files.FailReadsUnder(Folder);
        single.Show();
        _files.StopFailing();

        Assert.Same(window, Assert.Single(made.All));
        AssertAt(moved, window);
        Assert.Empty(Warnings());

        window.Close();

        AssertSaved(moved, maximized: false);
        single.Show();

        Assert.Equal(2, made.All.Count);
        AssertAt(moved, made.All[1]);
    }

    [WpfFact]
    public void A_window_saved_maximized_reopens_maximized_on_its_screen_and_restores_to_its_saved_size()
    {
        var saved = PlaceOnPointerScreen(60, 50, 640, 420);
        Save(saved, maximized: true);
        using var made = new MadeWindows();
        var single = Single(made);

        single.Show();

        var window = Assert.Single(made.All);
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal(WorkAreaAround(saved), WorkAreaAround(Win32.WindowRect(Handle(window))));

        window.WindowState = WindowState.Normal;

        AssertAt(saved, window);

        window.WindowState = WindowState.Maximized;
        window.WindowState = WindowState.Minimized;
        window.Close();

        AssertSaved(saved, maximized: true);
    }

    [WpfFact]
    public void A_place_off_every_screen_is_ignored_the_window_opens_on_the_pointer_screen_and_is_saved_where_it_opened()
    {
        var virtualScreen = Win32.VirtualScreen();
        Save(new Win32.Rect { Left = virtualScreen.Right + 500, Top = 100, Right = virtualScreen.Right + 1100, Bottom = 500 });
        using var made = new MadeWindows();
        var single = Single(made);

        single.Show();

        var window = Assert.Single(made.All);
        var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
        var rect = Win32.WindowRect(Handle(window));
        Assert.InRange(rect.Left + rect.Width / 2 - (work.Left + work.Width / 2), -RoundingPixels, RoundingPixels);
        Assert.InRange(rect.Top + rect.Height / 2 - (work.Top + work.Height / 2), -RoundingPixels, RoundingPixels);
        Assert.Empty(Warnings());

        window.Close();

        AssertSaved(rect, maximized: false);
    }

    [WpfFact]
    public void A_place_that_cannot_be_saved_does_not_stop_the_window_from_closing_and_the_next_open_uses_the_old_place()
    {
        var saved = PlaceOnPointerScreen(60, 50, 640, 420);
        Save(saved);
        using var made = new MadeWindows();
        var single = Single(made);
        single.Show();
        var window = Assert.Single(made.All);
        window.Left += 40;
        _files.FailWritesUnder(Folder);

        Assert.Null(Record.Exception(window.Close));

        Assert.False(window.IsVisible);
        var warning = Assert.Single(Warnings());
        Assert.Contains(Path, warning.RenderMessage());

        _files.StopFailing();
        single.Show();

        Assert.Equal(2, made.All.Count);
        AssertAt(saved, made.All[1]);
    }

    private SingleWindow Single(MadeWindows made) =>
        new(made.Make, new SavedWindowPlacement(_files, Path, "The test window's place", _log.CreateLogger()), _log.CreateLogger());

    private IReadOnlyList<LogEvent> Warnings() => _log.Events.Where(e => e.Level == LogEventLevel.Warning).ToList();

    private static Win32.Rect PlaceOnPointerScreen(int fromLeft, int fromTop, int width, int height)
    {
        var work = Win32.WorkAreaOfScreenWith(Win32.CursorPosition());
        return new Win32.Rect { Left = work.Left + fromLeft, Top = work.Top + fromTop, Right = work.Left + fromLeft + width, Bottom = work.Top + fromTop + height };
    }

    private static Win32.Rect WorkAreaAround(Win32.Rect rect) =>
        Win32.WorkAreaOfScreenWith(new Win32.Point { X = rect.Left + rect.Width / 2, Y = rect.Top + rect.Height / 2 });

    private void Save(Win32.Rect place, bool maximized = false) =>
        _files.WriteFile(Path, Encoding.UTF8.GetBytes(
            $$"""{"version":1,"left":{{place.Left}},"top":{{place.Top}},"width":{{place.Width}},"height":{{place.Height}},"maximized":{{(maximized ? "true" : "false")}}}"""));

    private void AssertSaved(Win32.Rect expected, bool maximized)
    {
        using var file = JsonDocument.Parse(_files.ReadFile(Path));
        var root = file.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.InRange(root.GetProperty("left").GetInt32() - expected.Left, -RoundingPixels, RoundingPixels);
        Assert.InRange(root.GetProperty("top").GetInt32() - expected.Top, -RoundingPixels, RoundingPixels);
        Assert.InRange(root.GetProperty("width").GetInt32() - expected.Width, -RoundingPixels, RoundingPixels);
        Assert.InRange(root.GetProperty("height").GetInt32() - expected.Height, -RoundingPixels, RoundingPixels);
        Assert.Equal(maximized, root.GetProperty("maximized").GetBoolean());
    }

    private static void AssertAt(Win32.Rect expected, Window window)
    {
        var actual = Win32.WindowRect(Handle(window));
        Assert.InRange(actual.Left - expected.Left, -RoundingPixels, RoundingPixels);
        Assert.InRange(actual.Top - expected.Top, -RoundingPixels, RoundingPixels);
        Assert.InRange(actual.Width - expected.Width, -RoundingPixels, RoundingPixels);
        Assert.InRange(actual.Height - expected.Height, -RoundingPixels, RoundingPixels);
    }

    private static nint Handle(Window window) => new WindowInteropHelper(window).Handle;
}
