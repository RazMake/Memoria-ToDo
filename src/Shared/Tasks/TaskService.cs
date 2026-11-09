using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The <see cref="ITasks"/> an app gets from <see cref="TaskStore"/>. Every call runs under one lock.</summary>
internal sealed class TaskService(IFileSystem files, IImageEncoder encoder, TimeProvider time, string deviceFolder) : ITasks
{
    private readonly Lock gate = new();
    private readonly TaskFolder folder = new(files);
    private readonly DraftStore drafts = new(files, deviceFolder);
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
            var now = time.GetUtcNow();
            now = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, now.Offset);
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

    public string SaveDraftImage(string taskId, byte[] image, string altText)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(altText);
        lock (gate)
        {
            var encoded = shrinker.Shrink(image);
            var fileName = $"{TaskId.New(time)}.{encoded.Extension}";
            drafts.SaveImage(taskId, fileName, encoded.Bytes);
            return ImageMarkdown.Of(altText, taskId, fileName);
        }
    }
}
