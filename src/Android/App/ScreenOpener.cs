using Android.Content;
using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.App;

/// <summary>Opens the app's screens from the application context, as new tasks on top of the screen in front.</summary>
internal sealed class ScreenOpener(Context context) : IScreenOpener
{
    public void Open(string activityName)
    {
        var intent = new Intent()
            .SetClassName(context.PackageName!, activityName)
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        context.StartActivity(intent);
    }
}
