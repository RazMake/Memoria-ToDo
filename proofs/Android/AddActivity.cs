using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using Google.Android.Material.AppBar;

namespace Memoria.Proofs;

/// <summary>
/// The Add screen, as the Task editor designs it, timed from launch until it accepts typing. It keeps no history,
/// so opening it again while the process lives is a warm start, and its menu closes the app for a cold one. The menu
/// also opens Proof Run, since some launchers show only one of the app's two icons.
/// </summary>
[Activity(
    Label = "Proof Add",
    MainLauncher = true,
    NoHistory = true,
    Exported = true,
    WindowSoftInputMode = SoftInput.StateAlwaysVisible | SoftInput.AdjustResize)]
public sealed class AddActivity : AppCompatActivity
{
    private long createdNs;
    private bool cold;
    private long? firstFrameNs;
    private long? windowFocusNs;
    private long? keyboardVisibleNs;
    private long? keyboardShownNs;
    private bool showCalled;
    private bool saved;
    private EditText title = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        createdNs = SystemClock.ElapsedRealtimeNanos();
        cold = LaunchTimings.ClaimColdStart();
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.add);
        title = FindViewById<EditText>(Resource.Id.title)!;

        var toolbar = FindViewById<MaterialToolbar>(Resource.Id.toolbar)!;
        toolbar.Menu!.Add(0, 1, 0, "Close the app")!.SetShowAsAction(ShowAsAction.Never);
        toolbar.Menu!.Add(0, 2, 1, "Open Proof Run")!.SetShowAsAction(ShowAsAction.Never);
        toolbar.MenuItemClick += (_, e) =>
        {
            if (e.Item!.ItemId == 2)
            {
                StartActivity(new Intent(this, typeof(ProofActivity)).AddFlags(ActivityFlags.NewTask));
                return;
            }

            Save();

            // Removing every task, the proof screen's too, keeps Android from restarting the process to show it.
            foreach (var task in ((ActivityManager)GetSystemService(ActivityService)!).AppTasks!)
            {
                task.FinishAndRemoveTask();
            }

            Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
        };

        var observer = Window!.DecorView.ViewTreeObserver!;
        observer.AddOnDrawListener(new DrawListener(() => firstFrameNs ??= SystemClock.ElapsedRealtimeNanos()));
        observer.GlobalLayout += (_, _) =>
        {
            if (keyboardVisibleNs is null && KeyboardVisible())
            {
                keyboardVisibleNs = SystemClock.ElapsedRealtimeNanos();
                ReportFullyDrawn();
            }
        };
        ViewCompat.SetWindowInsetsAnimationCallback(title, new KeyboardAnimation(this));
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (!hasFocus || windowFocusNs is not null)
        {
            return;
        }

        windowFocusNs = SystemClock.ElapsedRealtimeNanos();
        if (!KeyboardVisible())
        {
            showCalled = true;
            WindowCompat.GetInsetsController(Window!, title)!.Show(WindowInsetsCompat.Type.Ime());
        }
    }

    protected override void OnStop()
    {
        Save();
        base.OnStop();
    }

    private bool KeyboardVisible() =>
        ViewCompat.GetRootWindowInsets(title)?.IsVisible(WindowInsetsCompat.Type.Ime()) == true;

    private void Save()
    {
        if (saved)
        {
            return;
        }

        saved = true;
        (long startNs, string from) = !cold
            ? (createdNs, "onCreate (misses the system's part)")
            : OperatingSystem.IsAndroidVersionAtLeast(33)
                ? (Android.OS.Process.StartRequestedElapsedRealtime * 1_000_000, "process start request")
                : (Android.OS.Process.StartElapsedRealtime * 1_000_000, "process fork");
        double? Ms(long? ns) => ns is null ? null : (ns.Value - startNs) / 1e6;

        LaunchTimings.Append(this, new Launch(
            DateTime.UtcNow,
            cold,
            from,
            Ms(createdNs),
            Ms(firstFrameNs),
            Ms(windowFocusNs),
            Ms(keyboardVisibleNs),
            Ms(keyboardShownNs),
            showCalled));
    }

    private sealed class DrawListener(Action onDraw) : Java.Lang.Object, ViewTreeObserver.IOnDrawListener
    {
        public void OnDraw() => onDraw();
    }

    private sealed class KeyboardAnimation(AddActivity screen)
        : WindowInsetsAnimationCompat.Callback(DispatchModeContinueOnSubtree)
    {
        public override WindowInsetsCompat OnProgress(
            WindowInsetsCompat? insets,
            IList<WindowInsetsAnimationCompat>? runningAnimations) => insets!;

        public override void OnEnd(WindowInsetsAnimationCompat? animation)
        {
            if (animation is not null
                && (animation.TypeMask & WindowInsetsCompat.Type.Ime()) != 0
                && screen.keyboardShownNs is null
                && screen.KeyboardVisible())
            {
                screen.keyboardShownNs = SystemClock.ElapsedRealtimeNanos();
            }
        }
    }
}
