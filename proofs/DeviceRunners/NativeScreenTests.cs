using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Google.Android.Material.AppBar;
using Memoria.ToDo.Android.Configure;
using Xunit;
using NativeActivity = Android.App.Activity;
using NativeApplication = Android.App.Application;
using NativeView = Android.Views.View;

namespace Memoria.DeviceRunnersProof;

public sealed class NativeScreenTests
{
    [Fact]
    [Trait("Category", "UX")]
    public async Task hosts_the_native_configure_screen_and_lays_out_its_toolbar()
    {
        var application = Assert.IsAssignableFrom<NativeApplication>(NativeApplication.Context);
        using var lifecycle = new ConfigureLifecycle();
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                application.RegisterActivityLifecycleCallbacks(lifecycle);
                application.StartActivity(new Intent(application, typeof(ConfigureActivity))
                    .AddFlags(ActivityFlags.NewTask));
            });

            var activity = await lifecycle.Resumed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var content = Assert.IsAssignableFrom<NativeView>(
                    activity.FindViewById(Android.Resource.Id.Content));
                content.Measure(
                    NativeView.MeasureSpec.MakeMeasureSpec(800, MeasureSpecMode.Exactly),
                    NativeView.MeasureSpec.MakeMeasureSpec(1280, MeasureSpecMode.Exactly));
                content.Layout(0, 0, content.MeasuredWidth, content.MeasuredHeight);
                var toolbar = Assert.IsType<MaterialToolbar>(activity.FindViewById(
                    Memoria.ToDo.Android.Configure.Resource.Id.configure_toolbar));
                Assert.Equal("Configure", toolbar.Title);
                Assert.True(toolbar.Width > 0);
                Assert.True(toolbar.Height > 0);
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                lifecycle.Activity?.Finish();
                application.UnregisterActivityLifecycleCallbacks(lifecycle);
            });
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void runs_xunit_v3_inside_the_android_process()
    {
        Assert.True(OperatingSystem.IsAndroid());
        Assert.NotNull(TestContext.Current.Test);
    }

    [Fact]
    [Trait("Category", "FailureProof")]
    public void reports_an_intentional_assertion_failure()
    {
        Assert.Fail("DeviceRunners intentional assertion failure");
    }

    private sealed class ConfigureLifecycle : Java.Lang.Object,
        NativeApplication.IActivityLifecycleCallbacks
    {
        public TaskCompletionSource<NativeActivity> Resumed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public NativeActivity? Activity { get; private set; }

        public void OnActivityCreated(NativeActivity activity, Bundle? savedInstanceState)
        {
            if (activity is ConfigureActivity)
                Activity = activity;
        }

        public void OnActivityResumed(NativeActivity activity)
        {
            if (activity is ConfigureActivity)
                Resumed.TrySetResult(activity);
        }

        public void OnActivityDestroyed(NativeActivity activity) { }
        public void OnActivityPaused(NativeActivity activity) { }
        public void OnActivitySaveInstanceState(NativeActivity activity, Bundle outState) { }
        public void OnActivityStarted(NativeActivity activity) { }
        public void OnActivityStopped(NativeActivity activity) { }
    }
}
