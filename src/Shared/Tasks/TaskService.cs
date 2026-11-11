using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The <see cref="ITasks"/> an app gets from <see cref="TaskStore"/>. Every call runs under one lock.</summary>
internal sealed class TaskService(IFileSystem files, IImageEncoder encoder, TimeProvider time, string deviceFolder) : ITasks
{
    private readonly Lock gate = new();
    private readonly TaskFolder folder = new(files);
    private readonly DraftStore drafts = new(files, deviceFolder);
    private readonly EditImageStore editImages = new(files, deviceFolder);
    private readonly ImageShrinker shrinker = new(encoder);
    private string? clone;
    private TaskSet set = TaskSet.Empty;

    public event EventHandler? Changed;

    public IReadOnlyList<TaskItem> All
    {
        get
        {
            lock (gate)
                return set.Items;
        }
    }

    public void UseRepositoryFolder(string? repositoryFolder)
    {
        if (repositoryFolder is not null && string.IsNullOrWhiteSpace(repositoryFolder))
            throw new ArgumentException("Must be a folder path, or null.", nameof(repositoryFolder));
        lock (gate)
        {
            var loaded = repositoryFolder is null ? TaskSet.Empty : new TaskSet(folder.Read(repositoryFolder));
            clone = repositoryFolder;
            set = loaded;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public TaskItem Add(TaskDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        TaskItem task;
        lock (gate)
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            TaskId.ThrowIfInvalid(draft.TaskId, nameof(draft));
            if (set.Contains(draft.TaskId) || folder.FileExists(root, draft.TaskId))
                throw new ArgumentException("A task with this ID exists.", nameof(draft));
            var title = TaskText.SingleLine(draft.Title, nameof(draft));
            var tags = draft.Tags.Aggregate((IReadOnlyList<string>)[], (all, tag) => set.Tags.With(all, tag));
            var now = Now();
            var rank = RankKey.Before(set.Items.Count == 0 ? null : set.Items[0].Rank);
            task = new TaskItem(draft.TaskId, title, TaskText.LineFeeds(draft.Description), rank, tags, now, now, null);

            Publish(root, task);
            set = set.WithTask(task);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return task;
    }

    /// <summary>
    /// Writes the task's file, moves the draft's images into the clone, and deletes the draft. When a step fails, it
    /// undoes the steps before it, so the draft is whole, nothing of the task is left in the clone, and Add can be retried.
    /// </summary>
    private void Publish(string root, TaskItem task)
    {
        var undo = new Stack<Action>();
        try
        {
            folder.Write(root, task);
            undo.Push(() => folder.Delete(root, task.Id));
            foreach (var (from, to) in drafts.ImageMoves(task.Id, root))
            {
                files.MoveFile(from, to);
                undo.Push(() => files.MoveFile(to, from));
            }

            drafts.Discard();
        }
        catch (Exception failure)
        {
            var undoFailures = new List<Exception>();
            foreach (var step in undo)
            {
                try
                {
                    step();
                }
                catch (Exception e)
                {
                    undoFailures.Add(e);
                }
            }

            if (undoFailures.Count == 0)
                throw;
            throw new AggregateException("Adding failed, and so did undoing it.", [failure, .. undoFailures]);
        }
    }

    public IReadOnlyList<TagUse> TagsInUse()
    {
        lock (gate)
            return set.Tags.InUse;
    }

    public IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed)
    {
        lock (gate)
            return set.Tags.With(tags, typed);
    }

    public IReadOnlyList<string> SuggestTags(string typed, IReadOnlyList<string> tags)
    {
        lock (gate)
            return set.Tags.Suggest(typed, tags);
    }

    public IReadOnlyList<TaskItem> Search(string text)
    {
        lock (gate)
            return set.Search(text);
    }

    public IReadOnlyList<TaskItem> Search(string text, string tag)
    {
        var wanted = TaskText.SingleLine(tag, nameof(tag));
        lock (gate)
            return set.Search(text, wanted);
    }

    public TaskItem SaveEdit(TaskItem started, TaskDraft edited)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(edited);
        TaskItem saved;
        lock (gate)
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            if (edited.TaskId != started.Id)
                throw new ArgumentException("The edit is of another task.", nameof(edited));
            var title = TaskText.SingleLine(edited.Title, nameof(edited));
            var description = TaskText.LineFeeds(edited.Description);
            var editedTags = edited.Tags.Select(tag => TaskText.SingleLine(tag, nameof(edited))).ToList();
            var current = Find(started.Id);

            var changed = current with
            {
                Title = title == started.Title ? current.Title : title,
                Description = description == started.Description ? current.Description : description,
                Tags = ApplyTagEdit(current.Tags, started.Tags, editedTags),
            };
            if (changed.Title == current.Title && changed.Description == current.Description && changed.Tags.SequenceEqual(current.Tags))
                return current;

            saved = changed with { Updated = Now() };
            foreach (var (from, to) in editImages.ImageMoves(saved.Id, root))
                files.MoveFile(from, to);
            folder.Write(root, saved);
            set = set.ReplacingTask(saved);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    /// <summary>The current tags without those the edit removed, then with those it added, ignoring case.</summary>
    private IReadOnlyList<string> ApplyTagEdit(IReadOnlyList<string> current, IReadOnlyList<string> started, IReadOnlyList<string> edited)
    {
        var removed = started.Where(tag => !TagCatalog.Has(edited, tag)).ToList();
        var tags = (IReadOnlyList<string>)[.. current.Where(tag => !TagCatalog.Has(removed, tag))];
        return edited.Where(tag => !TagCatalog.Has(started, tag)).Aggregate(tags, (all, tag) => set.Tags.With(all, tag));
    }

    public TaskItem Move(string taskId, string? previousTaskId, string? nextTaskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        if (previousTaskId is not null)
            TaskId.ThrowIfInvalid(previousTaskId, nameof(previousTaskId));
        if (nextTaskId is not null)
            TaskId.ThrowIfInvalid(nextTaskId, nameof(nextTaskId));
        return Change(taskId, task =>
        {
            var previous = previousTaskId is null ? null : Find(previousTaskId);
            var next = nextTaskId is null ? null : Find(nextTaskId);
            if (previousTaskId == taskId || nextTaskId == taskId)
                throw new ArgumentException("A task can't be its own neighbor.");
            var place = set.IndexOf(taskId);
            var before = previous is null ? -1 : set.IndexOf(previous.Id);
            var after = next is null ? set.Items.Count : set.IndexOf(next.Id);
            if (before >= after)
                throw new ArgumentException("The previous task doesn't show before the next.");
            if (before < place && place < after)
                return task;
            if (previous is not null && next is not null && previous.Rank == next.Rank)
                throw new ArgumentException("The neighbors share a rank, so no rank of the task's own shows it between them.");
            return task with { Rank = RankKey.Between(previous?.Rank, next?.Rank), Updated = Now() };
        });
    }

    public TaskItem Resolve(string taskId) =>
        Change(taskId, task => Now() is var now && task.Resolved is null ? task with { Resolved = now, Updated = now } : task);

    public TaskItem Reopen(string taskId) =>
        Change(taskId, task => task.Resolved is null ? task : task with { Resolved = null, Updated = Now() });

    public void Delete(string taskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        var removed = false;
        try
        {
            lock (gate)
            {
                Find(taskId);
                folder.Delete(clone!, taskId);
                set = set.WithoutTask(taskId);
                removed = true;
                files.DeleteFolder(RepositoryLayout.ImagesFolder(clone!, taskId));
                editImages.Discard(taskId);
            }
        }
        finally
        {
            // Deleting the task file is the commit: once it succeeded the removal is published, even if the image cleanup threw.
            if (removed)
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Saves what <paramref name="change"/> makes of the task, unless it returns the task itself. Raises <see cref="Changed"/> after a save.</summary>
    private TaskItem Change(string taskId, Func<TaskItem, TaskItem> change)
    {
        TaskItem saved;
        lock (gate)
        {
            var task = Find(taskId);
            saved = change(task);
            if (ReferenceEquals(saved, task))
                return task;
            folder.Write(clone!, saved);
            set = set.ReplacingTask(saved);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    /// <summary>The task in <see cref="All"/>. Call under the lock.</summary>
    private TaskItem Find(string taskId)
    {
        var index = set.IndexOf(taskId);
        return index < 0 ? throw new TaskNotFoundException() : set.Items[index];
    }

    /// <summary>The current time, to the millisecond, as task files hold it.</summary>
    private DateTimeOffset Now()
    {
        var now = time.GetUtcNow();
        return new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, now.Offset);
    }

    public string LinkAddress(string taskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        return TaskId.LinkPrefix + taskId;
    }

    public string? TaskIdIn(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.StartsWith(TaskId.LinkPrefix, StringComparison.Ordinal) && TaskId.IsValid(address.AsSpan(TaskId.LinkPrefix.Length))
            ? address[TaskId.LinkPrefix.Length..]
            : null;
    }

    public TaskDraft StartDraft() => new(TaskId.New(time), "", "", []);

    public TaskDraft? LoadDraft()
    {
        lock (gate)
        {
            var draft = drafts.Load();
            if (draft is null || !set.Contains(draft.TaskId))
                return draft;
            drafts.Discard();
            return null;
        }
    }

    public void KeepDraft(TaskDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        lock (gate)
            drafts.Keep(draft);
    }

    public void DiscardDraft()
    {
        lock (gate)
            drafts.Discard();
    }

    public void DiscardEditImages(string taskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        lock (gate)
            editImages.Discard(taskId);
    }

    public string SaveImage(string taskId, byte[] image, string altText)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(altText);
        lock (gate)
        {
            var encoded = shrinker.Shrink(image);
            var fileName = $"{TaskId.New(time)}.{encoded.Extension}";
            if (set.Contains(taskId))
                editImages.Save(taskId, fileName, encoded.Bytes);
            else
                drafts.SaveImage(taskId, fileName, encoded.Bytes);
            return ImageMarkdown.Of(altText, taskId, fileName);
        }
    }
}
