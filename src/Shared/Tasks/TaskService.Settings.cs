using Memoria.ToDo.Shared.Platform;

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

        public string? CloseWindowShortcut => Read(settings => settings.CloseWindowShortcut.Value);

        public string? SaveShortcut => Read(settings => settings.SaveShortcut.Value);

        public string? ConfigureShortcut => Read(settings => settings.ConfigureShortcut.Value);

        public string? ContactsShortcut => Read(settings => settings.ContactsShortcut.Value);

        public event EventHandler? Changed
        {
            add => owner.settingsChanged += value;
            remove => owner.settingsChanged -= value;
        }

        public void SetDeletionDelayDays(int days)
        {
            if (days is < SharedSettings.MinDeletionDelayDays or > SharedSettings.MaxDeletionDelayDays)
                throw new ArgumentOutOfRangeException(nameof(days), days, "Must be from 1 to 365.");
            owner.ChangeSettings("deletion delay in days", settings => settings.DeletionDelayDays.Value, settings => settings.DeletionDelayDays.Value == days
                ? settings
                : settings with { DeletionDelayDays = new Setting<int>(days, owner.Now()) });
        }

        public void SetAddNewTaskShortcut(string? shortcut) =>
            owner.ChangeSettings("add new task shortcut", settings => settings.AddNewTaskShortcut.Value, settings => settings.AddNewTaskShortcut.Value == shortcut
                ? settings
                : settings with { AddNewTaskShortcut = new Setting<string?>(shortcut, owner.Now()) });

        public void SetManageTasksShortcut(string? shortcut) =>
            owner.ChangeSettings("manage tasks shortcut", settings => settings.ManageTasksShortcut.Value, settings => settings.ManageTasksShortcut.Value == shortcut
                ? settings
                : settings with { ManageTasksShortcut = new Setting<string?>(shortcut, owner.Now()) });

        public void SetCloseWindowShortcut(string? shortcut) =>
            owner.ChangeSettings("close window shortcut", settings => settings.CloseWindowShortcut.Value, settings => settings.CloseWindowShortcut.Value == shortcut
                ? settings
                : settings with { CloseWindowShortcut = new Setting<string?>(shortcut, owner.Now()) });

        public void SetSaveShortcut(string? shortcut) =>
            owner.ChangeSettings("save shortcut", settings => settings.SaveShortcut.Value, settings => settings.SaveShortcut.Value == shortcut
                ? settings
                : settings with { SaveShortcut = new Setting<string?>(shortcut, owner.Now()) });

        public void SetConfigureShortcut(string? shortcut) =>
            owner.ChangeSettings("configure shortcut", settings => settings.ConfigureShortcut.Value, settings => settings.ConfigureShortcut.Value == shortcut
                ? settings
                : settings with { ConfigureShortcut = new Setting<string?>(shortcut, owner.Now()) });

        public void SetContactsShortcut(string? shortcut) =>
            owner.ChangeSettings("contacts shortcut", settings => settings.ContactsShortcut.Value, settings => settings.ContactsShortcut.Value == shortcut
                ? settings
                : settings with { ContactsShortcut = new Setting<string?>(shortcut, owner.Now()) });

        private T Read<T>(Func<SharedSettings, T> read) => owner.repository.Locked(_ => read(owner.settings.Require()));
    }

    /// <summary>
    /// Writes what <paramref name="edit"/> makes of the settings, unless it returns them unchanged, and logs the
    /// setting called <paramref name="name"/> with the value <paramref name="valueOf"/> reads before and after.
    /// </summary>
    private void ChangeSettings(string name, Func<SharedSettings, object?> valueOf, Func<SharedSettings, SharedSettings> edit)
    {
        object? before = null, after = null;
        var written = false;
        repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            var current = settings.Require();
            var changed = edit(current);
            if (changed == settings.Value)
                return;
            settingsFolder.Write(root, changed);
            settings = new FileLoad<SharedSettings>(changed, true, null);
            before = valueOf(current);
            after = valueOf(changed);
            written = true;
        });

        if (written)
        {
            logger.Information("Changed the {Setting} from {OldValue} to {NewValue}", name, before, after);
            AnnounceSaved(Notice.Settings | Notice.LocalSaved, null);
        }
    }
}
