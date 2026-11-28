using Android.App;
using Android.OS;

namespace Memoria.ToDo.Android.Shell;

/// <summary>The screen Android opens when the app starts. It opens Task View, then closes, so Back from Task View leaves the app.</summary>
[Activity(Name = EntryActivityName.Value, MainLauncher = true, Exported = true, Theme = "@android:style/Theme.NoDisplay")]
internal sealed class EntryActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        EntryServices.Current.Get().Open();
        Finish();
    }
}
