using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The task files in a clone's <c>tasks</c> folder.</summary>
internal sealed class TaskFolder(IFileSystem files)
{
    /// <summary>Every readable task, and the files it skipped. A file that can't be read is skipped, so one bad file doesn't hide the rest.</summary>
    public (List<TaskItem> Tasks, List<ISkippedRepositoryFile> Skipped) Read(string clone)
    {
        var tasks = new List<TaskItem>();
        var skipped = new List<ISkippedRepositoryFile>();
        foreach (var path in files.ListFiles(RepositoryLayout.TasksFolder(clone)))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            var relative = $"{RepositoryLayout.TasksFolderName}/{Path.GetFileName(path)}";
            try
            {
                var task = TaskFileFormat.Read(files.ReadFile(path));
                if (RepositoryFileRules.Instance.TaskIdInPath(relative) != task.Id)
                    throw new InvalidDataException("The file's name isn't its task's ID.");
                tasks.Add(task);
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                skipped.Add(SkippedRepositoryFile.Of(relative, e));
            }
        }

        return (tasks, skipped);
    }

    public void Write(string clone, TaskItem task) =>
        files.WriteFile(RepositoryLayout.TaskFile(clone, task.Id), TaskFileFormat.Write(task));

    public void Delete(string clone, string taskId) => files.DeleteFile(RepositoryLayout.TaskFile(clone, taskId));

    /// <summary>Whether a file is at the task's path, even one <see cref="Read"/> skipped.</summary>
    public bool FileExists(string clone, string taskId) => files.FileExists(RepositoryLayout.TaskFile(clone, taskId));
}
