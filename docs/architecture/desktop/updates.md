# Updates: how the desktop app tells you about a new version and installs it

Status: Decided

Updates checks this repository's GitHub Releases for a newer version of the desktop app. It tells you through a status bar in Add and Task View and a card in Configure. It installs the new version when you ask, or each night when you turn Auto update on. The user decided that Updates is a desktop module of its own, as the [module map](../modules.md#module-map) lists it. Today [Shell](shell.md#velopack-installs-the-app-and-keeps-it-up-to-date) checks and downloads updates in the background.

## Behavior

### Updates tells you about a new version and installs it only when you agree

The app always knows one of four things about updates, and every part that shows updates shows the same one:

| State | When | What you see |
| --- | --- | --- |
| Not installed | The app runs from a build folder, which [Velopack](https://docs.velopack.io/reference/cs/Velopack/UpdateManager) can't update | The running version, and that updates need an installed copy |
| No update | No check has found a newer version yet | The running version |
| Available | A check found a newer version | The new version, in the [status bar](#add-and-task-view-show-an-update-in-a-shared-status-bar) and on Configure's [Update card](configure.md#the-update-card-leads-the-application-section) |
| Updating | An update to the new version is under way | "Updating to" the new version, with the update controls turned off |

Proposed, each a decision to confirm:

- **A check only looks; it never downloads.** Today the app downloads each newer version in the background and Velopack applies it at the next start. That would update the app with Auto update off, so it's replaced. A version downloads only when an update starts.
- **A downloaded version is applied only by an update Updates starts, never at the next start.** The composition root turns off Velopack's apply at start ([SetAutoApplyOnStartup](https://docs.velopack.io/reference/cs/Velopack/VelopackApp)). So a version downloaded for an update you then cancelled waits for your next update, and a second start of the app can't apply it and end the running copy.
- **The app ignores GitHub prereleases and uses Velopack's default channel**, as before.

### A check runs at start and each night at 3 AM

Updates checks once when the app starts, then each night at 3:00 by the PC's local time. Proposed, each a decision to confirm:

- **One schedule replaces the check every 24 hours.** At 3:00 a PC that runs all night checks, and a PC that's off at night checks when the app starts at sign-in. Time zones and daylight saving time move the run with the local clock, so it stays at 3:00. Changing the PC's time zone while the app runs moves the next run to 3:00 in the new zone, within an hour.
- **A night run missed while the PC slept runs soon after it wakes.** Updates looks at the clock at least once an hour while the PC is awake, as the old schedule did, since on Windows 8 and later a timed wait doesn't count time asleep ([WaitForSingleObject](https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject)). Changing the PC's clock moves the next run as much.
- **A failed check changes nothing and shows nothing.** The state stays as it was, and the next check tries again.
- **A check doesn't run while an update is under way**, and a check that was already running when the update started doesn't change the version it installs.
- **A check that finds no newer version after one was found returns to No update.** That happens when a release is deleted.
- **The first check does no slow work before its first wait**, since Shell starts Updates on the UI thread while the app starts.
- **Stopping the app also stops a check or a download under way.** Velopack's check can't be cancelled, so Updates stops waiting for it and lets its request end in the background; a download is cancelled.

### Add and Task View show an update in a shared status bar

Add and Task View show the same status bar along their bottom edge. Updates owns it, so the two windows share one component. Task View puts its [Sync button](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button) at the bar's start; Add has no Sync button. The bar's start takes the width the update text leaves, so Task View's [version](task-list.md#a-status-bar-holds-the-sync-button-and-the-version) can sit at its right, just before the update text.

- **While a version is available, the bar's end says "Update available: 2.2.0"**, with "Update available" in bold and the whole text in the theme's caution color, a yellow in the dark theme ([Fluent theme colors](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Resources/Theme/Dark.xaml)). The text is a button: a click, or Enter or Space after Tab reaches it, opens [What is new](#what-is-new-shows-each-newer-releases-notes) over the bar's window, the user decided. It doesn't start the update; Update Now and the night run do, as [below](#an-update-downloads-the-version-then-restarts-the-app-into-it) says. It shows the hand pointer, as [every button in the app](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it) does.
- **A small gap separates the update text from what comes before it**, so it never touches Task View's version. The user decided the gap is 5 device-independent pixels. Proposed: the bar puts the gap on the update text's own left side, so Add and Task View get it from the one component, and Task View's version keeps its margins. This is a decision to confirm.
- **While updating, it says "Updating to 2.2.0"**, with the version in bold, in the theme's secondary text color, so it reads as muted, and it's no longer a button.
- **Otherwise the update text is gone.** Proposed: Add's bar then hides, so Add looks as it does today; Task View's bar stays for its Sync button. This is a decision to confirm.
- **The bar follows the state as it changes**, while the window is open.

The user asked for a shorter bar in a color of its own. Proposed, each a decision to confirm:

- **The bar is one line tall.** Its padding is small, so it's no taller than a line of text and the compact controls it holds, such as Task View's [Sync button](task-list.md#task-view-syncs-when-it-opens-and-on-its-sync-button). Proposed: 2 device-independent pixels of padding above and below its tallest content. This is a decision to confirm.
- **The bar's background is slightly different from the window's**, so it reads as a strip of its own. The color is a theme resource, not a color of ours, so it follows the light and dark themes. Proposed: the Fluent theme's `CardBackgroundFillColorDefaultBrush`, which Task View's [search bar](task-list.md#the-search-bar-spans-the-window-in-its-own-shade) uses too. This is a decision to confirm.
- **Add's bar looks the same**, since it's the same component.

Proposed: the bar runs along the windows' bottom edge, as a status bar does, so Task View's Sync button and its failure line move there from the window's top. This is a decision to confirm.

### An update downloads the version, then restarts the app into it

Update Now on the [Update card](configure.md#the-update-card-leads-the-application-section) and the [night run](#auto-update-updates-each-night-once-no-window-is-open) start the same update. It does nothing unless a version is available. In order, it:

1. turns the state to Updating, so the status bar and the card show it;
2. downloads the version, in the background, while the app keeps working;
3. asks Task View to close, as its close button does, so it [asks first](task-list.md#closing-task-view-asks-before-it-drops-a-changed-edit) about a changed edit. When you choose Cancel, or Save doesn't save, Task View stays open, the state returns to Available, and nothing else happens. Add and Configure close with the app in the next step, and Add keeps its [draft](task-editor.md#closing-without-saving-keeps-a-draft-on-this-device), as on Exit;
4. ends the app through its normal exit, as Exit in the tray menu does, so Shell stops sync and the shortcuts as it always does;
5. as the exit's last step before the app lets go of its one-instance claim, starts Velopack's updater. The updater waits for the app to end, applies the version, and starts the app again ([WaitExitThenApplyUpdates](https://docs.velopack.io/reference/cs/Velopack/UpdateManager)).

Proposed, each a decision to confirm:

- **The app restarts at once, rather than at the next start.** "Updating to" would otherwise show until you next exit the app or sign in again.
- **The updater starts as late as possible.** It waits only 60 seconds for the app to end, then gives up ([WaitExitThenApplyUpdates](https://docs.velopack.io/reference/cs/Velopack/UpdateManager)), so it starts after Shell has stopped sync, the slowest step.
- **An update you start restarts the app as a start by hand does**, so Task View shows, as Shell's [startup rules](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view) say, and Velopack shows its progress window while it applies the version.
- **The night run restarts the app as a start at sign-in does**: Velopack shows no window, and the app comes back with only its tray icon. Not confirmed: that Velopack passes the sign-in argument on after an update, which Shell [lists for a check by hand](shell.md#only-one-instance-runs-and-a-start-by-hand-shows-task-view).
- **Velopack's [ApplyUpdatesAndRestart](https://docs.velopack.io/reference/cs/Velopack/UpdateManager) isn't used**: it ends the process at once, so sync would stop without its cleanup.

### Auto update updates each night, once no window is open

Auto update is a check box on the [Update card](configure.md#the-update-card-leads-the-application-section). While it's on, each night run that finds a newer version starts the update by itself, and the card hides Update Now.

Proposed, each a decision to confirm:

- **Auto update is off at first**, as the request's empty check box shows. It's this device's setting, kept on this device, since each PC decides when it restarts. It isn't synced.
- **It's kept in `auto-update.txt` in this device's folder**, `%LocalAppData%\Memoria-ToDo`, beside [`repository-folder.txt`](configure.md#the-sync-settings-section-sets-the-devices-clone), as `on` or `off`. A missing file means off.
- **The night run waits while any of the app's windows is open**, and starts within an hour after the last one closes. So it never closes a window under you, and it never meets Task View's prompt about a changed edit. Proposed, each a decision to confirm:
  - **A window opened during the night run's download holds the restart** until every window is closed again; the state stays Updating meanwhile.
  - **Turning Auto update off cancels a night run that's still waiting** for windows to close; one already downloading goes on, as an update you started would.
  - **A night run that's waiting doesn't stop the nightly checks**, so a newer version found meanwhile replaces the one it waits to install.
- **Turning Auto update on doesn't start an update.** A version already available waits for the night run. While Auto update is on, the card hides Update Now, so turn it off to update sooner.
- **A failed night update tries again the next night.**

### What is new shows each newer release's notes

What is new on the update card, and the status bar's "Update available" text, open a dialog with the notes of every release after the running version, up to the new one, newest first. Each release's notes sit under a heading with its version.

The user decided that **the notes come from the [changelog](../../contributing/changelog.md)**, `CHANGELOG.md` at the repository's root, written for people who use the app. Each release's notes are its version's section there. The release job packs that section into the release's Velopack files with `vpk pack --releaseNotes` ([release notes](https://docs.velopack.io/integrating/release-notes)), and refuses to publish without it, as [Version tags](../version-tags.md#a-version-is-tagged-only-when-the-changelog-has-its-notes) says.

Proposed, each a decision to confirm:

- **The notes come with the check**, so the dialog opens at once, with no network wait. Updates reads each newer release's notes from the same Velopack files the check reads ([VelopackAsset](https://docs.velopack.io/reference/cs/Velopack/VelopackAsset)), with no second request to GitHub. Velopack 1.2.0's GitHub source reads only the latest 10 releases. Proposed, a decision to confirm: when those don't reach the running version, the dialog shows them and ends with a link to this repository's GitHub Releases page for older notes, rather than making more requests to GitHub.
- **The dialog shows the notes as a formatted page, through the [Formatted view](formatted-view.md#public-interface)**, as Task View shows a description. Web links open in the default browser, and when the web view fails, the dialog shows the Markdown source with a message saying why, as the Formatted view does.
- **A release without notes, such as one published by hand, says "No notes for this version."** under its heading, rather than leaving a gap.
- **The dialog is modal, owned by the window it opens from**, Configure, Add, or Task View, the user decided, and centered on it, following the theme, and titled "What is new". A Close button, Esc, and the [Close window shortcut](README.md#the-close-window-shortcut-closes-any-of-the-apps-windows) close it.
- **The card's button shows while a version is available or updating.** Otherwise there's nothing new to show. The status bar opens it only while a version is available, since "Updating to" isn't a button.

### A copy run from a build folder shows its version and doesn't update

Velopack updates only a copy it installed ([UpdateManager](https://docs.velopack.io/reference/cs/Velopack/UpdateManager)). Proposed: a copy run from a build folder shows the version it was built with, says "Updates work only in an installed copy.", and turns Auto update off and greys it out. It never checks, so its status bar never shows an update. This is a decision to confirm.

### Velopack checks, downloads, and applies, behind one port

Updates reaches Velopack only through its install port, which the composition root implements with [Velopack](https://www.nuget.org/packages/Velopack/1.2.0) 1.2.0 and this repository's GitHub Releases, as Shell's adapter does today. The repository is public, so the check needs no token. So Updates' unit tests use a fake, and only the adapter's integration tests reach Velopack.

## Public interface

The composition root creates Updates once and passes it on: Shell starts and stops it, and Add, Task View, and Configure show its state. Proposed: the exact parts below. This is a decision to confirm.

```csharp
namespace Memoria.ToDo.Desktop.Updates;

public static class AppUpdates
{
    /// Creates Updates. Nothing checks until IUpdateLifetime.Start. The web view function makes a new web view
    /// each time What is new opens. Fails when an argument is null or deviceFolder isn't a full path.
    public static IAppUpdates Create(IAppInstall install, IFileSystem files, string deviceFolder, TimeProvider time,
        ITheme theme, Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, CloseWindowKeys closeWindowKeys);
}

/// Updates as the composition root holds it.
public interface IAppUpdates : IUpdateStatus, IUpdateLifetime { }

/// What Shell uses.
public interface IUpdateLifetime
{
    /// Reads Auto update and starts the checks: one now, then each night at 3:00 local time.
    /// Checks nothing when the app isn't installed. Called once.
    void Start(IAppExit exit);

    /// Cancels a check or a download under way and waits for it. When an update ended the app, starts
    /// Velopack's updater as its last step. Repeated calls join the same stop.
    Task StopAsync();
}

/// What Updates needs from Shell to restart the app. Shell implements it.
public interface IAppExit
{
    /// Whether any of the app's windows is open.
    bool AnyWindowOpen { get; }

    /// Closes Task View as its close button does. False when it stays open, such as when the user cancels
    /// its prompt about a changed edit. The other windows close with the app at End.
    bool CloseWindows();

    /// Ends the app through its normal exit, which calls IUpdateLifetime.StopAsync.
    void End();
}

/// What Add, Task View, and Configure use.
public interface IUpdateStatus
{
    /// The state now.
    UpdateState State { get; }

    /// Raised on the UI thread after State or AutoUpdate changes.
    event EventHandler? Changed;

    /// Whether Auto update is on.
    bool AutoUpdate { get; }

    /// Turns Auto update on or off and saves it on this device. Fails, keeping the previous value, when
    /// the setting can't be saved, or when the app isn't installed or is updating.
    void SetAutoUpdate(bool on);

    /// Starts an update to the available version. Does nothing unless State.Stage is Available.
    void UpdateNow();

    /// Opens What is new, modal and owned by the window. Does nothing unless a version is available or updating.
    void ShowWhatIsNew(Window owner);
}

public enum UpdateStage { NotInstalled, NoUpdate, Available, Updating }

/// The running version, the newer one when there is one, and why the last update or setting read failed, if it did.
public sealed record UpdateState(UpdateStage Stage, string CurrentVersion, string? NewVersion, string? Failure);

/// The status bar Add and Task View show along their bottom edge. Start, when given, sits at the bar's start,
/// and the bar always shows; without it, the bar shows only while a version is available or updating.
public sealed class UpdateStatusBar : FrameworkElement // the exact WPF base class is the coder's choice
{
    public UpdateStatusBar(IUpdateStatus updates, UIElement? start);
}
```

Updates publishes one port. The composition root implements it with Velopack, and Updates' unit tests use its [shared fake](../technology-stack.md#each-port-has-one-hand-written-fake-shared-by-every-test), which replaces Shell's fake of today's updates port:

```csharp
namespace Memoria.ToDo.Desktop.Updates;

/// The installed copy and the releases it can update to.
public interface IAppInstall
{
    /// The running version, such as "2.1.0": Velopack's when installed, otherwise the build's.
    string Version { get; }

    /// False when the app runs from a build folder.
    bool IsInstalled { get; }

    /// Checks GitHub Releases. Null when there's no newer version. Fails when the check fails.
    Task<NewerVersion?> CheckAsync(CancellationToken cancellationToken);

    /// Downloads a version the latest check found. Fails when the download fails or the check didn't find it.
    Task DownloadAsync(NewerVersion version, CancellationToken cancellationToken);

    /// Starts Velopack's updater, which waits for the app to end, applies the downloaded version, and starts
    /// the app again: as at sign-in and with no window when quiet, otherwise as a start by hand.
    void ApplyAfterExit(NewerVersion version, bool quiet);
}

/// A newer version, with the notes of each release after the running one, newest first. OlderNotesPage is the page
/// that holds every release's notes when the notes the check could read don't reach the running version, else null.
public sealed record NewerVersion(string Version, IReadOnlyList<ReleaseNotes> Notes, Uri? OlderNotesPage = null);

/// One release's notes in Markdown; null when it was published without any.
public sealed record ReleaseNotes(string Version, string? Markdown);
```

The time source is .NET's [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider), as for [every module](../modules.md#the-platform-serves-every-module-and-knows-none), including its local time zone for 3:00. Tests pass the shared fake, which moves time forward only when told; it gains a settable local time zone, so tests can check the night run across daylight saving time.

## Failure modes

- **A check fails**, such as when the PC is offline: nothing changes and nothing shows, and the next check tries again.
- **A download fails**: the state returns to Available, the update card says "The update to 2.2.0 failed:" followed by the reason, and the status bar shows "Update available" again. The night run tries again the next night.
- **You cancel Task View's prompt, or Save doesn't save**: the state returns to Available with no message, since you chose it.
- **Velopack's updater can't start**: the app still ends, as Exit does, and the error shows as any failed stop does, through the [platform](README.md#the-platform-runs-the-app-and-the-composition-root-builds-it). You start the app again, and its next check finds the version again.
- **The app takes longer than 60 seconds to end after the updater starts**: the updater gives up, so the app stays closed on its old version until you start it again. Velopack doesn't confirm a failure to the app. This is an accepted risk; the updater starts after the slow steps.
- **Auto update's file can't be read, or holds something other than `on` or `off`**: Auto update stays off, and the card says why. Turning it on writes the file again.
- **Auto update can't be saved**: the check box keeps its previous value, and the card says why, as Configure does for [every setting](configure.md#failure-modes).
- **The web view fails in What is new**: the dialog shows the notes' Markdown source with a message, as the [Formatted view](formatted-view.md#failure-modes) does.

## Ruled out

- When the app checks for updates:
  - **24 hours of the app running, not of the clock**: a PC that sleeps or is off most of the day would check far less than daily.
  - **A check every 24 hours, plus the night run**: two schedules for one job, and the night run already checks.
- What happens to a version a check finds:
  - **Download it in the background and apply it at the next start, as before**: the app would update with Auto update off.
  - **Download it in the background, and only apply it when you agree**: Update Now would be faster, but every PC would download versions it may never install.
- What the status bar's "Update available" text does:
  - **A click starts the update**: you chose What is new, so you read what changed first and update from Configure.
- How an update ends the app:
  - **Velopack's ApplyUpdatesAndRestart**: it ends the process at once, so sync would stop without its cleanup.
  - **Apply at the next exit or start, without a restart**: "Updating to" would show until then.
- How Updates reaches Velopack:
  - **It calls Velopack directly**: its rules could only be tested against GitHub, the reason the tray implementation left Shell.
  - **The composition root runs the checks**: the update rules would leave the modules, and the composition root only builds the app from them.
- Where the checks get the time:
  - **A timer port of our own**: it would repeat TimeProvider, which every module uses; [Modules](../modules.md#ruled-out) rules out a clock interface of ours.
- The night run:
  - **Close the windows at 3:00 even when they're open**: Task View's prompt about a changed edit would wait all night, and an update could start the moment you answer it in the morning.
  - **Skip a night run the PC slept through**: a laptop that sleeps every night would never update by itself.
- Where the update capability lives:
  - **Updates stay in Shell, and the platform publishes the update state and the status bar**: the platform would hold one capability's rules, which [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) keeps for code every module shares, and Shell would grow. You chose a module of its own.
  - **A new Updates module, which the window modules don't use**: the composition root would pass them plain functions that make Updates' elements, so their arguments would say nothing of what they get, and Configure's card look would move to the platform.
- Where the release notes come from:
  - **Notes written from the commit subjects since the previous tag**: subjects such as "Removed temporary code" aren't written for users, and include merge commits. You chose a changelog.
  - **GitHub's generated release notes, read from each release**: they list pull requests, which this repository doesn't use, and the app would make one more request to GitHub for each release, against its limit of 60 an hour without a token ([rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)).
