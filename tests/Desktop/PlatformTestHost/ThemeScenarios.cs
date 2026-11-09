using System.Windows;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The theme scenarios. Only <c>theme-read</c> uses Windows' own setting, read-only; every other scenario uses a fake
/// setting, so none changes the user's theme. They print:
/// <c>theme is-dark=True|False</c>, <c>theme-create-error=&lt;exception type&gt;</c>,
/// <c>theme-source reads=&lt;n&gt; listeners=&lt;n&gt;</c> (what the fake setting saw),
/// <c>theme-changed ui-thread=True|False is-dark=True|False</c> (each Changed),
/// <c>theme-settled changes=&lt;n&gt; is-dark=True|False</c> (once the UI thread had nothing left to do), and
/// <c>theme-listeners running=&lt;n&gt;</c> / <c>theme-listeners after-shutdown=&lt;n&gt;</c>.
/// </summary>
internal static class ThemeScenarios
{
    // Start reads the theme from Windows' own setting and says what it is, then ends the app.
    public static void Read(Action endApp)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            ScenarioLifetime.Say($"theme is-dark={new WindowsTheme(new WindowsThemeSetting()).IsDark}");
            endApp();
        });
    }

    // Start makes the theme while Windows switches to dark right after the theme's first read of the setting.
    public static void SwitchDuringCreation(Action endApp)
    {
        var setting = new FakeThemeSetting();
        setting.SwitchAfterNextRead(true);
        OnUiThread(() => Settle(new Watched(setting), endApp));
    }

    // Start switches the setting to dark from a background thread.
    public static void SwitchFromBackgroundThread(Action endApp)
    {
        var setting = new FakeThemeSetting();
        OnUiThread(() =>
        {
            var watched = new Watched(setting);
            var thread = new Thread(() =>
            {
                setting.Switch(true);
                Settle(watched, endApp);
            });
            thread.Start();
        });
    }

    // Start switches the setting to dark and straight back, before the UI thread looks at it.
    public static void SwitchAndBackUnseen(Action endApp)
    {
        var setting = new FakeThemeSetting();
        OnUiThread(() =>
        {
            var watched = new Watched(setting);
            setting.Switch(true);
            setting.Switch(false);
            Settle(watched, endApp);
        });
    }

    // Start makes a Changed handler that throws, then switches the setting from a background thread. Nothing ends the app.
    public static void ChangedHandlerThrows(Action endApp)
    {
        var setting = new FakeThemeSetting();
        OnUiThread(() =>
        {
            new Watched(setting).Theme.Changed += (_, _) => throw new InvalidOperationException("changed boom");
            ScenarioLifetime.Say("theme-created");
            new Thread(() => setting.Switch(true)).Start();
        });
    }

    // Start makes the theme, then makes reading fail and switches the setting from a background thread. Nothing ends the app.
    public static void ReadFailsWhileRefreshing(Action endApp)
    {
        var setting = new FakeThemeSetting();
        OnUiThread(() =>
        {
            _ = new Watched(setting);
            ScenarioLifetime.Say("theme-created");
            setting.ReadFailure = new InvalidOperationException("read boom");
            new Thread(() => setting.RaiseMayHaveChanged()).Start();
        });
    }

    // Start makes the theme, says how many listeners the setting has, and ends the app; once the UI thread has shut down, says it again.
    public static void ListenersAcrossShutdown(Action endApp)
    {
        var setting = new FakeThemeSetting();
        OnUiThread(() =>
        {
            _ = new Watched(setting);
            ScenarioLifetime.Say($"theme-listeners running={setting.Listeners}");
            Application.Current.Dispatcher.ShutdownFinished += (_, _) =>
                ScenarioLifetime.Say($"theme-listeners after-shutdown={setting.Listeners}");
            endApp();
        });
    }

    // Start makes the theme on a thread that isn't the UI thread, and says what it threw and what the setting saw.
    public static void CreateOffUiThread(Action endApp)
    {
        var setting = new FakeThemeSetting { ReadFailure = new InvalidOperationException("The setting must not be read.") };
        var thread = new Thread(() =>
        {
            try
            {
                _ = new WindowsTheme(setting);
                ScenarioLifetime.Say("theme-create-error=none");
            }
            catch (Exception e)
            {
                ScenarioLifetime.Say($"theme-create-error={e.GetType().Name}");
            }
            finally
            {
                ScenarioLifetime.Say($"theme-source reads={setting.Reads} listeners={setting.Listeners}");
                endApp();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void OnUiThread(Action action) => Application.Current.Dispatcher.BeginInvoke(action);

    // Once the UI thread has nothing left to do, so every refresh it was handed has run, says what the theme did.
    private static void Settle(Watched watched, Action endApp) =>
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            ScenarioLifetime.Say($"theme-settled changes={watched.Changes} is-dark={watched.Theme.IsDark}");
            endApp();
        });

    // A theme made on the UI thread, with its Changed events counted and reported.
    private sealed class Watched
    {
        public Watched(IThemeSetting setting)
        {
            Theme = new WindowsTheme(setting);
            Theme.Changed += (_, _) =>
            {
                Changes++;
                ScenarioLifetime.Say(
                    $"theme-changed ui-thread={Application.Current.Dispatcher.CheckAccess()} is-dark={Theme.IsDark}");
            };
        }

        public WindowsTheme Theme { get; }

        public int Changes { get; private set; }
    }
}
