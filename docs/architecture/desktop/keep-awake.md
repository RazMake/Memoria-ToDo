# Keep awake: how the desktop app keeps the screen on

Status: Decided

Keep awake stops Windows from starting the screen saver, turning the screen off, or locking the PC for lack of input while the user wants it to. The user turns it on and off with Prevent screen saver in [Configure](configure.md#the-behavior-section-holds-the-apps-switches), and Task View's [status bar](task-list.md#a-status-bar-holds-the-sync-button-and-the-version) shows whether it's on. The user asked for it, and decided the module, which locks pause it, that it keeps the system awake too, and how the status bar shows it.

## Behavior

### Prevent screen saver keeps the screen on while the app runs

The user asked for a setting that keeps the screen from timing out, through Windows' normal way of doing it. Proposed, each a decision to confirm:

- **While it's on, the app asks Windows to keep the display on.** It uses a Windows power request, made with [PowerCreateRequest](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powercreaterequest) and set with [PowerSetRequest](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powersetrequest) for the display, and for the system too, as the user decided, so the PC doesn't sleep while the screen stays on. While the display request holds, Windows starts no screen saver, doesn't turn the screen off, and doesn't lock the session for lack of input ([POWER_REQUEST_TYPE](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntpoapi/ne-ntpoapi-_power_request_type)).
- **The request carries a reason**, "Memoria-ToDo: Prevent screen saver is on", which `powercfg /requests` shows, so a user who wonders why the screen stays on can find out.
- **It holds only while the app runs.** Windows drops a process's power requests when it ends, so a crash never leaves the screen forced on. Exit and an update's restart release it first, as [Shell's shutdown](shell.md#shutdown-joins-owned-work-without-blocking-the-dispatcher) does for every lifetime.
- **It doesn't stop a sleep the user asks for.** Closing a laptop's lid, the power button, and Sleep in the Start menu still work, since a power request only stops what idle time would do.
- **It's off at first**, and it's this device's setting, never synced, since each PC has its own screen and power rules.
- **It's kept in `prevent-screen-saver.txt` in this device's folder**, `%LocalAppData%\Memoria-ToDo`, beside Updates' `auto-update.txt` and the MCP server's `mcp-server.txt`, through the platform's [on-or-off setting file](README.md#an-on-or-off-setting-file-keeps-one-switch-on-this-device). A missing file means off, and a damaged one counts as off. Its messages call it "Prevent screen saver's file".
- **A switch applies at once**: turning it on sets the request, unless the session is locked, and turning it off clears it.

Not confirmed by a run:

- That the display request also stops the lock that an organization's "machine inactivity limit" policy sets, which may work apart from the screen saver.
- That a request set while a remote desktop session is connected keeps the remote session's screen on.

### Locking the PC pauses it until the PC is unlocked

The user decided that while the feature is on, locking the PC turns it off until the PC is unlocked, when it comes back. Locking changes only what the app asks of Windows, never the setting. The user also decided which locks pause it; the rest is proposed, each a decision to confirm:

- **Any lock pauses it**: Win+L, Lock from Ctrl+Alt+Del, switching to another Windows user, a remote desktop session that disconnects, and a lock set by policy. Windows reports all of them the same way, so the app can't tell a lock by hand from another kind ([WTSRegisterSessionNotification](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification)). While the display request holds, Windows doesn't lock for lack of input, so in practice a lock is one the user or an administrator chose.
- **While paused, the app holds no request**, so the lock screen times out and the screen turns off as Windows' settings say.
- **Unlocking resumes it**, setting the request again when the setting is still on.
- **A start while the session is locked starts paused.** This happens when the [night run](updates.md#auto-update-updates-each-night-once-no-window-is-open) restarts the app into a new version at 3 AM. Without this rule the screen would stay on all night.

### The status bar shows whether it's on

The user asked for an icon with a tooltip in the status bar that shows whether the feature is on, and decided it's a sun in Task View's status bar only, which isn't a button, as the [Task list](task-list.md#a-status-bar-holds-the-sync-button-and-the-version) says. Keep awake publishes its state; the Task list draws the icon. The state is one of:

| State | When | Proposed tooltip |
| --- | --- | --- |
| Off | The setting is off | "Prevent screen saver is off." |
| On | The setting is on, and the app holds the request | "Prevent screen saver is on: the screen stays on." |
| Paused | The setting is on, and the session is locked | "Prevent screen saver is paused while the PC is locked." |
| Failed | The setting is on, and Windows refused the request, or the setting can't be read | "Prevent screen saver isn't working:" followed by the reason |

Paused rarely shows, since nobody sees Task View while the PC is locked, but the state is real for the log and for tests.

### Every change of state is logged

Keep awake logs what [Logging's table](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens) lists for it, under the component name "Keep awake". Proposed, each a decision to confirm:

- **At Information**: the setting turned on or off; the request set or cleared, with why: the setting, a lock, an unlock, the start, or the stop.
- **At Warning**: a setting file that can't be read, which the on-or-off setting file logs; a lock state Windows can't give, after which the app counts the session as unlocked.
- **At Error**: Windows refusing to set or clear the request, with the exception.
- **At Debug**: the start's choice, naming the saved setting and the lock state it found.

## Public interface

Keep awake is a new desktop module, `Memoria.ToDo.Desktop.KeepAwake`, which uses only the platforms. Shell starts and stops it, Configure shows and changes its setting, and the Task list shows its state, as the [module map](../modules.md#module-map) says. Its rules live here, and Windows sits behind two ports the composition root implements, so its unit tests run on fakes. The user decided the module itself; the parts below are proposed, each a decision to confirm.

```csharp
namespace Memoria.ToDo.Desktop.KeepAwake;

public static class KeepAwake
{
    /// Creates Keep awake. Reads nothing and asks Windows nothing until Start.
    /// Throws ArgumentNullException for a null argument, and ArgumentException when deviceFolder
    /// isn't fully qualified.
    public static IKeepAwake Create(IDisplayRequest display, ISessionLock session, IFileSystem files,
        string deviceFolder, ILogger logger);
}

/// Keep awake as the composition root holds it.
public interface IKeepAwake : IKeepAwakeStatus, IKeepAwakeLifetime { }

/// What Shell uses.
public interface IKeepAwakeLifetime
{
    /// Reads the setting and the lock state, sets the request when the setting is on and the session
    /// unlocked, and follows locks from now on. Called once, on the UI thread. A setting it can't read,
    /// a lock state Windows can't give, or a request Windows refuses shows in State, never as a failure.
    void Start();

    /// Stops following locks and clears the request. Repeated calls do nothing. Logs a failure at
    /// Error before throwing it, as every lifetime's cleanup does.
    void Stop();
}

/// What Configure and the Task list use.
public interface IKeepAwakeStatus
{
    /// The state now.
    KeepAwakeState State { get; }

    /// Raised on the UI thread after State changes.
    event EventHandler? Changed;

    /// Turns the setting on or off and saves it on this device. Fails, keeping the previous value,
    /// when the setting can't be saved. A request Windows refuses doesn't fail the call: it shows in
    /// State.Failure. Before Start and after Stop, it only saves the setting and leaves the request alone.
    void SetOn(bool on);
}

/// IsOn is the setting. Paused is true while the setting is on and the session is locked. Failure says
/// why the setting can't be read or Windows refused the request; null when nothing failed.
public sealed record KeepAwakeState(bool IsOn, bool Paused, string? Failure);

/// Windows' power request for the display, and the system while it's on. The composition root implements it.
public interface IDisplayRequest
{
    /// Sets or clears the request. Throws Win32Exception when Windows refuses.
    void Set(bool on);
}

/// Whether this Windows session is locked. The composition root implements it.
public interface ISessionLock
{
    /// True while the session is locked or disconnected. Throws Win32Exception when Windows can't say.
    bool IsLocked { get; }

    /// Raised on the UI thread after the session locks, unlocks, disconnects, or reconnects.
    event EventHandler? Changed;
}
```

Proposed, each a decision to confirm:

- **The composition root's lock adapter asks Windows for the lock state after each session change**, rather than tracking the events, so a missed or repeated event can't leave the state wrong. It follows the events through [SystemEvents.SessionSwitch](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionswitch), which .NET's Windows desktop framework already has, and reads the state with [WTSQuerySessionInformation](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsquerysessioninformationw) and `WTSSessionInfoEx`.
- **Each port has one hand-written fake shared by every test**, as the [technology stack](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test) says. The real adapters have integration tests; a test can't lock the PC, so the lock adapter's events are checked by hand.

## Failure modes

- **Windows refuses the request**: the state is Failed with Windows' reason; Configure shows it under Prevent screen saver in the error color, and the status bar icon shows it in its tooltip. The setting stays on, and the next lock and unlock, or turning the setting off and on, tries again. Logged at Error.
- **Windows can't clear the request**: the state is Failed with the reason; the screen may stay on until the app ends, when Windows drops the request. Logged at Error.
- **The setting file can't be read, or holds something else**: the setting counts as off, as the on-or-off setting file decides, and Configure shows why. Turning it on writes the file again.
- **The setting can't be saved**: the check box keeps its previous value, and Configure says why, as it does for [every setting](configure.md#failure-modes).
- **Windows can't say whether the session is locked**: the app counts it as unlocked, so the setting works as the user set it, and logs a Warning.
- **The lock events stop arriving**: the request stays as it was until the next event or the app's end. No test can make Windows stop sending them; this is an accepted risk.

## Ruled out

- **[SetThreadExecutionState](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate)**: it binds the request to the thread that calls it, so the call must come from the UI thread and stay there, and its documentation says it doesn't stop the screen saver. A power request belongs to its handle and carries a reason Windows shows.
- **Moving the mouse or pressing a key now and then**: it fakes input, which can wake other apps or type into them.
- **Shell owns it**: then Configure and the Task list couldn't reach its state, since Shell uses them.
- **The platform owns it**: the platform holds code every module shares, not a capability with its own setting and rules, as [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) says.
- **Inside Updates or the MCP server**: a module that owns another capability would change for two reasons, and Configure and the Task list would depend on it for a setting that isn't its own.
- **The composition root alone**: its rules, such as pausing on a lock and starting paused, would have no unit tests, and Configure and the Task list would need the state passed through by hand.
- Which locks pause it:
  - **Only a lock by hand**: Windows reports every lock the same way, so the app would have to guess.
  - **Locks, but not a remote desktop session that disconnects**: nobody sees a disconnected session's screen, so keeping it on only wastes power.
- What it keeps awake:
  - **The display only**: Windows' sleep timer could still put the PC to sleep while the screen is meant to stay on.

## Open questions

None.
