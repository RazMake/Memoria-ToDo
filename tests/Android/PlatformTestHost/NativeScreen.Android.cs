using Android.App;
using Android.Content;
using Android.OS;
using Xunit;
using NativeActivity = Android.App.Activity;
using NativeApplication = Android.App.Application;
using NativeView = Android.Views.View;

namespace Memoria.ToDo.Android.DeviceHost;

[CollectionDefinition(NativeScreenCollection.Name, DisableParallelization = true)]
public sealed class NativeScreenCollection
{
    public const string Name = "Android screens";
}

internal sealed class NativeScreen<TActivity> : Java.Lang.Object, NativeApplication.IActivityLifecycleCallbacks, IAsyncDisposable
    where TActivity : NativeActivity
{
    private readonly TaskCompletionSource<TActivity> _resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly NativeApplication _application = Assert.IsAssignableFrom<NativeApplication>(NativeApplication.Context);
    private TActivity? _activity;

    public static async Task<NativeScreen<TActivity>> ShowAsync()
    {
        var screen = new NativeScreen<TActivity>();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                screen._application.RegisterActivityLifecycleCallbacks(screen);
                screen._application.StartActivity(new Intent(screen._application, typeof(TActivity)).AddFlags(ActivityFlags.NewTask));
            });
            await screen._resumed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return screen;
        }
        catch
        {
            await screen.DisposeAsync();
            throw;
        }
    }

    public Task CheckAsync(Action<TActivity> check) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        var activity = _activity ?? throw new InvalidOperationException("The native screen has not opened.");
        var content = Assert.IsAssignableFrom<NativeView>(activity.FindViewById(global::Android.Resource.Id.Content));
        content.Measure(NativeView.MeasureSpec.MakeMeasureSpec(800, global::Android.Views.MeasureSpecMode.Exactly),
            NativeView.MeasureSpec.MakeMeasureSpec(1280, global::Android.Views.MeasureSpecMode.Exactly));
        content.Layout(0, 0, content.MeasuredWidth, content.MeasuredHeight);
        check(activity);
    });

    public async ValueTask DisposeAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _activity?.Finish();
            _application.UnregisterActivityLifecycleCallbacks(this);
        });
        Dispose();
    }

    public void OnActivityCreated(NativeActivity activity, Bundle? savedInstanceState) { if (activity is TActivity screen) _activity = screen; }
    public void OnActivityResumed(NativeActivity activity) { if (activity is TActivity screen) _resumed.TrySetResult(screen); }
    public void OnActivityDestroyed(NativeActivity activity) { }
    public void OnActivityPaused(NativeActivity activity) { }
    public void OnActivitySaveInstanceState(NativeActivity activity, Bundle outState) { }
    public void OnActivityStarted(NativeActivity activity) { }
    public void OnActivityStopped(NativeActivity activity) { }
}
