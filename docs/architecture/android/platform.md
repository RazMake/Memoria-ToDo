# Platform and composition root: how the Android app starts

Status: Decided

The Android platform, `Memoria.ToDo.Android.Platform`, holds what two or more Android screens share. The composition root, `Memoria.ToDo.Android.App`, is the app Android installs: it builds every module when the app starts, before Android opens any screen.

## Behavior

### The composition root builds the app before Android opens a screen

Android makes each screen, an activity, itself. Before it makes the first one, it creates the app's one `Application` object and calls its `OnCreate`, in every process, including a process Android starts again to bring back a screen it closed ([Application.onCreate](https://developer.android.com/reference/android/app/Application#onCreate()), [process lifecycle](https://developer.android.com/guide/components/activities/process-lifecycle)). So the composition root's `Application` class builds the whole app there: the logger first, then each module through its create method, and Shell last.

Proposed, each a decision to confirm:

- **The app's name has one home**, the `Product` property that already names the desktop app, as the [technology stack](../technology-stack.md#three-github-actions-workflows-check-pushes-run-every-test-and-publish-releases) says. The composition root's project passes it to Android as the app's label.
- **The composition root logs the app's start at Information, with its version**, once it has built every module, as the desktop's platform [logs its start](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens). Android ends the app's process without telling it, so there's no entry for the end.
- **An error no module handles is logged at Fatal from "App", then Android ends the app** and shows its own message that the app stopped. Fatal is the [level](../logging.md#five-levels-say-what-an-entry-records) for an error that ends the app. The desktop [keeps running](../logging.md#errors-no-module-handles-are-logged-before-theyre-shown) after such an error on its UI thread; the phone doesn't, since Android ends the process anyway. One hook, .NET's [UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception), sees each such error on any thread, so it's logged once.
- **The app ID is `com.razmake.memoriatodo`, set once, in the composition root's project.** Android updates an installed app only when the new APK has the same ID and signing key ([application ID](https://developer.android.com/build/configure-app-module#set-application-id)), so the ID can't change after the first release. You chose it on November 26, 2026, the form the [proof on a phone](README.md#a-proof-on-a-phone-comes-before-any-other-android-work) used.

### Each project builds for the PC and Android, except the composition root

The [technology stack](../technology-stack.md#each-android-module-builds-for-both-the-pc-and-android) decides that each Android module builds for both, and where each project lives. Proposed, each a decision to confirm:

- **The composition root builds only for Android.** It holds only what needs Android: the `Application` class, the manifest, the log's [sink](#the-log-goes-to-androids-system-log), and later the Android adapters, such as the Keystore key and libgit2. No test project references it, as no desktop unit test references the desktop's.
- **Each module's public interface uses no Android type**, so it's the same in both builds, and one pair of `PublicAPI` files serves both. Android-only code stays internal to its module. Android's build adds a public `Resource` class to every project, so `Directory.Build.props` sets `AndroidResourceDesignerClassModifier` to `internal`. A check build of step 1's declarations for both on November 26, 2026 passed the public API check only with that setting.
- **What a screen does when it's tapped sits in a plain class its activity calls**, built for both, so the module's own unit tests check it on the PC. The activity only passes each tap on and shows the result. What only Android does, such as opening and closing activities, Back, the theme, and the log's sink, is checked by hand in [delivery step 1](README.md#delivery-backlog), and by UX tests on an emulator from step 2.
- **An Android-only file's name ends in `.Android.cs`**, and `Directory.Build.props` leaves such files out of the PC build. .NET MAUI picks platform files by name the same way ([multi-targeting](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/configure-multi-targeting)). Android resources and the manifest build only for Android already.
- **Code names Android's own types through `using` directives at the top of the file, never as `Android.…` inside a namespace.** Inside `Memoria.ToDo.Android.Shell`, C# reads `Android.App.Activity` as `Memoria.ToDo.Android.App.Activity`, and the build fails; a `using Android.App;` above the namespace reads it from the global namespace. A probe build with the pinned SDK showed both on November 26, 2026. No type in the composition root may share a name with one of Android's, such as `Application`, since a match would compile and bind to ours.

### Screens get what they need from their module's create method

An activity's constructor takes no arguments, because Android calls it. So each module's create method stores what its screens need where its activities read it, through the platform's screen services below. Since the composition root runs before any screen, a screen Android brings back still finds them. You chose this on November 26, 2026. It keeps one screen per activity, as the [proof on a phone](README.md#a-proof-on-a-phone-comes-before-any-other-android-work) timed Add, and needs no new package:

- **Each kind of screen has one place for what it needs**, internal to its module. The module's create method sets it, and a later create replaces it, so each UX test passes its own fakes. Its cost: the place is static, so two UX tests of one screen can't run side by side.
- **A screen that finds nothing set fails at once**, naming what it needed, rather than showing an empty screen, as [Fail fast](../../../AGENTS.md#code-design) asks.

### Screens open through one opener the composition root supplies

Each screen's `Show` opens its activity, but it gets no Android context, so it can't start the activity from the screen that asked. Code outside an activity may start one only as a new task ([Context.startActivity](https://developer.android.com/reference/android/content/Context#startActivity(android.content.Intent))). That knowledge gets one home: the platform publishes a screen opener, which the composition root implements once with the app's context. Proposed, each a decision to confirm:

- **Each module's create method takes the opener**, and its `Show` asks the opener to open its activity. `Show` does the same in the PC build, so unit tests pass a fake opener and see the request. Nothing else calls `Show` on the PC, since the PC build has no app.
- **Each activity has a fixed name**, set through the `Name` of its `Activity` attribute from a constant its module keeps, and `Show` passes that constant to the opener. Without it, .NET names each activity from a hash of its .NET name ([Android manifest](https://learn.microsoft.com/en-us/dotnet/maui/android/manifest)), which the module couldn't name in the PC build, and which changes when the class moves. Names take the form `memoria.todo.<module>.<Screen>Activity`, such as `memoria.todo.taskeditor.AddActivity`.
- **The opener starts the activity from the app's context with Android's new-task flag, and the single-top flag so a screen already in front isn't opened twice.** The app's screens share one task by default, so the new-task flag puts the new screen on top of the one in front rather than in a task of its own, and Back returns to it ([tasks and the back stack](https://developer.android.com/guide/components/activities/tasks-and-back-stack)). A double tap on the floating Add button opens one Add, not two.

### Each screen keeps clear of the system bars

The app targets Android's newest API, so on Android 15 and later it draws behind the status bar and the navigation bar, and can't opt out ([edge-to-edge](https://developer.android.com/develop/ui/views/layout/edge-to-edge)). So each screen's layout sets Android's `fitsSystemWindows` attribute on its outermost element, which pads the screen by the bars' size: the top app bar sits below the status bar, and the floating Add button above the navigation bar. It needs no code. You chose this on November 26, 2026. Its cost: in landscape, a camera cutout at the screen's side may still touch the edge of the top app bar, which the hand check in [delivery step 1](README.md#delivery-backlog) looks for.

### The log goes to Android's system log

The composition root writes the log to Android's system log, Logcat, through a small Serilog sink of its own on `Android.Util.Log`, so it needs no new package. [Logging](../logging.md#the-android-app-passes-a-logger-too) left the phone's sink to this design, and the shared modules log as they do on the desktop. You chose this on November 26, 2026.

- **You read the log with `adb logcat` on a connected PC.** Android keeps only recent entries, so a problem on the phone is explainable only while it's connected, or soon after.
- **Each entry's tag is the component that wrote it**, such as "Task list", and its Logcat priority follows its level. Proposed, a decision to confirm.

## Public interface

The modules use two parts, proposed for [delivery step 1](README.md#delivery-backlog). The [screen opener](#screens-open-through-one-opener-the-composition-root-supplies) is proposed, a decision to confirm:

```csharp
namespace Memoria.ToDo.Android.Platform;

/// Opens the app's screens. The composition root implements it with Android's application context,
/// and tests pass a fake.
public interface IScreenOpener
{
    /// Opens the activity with this fixed name in front of the app's other screens, unless it's in front
    /// already. A name Android can't find throws.
    void Open(string activityName);
}

/// What one kind of screen needs. Android makes each screen itself and passes it nothing, so the screen's
/// module sets this from its create method, before Android can make the screen, and the screen reads it.
public sealed class ScreenServices<T> where T : class
{
    /// Sets what the screen needs, replacing what an earlier call set.
    /// Throws ArgumentNullException when services is null.
    public void Set(T services);

    /// What the screen needs. Throws InvalidOperationException, naming T, when nothing was set.
    public T Get();
}
```

The composition root implements the screen opener. It's the app, so nothing uses it.

## Failure modes

- **A screen opens before its module's create method ran**: it throws, naming what it needed. The error is logged as one no module handles, and Android ends the app.
- **The opener gets a name no activity has**: Android throws. The error is logged as one no module handles, and Android ends the app.
- **Building the app fails in `OnCreate`**: the failure is logged once, as an error no module handles, and Android ends the app before any screen shows.
- **The log can't be written**: the app carries on, as [Logging](../logging.md#a-log-that-cant-be-written-never-stops-the-app) decides.

## Ruled out

The options below were considered and rejected, grouped by decision.

- How each screen gets what it needs:
  - **One activity, with each screen as an AndroidX fragment made by a `FragmentFactory`** ([FragmentFactory](https://developer.android.com/reference/androidx/fragment/app/FragmentFactory)): every module's screen would become a fragment of Shell's one activity, and Add's start time, measured as its own activity, would need measuring again.
  - **Each activity asks the composition root's `Application` object**: every module would depend on the composition root, which [Modules](../modules.md#the-platform-serves-every-module-and-knows-none) forbids.
- Which application ID:
  - **`io.github.razmake.memoriatodo`**, built from `razmake.github.io`, a name you control through GitHub: offered, and not chosen.
- Where the log goes:
  - **Log files in private storage, like the desktop's**: only Android's backup tools or a later Logs screen could read them, and only the desktop's composition root may use the Serilog file packages, as the [architecture test](../technology-stack.md#architecture-tests-check-the-module-map) checks.
  - **No sink until a phone Logs screen is designed**: nothing on the phone would be explainable from the log, against [Logging](../logging.md#every-operation-failure-and-decision-is-logged-where-it-happens).
- How screens keep clear of the system bars:
  - **One helper in the Android platform that each screen calls**, padding for the bars and for cutouts in every orientation: the platform's public interface would hold an Android-only type, unlike every other part of it, so its PC and Android builds would need separate API lists.
