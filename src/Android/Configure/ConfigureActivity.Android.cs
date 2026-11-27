using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;

namespace Memoria.ToDo.Android.Configure;

/// <summary>The Configure screen: an empty screen with a top app bar titled "Configure".</summary>
[Activity(Name = ConfigureActivityName.Value, Exported = false)]
internal sealed class ConfigureActivity : AppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.configure_configure);
    }
}
