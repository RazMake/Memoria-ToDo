using System.IO;
using System.Text;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests;

/// <summary>
/// Updates built on the shared fakes of its ports, as its tests use it: an installed copy at 2.1.0 with no newer version,
/// a clock at 2026-01-01 00:00 UTC whose local time zone is UTC (so 3:00 local is 03:00 UTC), and no window open.
/// Build it on the test's UI thread, as the composition root builds Updates on the app's. Shared test setup, not a fake of a port.
/// </summary>
internal sealed class UpdatesHarness
{
    public const string DeviceFolder = @"C:\device";

    private readonly List<FakeDescriptionWebView> webViews = [];

    public UpdatesHarness(DateTimeOffset? start = null, TimeZoneInfo? zone = null)
    {
        Time = new FakeTimeProvider(start);
        Time.UseLocalTimeZone(zone ?? TimeZoneInfo.Utc);
        Updates = Create(Install, Files, DeviceFolder);
    }

    public static string AutoUpdateFile { get; } = Path.Combine(DeviceFolder, "auto-update.txt");

    public FakeAppInstall Install { get; } = new();

    public FakeAppExit Exit { get; } = new();

    public FakeFileSystem Files { get; } = new();

    public FakeTimeProvider Time { get; }

    public FakeTheme Theme { get; } = new();

    public FakeDefaultBrowser Browser { get; } = new();

    public CloseWindowKeys CloseWindowKeys { get; } = new();

    /// <summary>The entries Updates logged, through the logger it was given.</summary>
    public InMemoryLogSink Log { get; } = new();
    /// <summary>When set, each web view made from now on fails when it is asked to show a page.</summary>
    public string? WebViewFailure { get; set; }

    /// <summary>The web views What is new asked for, oldest first.</summary>
    public IReadOnlyList<FakeDescriptionWebView> WebViews => webViews;

    public IAppUpdates Updates { get; }

    /// <summary>Calls <see cref="AppUpdates.Create"/> on these fakes, for a test that changes an argument.</summary>
    public IAppUpdates Create(IAppInstall install, IFileSystem files, string deviceFolder) =>
        AppUpdates.Create(install, files, deviceFolder, Time, Theme, MakeWebView, Browser, CloseWindowKeys, Log.CreateLogger());

    /// <summary>A newer version whose notes list only itself.</summary>
    public static NewerVersion Newer(string version) => new(version, [new ReleaseNotes(version, $"Notes of {version}.")]);

    /// <summary>Starts Updates, as Shell does.</summary>
    public void Start() => Updates.Start(Exit);

    /// <summary>Writes Auto update's file, as an earlier run would have.</summary>
    public void WriteAutoUpdateFile(string text) => Files.WriteFile(AutoUpdateFile, Encoding.UTF8.GetBytes(text));

    /// <summary>Starts with a newer version found, and waits until the state shows it.</summary>
    public async Task StartWithVersionAvailableAsync(string version = "2.2.0")
    {
        Install.Found = Newer(version);
        Start();
        await WaitForStateAsync(state => state.Stage == UpdateStage.Available);
    }

    /// <summary>Starts with no newer version, and waits until the first check ended and a timer waits for the next.</summary>
    public async Task StartAndFirstCheckDoneAsync()
    {
        Start();
        await Install.WaitForCompletedChecksAsync(1);
        await Pump();
        await Time.WaitForActiveTimerAsync();
    }

    /// <summary>Moves the clock forward one hour at a time, each time after the code under test armed its timer, so every look it makes at the clock happens.</summary>
    public async Task AdvanceHourlyAsync(int hours)
    {
        for (var hour = 0; hour < hours; hour++)
        {
            await Time.WaitForActiveTimerAsync();
            Time.Advance(TimeSpan.FromHours(1));
        }
    }

    /// <summary>Lets the work already queued on the UI thread run, such as what follows a call that has just ended.</summary>
    public static async Task Pump() => await Dispatcher.Yield(DispatcherPriority.Background);

    /// <summary>Waits until the state satisfies the condition. Fails with the state it saw when that takes too long.</summary>
    public async Task<UpdateState> WaitForStateAsync(Func<UpdateState, bool> condition)
    {
        var matched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs e)
        {
            if (condition(Updates.State))
            {
                matched.TrySetResult();
            }
        }

        Updates.Changed += OnChanged;
        try
        {
            if (!condition(Updates.State))
            {
                await matched.Task.WaitAsync(TestWait.ForSignal);
            }
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The state never matched. It is {Updates.State}.");
        }
        finally
        {
            Updates.Changed -= OnChanged;
        }

        return Updates.State;
    }

    private FakeDescriptionWebView MakeWebView()
    {
        var view = new FakeDescriptionWebView { FailWhenShowing = WebViewFailure };
        webViews.Add(view);
        return view;
    }
}
