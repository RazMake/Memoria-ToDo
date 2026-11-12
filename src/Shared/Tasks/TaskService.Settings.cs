namespace Memoria.ToDo.Shared.Tasks;

internal sealed partial class TaskService
{
    public ISharedTaskSettings Settings => new SettingsView(this);

    /// <summary>The shared settings, kept in <c>settings.json</c> and changed under the same lock as the tasks.</summary>
    private sealed class SettingsView(TaskService owner) : ISharedTaskSettings
    {
        public int DeletionDelayDays => Read(settings => settings.DeletionDelayDays.Value);

        public string? AddNewTaskShortcut => Read(settings => settings.AddNewTaskShortcut.Value);

        public string? ManageTasksShortcut => Read(settings => settings.ManageTasksShortcut.Value);

        public event EventHandler? Changed
        {
            add
            {
                lock (owner.gate)
                    owner.settingsChanged += value;
            }
            remove
            {
                lock (owner.gate)
                    owner.settingsChanged -= value;
            }
        }

        public void SetDeletionDelayDays(int days)
        {
            if (days is < SharedSettings.MinDeletionDelayDays or > SharedSettings.MaxDeletionDelayDays)
                throw new ArgumentOutOfRangeException(nameof(days), days, "Must be from 1 to 365.");
            owner.ChangeSettings(settings => settings.DeletionDelayDays.Value == days
                ? settings
                : settings with { DeletionDelayDays = new Setting<int>(days, owner.Now()) });
        }

        public void SetAddNewTaskShortcut(string? shortcut) =>
            owner.ChangeSettings(settings => settings.AddNewTaskShortcut.Value == shortcut
                ? settings
                : settings with { AddNewTaskShortcut = new Setting<string?>(shortcut, owner.Now()) });

        public void SetManageTasksShortcut(string? shortcut) =>
            owner.ChangeSettings(settings => settings.ManageTasksShortcut.Value == shortcut
                ? settings
                : settings with { ManageTasksShortcut = new Setting<string?>(shortcut, owner.Now()) });

        private T Read<T>(Func<SharedSettings, T> read)
        {
            lock (owner.gate)
                return read(owner.settings.Require());
        }
    }

    /// <summary>Writes what <paramref name="edit"/> makes of the settings, unless it returns them unchanged.</summary>
    private void ChangeSettings(Func<SharedSettings, SharedSettings> edit)
    {
        lock (gate)
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            var changed = edit(settings.Require());
            if (changed == settings.Settings)
                return;
            settingsFolder.Write(root, changed);
            settings = new SettingsLoad(changed, true, null);
        }

        Announce(Notice.Settings | Notice.LocalSaved);
    }
}
