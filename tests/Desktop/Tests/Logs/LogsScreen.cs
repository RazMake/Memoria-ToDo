using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>
/// Drives a showing Logs window the way a user does, through what the screen shows. Shared test setup. Found by accessible labels:
/// <list type="bullet">
/// <item>A row of the list has automation id "Row" and the first line of its message as the name.</item>
/// <item>The search box is a text box named "Search". The Refresh button is found by its label.</item>
/// <item>The status line is a text block that starts with "Showing".</item>
/// </list>
/// The window reads the log off the UI thread, so <see cref="WaitForReads"/> lets the dispatcher work until a read has finished
/// and its result is on the screen.
/// </summary>
internal sealed class LogsScreen : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private readonly OpenWindows _windows = new();

    /// <summary>Shows the window and waits for its first read to show.</summary>
    public LogsScreen(LogsServices services)
    {
        Services = services;
        View = services.CreateWindow();
        View.Show();
        WaitForReads(1);
        ScreenSizeLimit.Lift(Window);
    }

    public LogsServices Services { get; }

    public ILogsWindow View { get; }

    public Window Window => _windows.Visible().Single();

    /// <summary>The first lines of the messages of the rows showing, top to bottom.</summary>
    public IReadOnlyList<string> RowMessages
    {
        get
        {
            Refresh();
            return [.. Visuals(Window).OfType<FrameworkElement>()
                .Where(element => element.IsVisible && AutomationProperties.GetAutomationId(element) == "Row")
                .OrderBy(element => element.TranslatePoint(new Point(0, 0), Window).Y)
                .Select(AutomationProperties.GetName)];
        }
    }

    /// <summary>The texts the window shows in text blocks.</summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            Refresh();
            return [.. Visuals(Window).OfType<TextBlock>().Where(block => block.IsVisible).Select(OpenWindows.TextOf).Where(text => text.Length > 0)];
        }
    }

    public void Search(string text)
    {
        Refresh();
        TextFields.Type(Visuals(Window).OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "Search"), text);
        Refresh();
    }

    /// <summary>Clicks the Refresh button and waits for the second read, or the <paramref name="reads"/>th, to show.</summary>
    public void ClickRefresh(int reads)
    {
        Refresh();
        Activate(VisibleButtons(Window).Single(button => LabelOf(button) == "Refresh"));
        WaitForReads(reads);
    }

    /// <summary>
    /// Lets the dispatcher work until the source has finished this many reads and the screen shows the result: no "Loading…" and a
    /// status line. The screen is looked at after every operation the dispatcher completes, so the wait ends as soon as the window
    /// has rendered what it read, never earlier. The time limit only bounds a failure, which it reports.
    /// </summary>
    public void WaitForReads(int count)
    {
        var window = Window;
        var dispatcher = window.Dispatcher;
        var finished = Services.Source.WhenReadsFinished(count);
        var frame = new DispatcherFrame();
        var timedOut = false;

        void Check()
        {
            if (frame.Continue && finished.IsCompleted && ShowsAResult(window))
            {
                frame.Continue = false;
            }
        }

        void OnOperationCompleted(object? sender, DispatcherHookEventArgs e) => Check();

        var timeout = new DispatcherTimer(
            Limit,
            DispatcherPriority.Normal,
            (_, _) =>
            {
                timedOut = true;
                frame.Continue = false;
            },
            dispatcher);
        dispatcher.Hooks.OperationCompleted += OnOperationCompleted;
        try
        {
            _ = finished.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Background, Check), TaskScheduler.Default);
            Check();
            if (frame.Continue)
            {
                Dispatcher.PushFrame(frame);
            }
        }
        finally
        {
            timeout.Stop();
            dispatcher.Hooks.OperationCompleted -= OnOperationCompleted;
        }

        if (timedOut)
        {
            Xunit.Assert.Fail($"The window never showed the result of read {count} within {Limit}. It shows: {string.Join(" | ", TextsOf(window))}");
        }
    }

    // A read's result is on screen when the list no longer says "Loading…" and the status line says what the list holds or why it can't.
    private static bool ShowsAResult(Window window)
    {
        var texts = TextsOf(window);
        return !texts.Contains("Loading…")
            && texts.Any(text => text.StartsWith("Showing", StringComparison.Ordinal) || text.StartsWith("The log can't be read", StringComparison.Ordinal));
    }

    private static List<string> TextsOf(Window window)
    {
        window.UpdateLayout();
        return [.. Visuals(window).OfType<TextBlock>().Where(block => block.IsVisible).Select(OpenWindows.TextOf)];
    }

    public void Dispose() => _windows.Dispose();

    private void Refresh()
    {
        var window = _windows.Visible().Single();
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }
}
