using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The direct changes: they share the store's lock, files, and notices, and never touch the draft or the edit images.</summary>
internal sealed partial class TaskService : IDirectTaskChanges
{
    TaskItem IDirectTaskChanges.Add(string title, string description, string? tag)
    {
        var validTitle = TaskText.SingleLine(title, nameof(title));
        ArgumentNullException.ThrowIfNull(description);
        var validTag = StoredTag(tag, nameof(tag));
        var task = repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            var added = NewTask(TaskId.New(time), validTitle, description, validTag);
            folder.Write(root, added);
            set = set.WithTask(added);
            return added;
        });

        Saved("Added", task);
        return task;
    }

    TaskItem IDirectTaskChanges.Update(string taskId, DateTimeOffset baseUpdated, string? title, string? description, string? tag)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        var validTitle = title is null ? null : TaskText.SingleLine(title, nameof(title));
        var validDescription = description is null ? null : TaskText.LineFeeds(description);
        var validTag = StoredTag(tag, nameof(tag));
        return Change("Updated", taskId, task =>
        {
            if (task.Updated != baseUpdated)
            {
                logger.Debug("Refused an update of task {TaskId}: it changed at {Updated}, not at {BaseUpdated}", taskId, task.Updated, baseUpdated);
                throw new StaleTaskException(task);
            }

            var changed = task with
            {
                Title = validTitle ?? task.Title,
                Description = validDescription ?? task.Description,
                Tags = tag is null ? task.Tags : TagsOf(validTag),
            };
            if (changed.Title == task.Title && changed.Description == task.Description && changed.Tags.SequenceEqual(task.Tags))
            {
                logger.Debug("Update of task {TaskId} changes nothing, so nothing is written", taskId);
                return task;
            }

            return changed with { Updated = NextUpdated(task) };
        });
    }

    TaskItem IDirectTaskChanges.AddImage(string taskId, byte[] image, string altText)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(altText);
        var saved = repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            var task = Find(taskId);
            var encoded = shrinker.Shrink(image);
            var fileName = $"{TaskId.New(time)}.{encoded.Extension}";
            logger.Debug("Image for task {TaskId} is {Length} bytes as {FileName}", taskId, encoded.Bytes.Length, fileName);
            var markdown = ImageMarkdown.Of(altText, taskId, fileName);
            var changed = task with
            {
                Description = task.Description.Length == 0 ? markdown : $"{task.Description}\n{markdown}",
                Updated = NextUpdated(task),
            };

            var imagePath = Path.Combine(RepositoryLayout.ImagesFolder(root, taskId), fileName);
            files.WriteFile(imagePath, encoded.Bytes);
            try
            {
                folder.Write(root, changed);
            }
            catch (Exception failure)
            {
                DeleteAfterFailure(imagePath, failure);
                throw;
            }

            set = set.ReplacingTask(changed);
            return changed;
        });

        Saved("Added an image to", saved);
        return saved;
    }

    /// <summary>Deletes the image a failed save left behind. When that fails too, throws both failures together.</summary>
    private void DeleteAfterFailure(string imagePath, Exception failure)
    {
        try
        {
            files.DeleteFile(imagePath);
        }
        catch (Exception undoFailure)
        {
            throw new AggregateException("Adding the image failed, and so did deleting it.", failure, undoFailure);
        }
    }

    /// <summary>The tag as stored, trimmed, or null for a null or empty one. Throws <see cref="ArgumentException"/> for one that isn't a single non-empty line.</summary>
    private static string? StoredTag(string? tag, string paramName) =>
        string.IsNullOrEmpty(tag) ? null : TaskText.SingleLine(tag, paramName);
}
