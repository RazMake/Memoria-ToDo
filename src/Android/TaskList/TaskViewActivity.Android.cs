using Android.App;
using Android.OS;
using AndroidX.AppCompat.App;
using Google.Android.Material.AppBar;
using Google.Android.Material.FloatingActionButton;

namespace Memoria.ToDo.Android.TaskList;

/// <summary>Task View: a top app bar with a Settings item, an empty list area, and a floating Add button. It only passes taps on.</summary>
[Activity(Name = TaskViewActivityName.Value, Exported = false)]
internal sealed class TaskViewActivity : AppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var actions = TaskViewServices.Current.Get();
        SetContentView(Resource.Layout.tasklist_task_view);

        var toolbar = FindViewById<MaterialToolbar>(Resource.Id.tasklist_toolbar)!;
        toolbar.InflateMenu(Resource.Menu.tasklist_task_view);
        toolbar.MenuItemClick += (_, _) => actions.SettingsTapped();

        FindViewById<FloatingActionButton>(Resource.Id.tasklist_add_button)!.Click += (_, _) => actions.AddTapped();
    }
}
