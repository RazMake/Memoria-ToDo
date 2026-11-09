using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The task files in a clone's <c>tasks</c> folder.</summary>
internal sealed class TaskFolder(IFileSystem files)
{
    /// <summary>Every readable task. A file that can't be read is skipped, so one bad file doesn't hide the rest.</summary>
    public List<TaskItem> Read(string clone)
    {
        var tasks = new List<TaskItem>();
        foreach (var path in files.ListFiles(RepositoryLayout.TasksFolder(clone)))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                tasks.Add(TaskFileFormat.Read(files.ReadFile(path)));
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return tasks;
    }

    public void Write(string clone, TaskItem task) =>
        files.WriteFile(RepositoryLayout.TaskFile(clone, task.Id), TaskFileFormat.Write(task));

    public void Delete(string clone, string taskId) => files.DeleteFile(RepositoryLayout.TaskFile(clone, taskId));

    /// <summary>Whether a file is at the task's path, even one <see cref="Read"/> skipped.</summary>
    public bool FileExists(string clone, string taskId) => files.FileExists(RepositoryLayout.TaskFile(clone, taskId));
}
