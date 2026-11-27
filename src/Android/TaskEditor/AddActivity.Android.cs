using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;

namespace Memoria.ToDo.Android.TaskEditor;

/// <summary>The Add screen: an empty screen with a top app bar titled "Add new task".</summary>
[Activity(Name = AddActivityName.Value, Exported = false)]
internal sealed class AddActivity : AppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.taskeditor_add);
    }
}
