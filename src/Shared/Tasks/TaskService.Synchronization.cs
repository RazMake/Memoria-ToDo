using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

internal sealed partial class TaskService
{
    private static readonly RepositoryFileRules fileRules = RepositoryFileRules.Instance;

    public ITaskSynchronization Synchronization => new SyncView(this);

    /// <summary>What Sync uses: the clone's rules, and settings and deletion that run under the repository's lock.</summary>
    private sealed class SyncView(TaskService owner) : ITaskSynchronization
    {
        public IRepositoryFileRules Files => fileRules;

        public void EnsureSettings() =>
            owner.repository.Locked(clone =>
            {
                var root = clone ?? throw new NoRepositoryFolderException();
                var current = owner.settings.Require();
                if (owner.settings.Present)
                    return;
                owner.settingsFolder.Write(root, current);
                owner.settings = new FileLoad<SharedSettings>(current, true, null);
            });

        public void DeleteExpiredTasks() =>
            owner.RemoveAndAnnounce((clone, committed) =>
            {
                var root = clone ?? throw new NoRepositoryFolderException();
                var cutoff = owner.Now().AddDays(-owner.settings.Require().DeletionDelayDays.Value);
                foreach (var task in owner.set.Items.Where(task => task.Resolved < cutoff).ToList())
                    owner.RemoveTask(root, task.Id, committed);
            });
    }
}
