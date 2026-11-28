using Memoria.ToDo.Android.TaskEditor;

namespace Memoria.ToDo.Android.TaskList;

/// <summary>What Task View does when tapped. Its activity passes each tap on.</summary>
internal sealed class TaskViewActions(IAddScreen add, Action openSettings)
{
    private readonly IAddScreen _add = add;
    private readonly Action _openSettings = openSettings;

    /// <summary>Handles a tap on the floating button.</summary>
    public void AddTapped() => _add.Show();

    /// <summary>Handles a tap on the Settings item.</summary>
    public void SettingsTapped() => _openSettings();
}
