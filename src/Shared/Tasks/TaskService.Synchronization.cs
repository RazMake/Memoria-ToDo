using System.Runtime.ExceptionServices;

namespace Memoria.ToDo.Shared.Tasks;

internal sealed partial class TaskService
{
    private static readonly RepositoryFileRules fileRules = RepositoryFileRules.Instance;

    public ITaskSynchronization Synchronization => new SyncView(this);

    /// <summary>What Sync uses: the clone's rules and diagnostics, and updates that run under Tasks' own lock.</summary>
    private sealed class SyncView(TaskService owner) : ITaskSynchronization
    {
        public string? RepositoryFolder
        {
            get
            {
                lock (owner.gate)
                    return owner.clone;
            }
        }

        public IRepositoryFileRules Files => fileRules;

        public IReadOnlyList<ISkippedRepositoryFile> SkippedFiles
        {
            get
            {
                lock (owner.gate)
                    return owner.skipped;
            }
        }

        public event EventHandler? LocalSaved
        {
            add
            {
                lock (owner.gate)
                    owner.localSaved += value;
            }
            remove
            {
                lock (owner.gate)
                    owner.localSaved -= value;
            }
        }

        public void EnsureSettings()
        {
            lock (owner.gate)
            {
                var root = owner.clone ?? throw new NoRepositoryFolderException();
                var current = owner.settings.Require();
                if (owner.settings.Present)
                    return;
                owner.settingsFolder.Write(root, current);
                owner.settings = new SettingsLoad(current, true, null);
            }
        }

        public void DeleteExpiredTasks()
        {
            var removed = false;
            try
            {
                lock (owner.gate)
                {
                    _ = owner.clone ?? throw new NoRepositoryFolderException();
                    var cutoff = owner.Now().AddDays(-owner.settings.Require().DeletionDelayDays.Value);
                    foreach (var task in owner.set.Items.Where(task => task.Resolved < cutoff).ToList())
                        owner.RemoveTask(task.Id, ref removed);
                }
            }
            finally
            {
                if (removed)
                    owner.Announce(Notice.Tasks | Notice.LocalSaved);
            }
        }

        public void UpdateRepository(string expectedFolder, Action update)
        {
            ArgumentNullException.ThrowIfNull(expectedFolder);
            ArgumentNullException.ThrowIfNull(update);
            Exception? updateFailure = null;
            Exception? reloadFailure = null;
            bool reloaded;
            lock (owner.gate)
            {
                if (owner.clone != expectedFolder)
                    throw new InvalidOperationException("The selected repository folder isn't the expected one.");
                try
                {
                    update();
                }
                catch (Exception e)
                {
                    updateFailure = e;
                }

                try
                {
                    owner.Apply(owner.Load(expectedFolder));
                }
                catch (Exception e)
                {
                    reloadFailure = e;
                }

                reloaded = reloadFailure is null;
            }

            if (reloaded)
                owner.Announce(Notice.Tasks | Notice.Settings);
            Rethrow(updateFailure, reloadFailure);
        }

        private static void Rethrow(Exception? updateFailure, Exception? reloadFailure)
        {
            if (updateFailure is not null && reloadFailure is not null)
                throw new AggregateException("The update failed, and so did reloading after it.", updateFailure, reloadFailure);
            var failure = updateFailure ?? reloadFailure;
            if (failure is not null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
