using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// The <see cref="ITaskStore"/> an app gets from <see cref="TaskStore"/>, and a part of the synced repository.
/// Every call runs under the repository's lock.
/// </summary>
internal sealed partial class TaskService(IFileSystem files, IImageEncoder encoder, TimeProvider time, string deviceFolder, IRepositoryAccess repository, ILogger logger) : ITaskStore, IRepositoryPart
{
    private readonly IRepositoryAccess repository = repository;
    private readonly ILogger logger = logger;
    private readonly TaskFolder folder = new(files);
    private readonly SettingsFolder settingsFolder = new(files);
    private readonly TagColorsFolder tagColorsFolder = new(files);
    private readonly DraftStore drafts = new(files, deviceFolder, logger);
    private readonly EditImageStore editImages = new(files, deviceFolder);
    private readonly ImageShrinker shrinker = new(encoder);
    private TaskSet set = TaskSet.Empty;
    private FileLoad<SharedSettings> settings = new(SharedSettings.Defaults, false, null);
    private FileLoad<TagColorSet> tagColors = new(TagColorSet.Empty, false, null);
    private IReadOnlyList<ISkippedRepositoryFile> skipped = [];

    public event EventHandler? Changed;

    public IDirectTaskChanges Direct => this;

    public void RequireReadableSettings() =>
        repository.Locked(_ =>
        {
            // A skipped settings.json is named before a skipped tags.json.
            var unreadable = skipped.FirstOrDefault(file => file.RelativePath == RepositoryLayout.SettingsFileName)
                ?? skipped.FirstOrDefault(file => file.RelativePath == RepositoryLayout.TagColorsFileName);
            if (unreadable is not null)
                throw new SkippedFileException(unreadable);
        });

    public string EditImagesFolder => RepositoryLayout.EditImagesRoot(deviceFolder);

    public IReadOnlyList<TaskItem> All => repository.Locked(_ => set.Items);

    IReadOnlyList<ISkippedRepositoryFile> IRepositoryPart.SkippedFiles => skipped;

    Action IRepositoryPart.Load(string? root)
    {
        var loaded = root is null ? Loaded.Empty : Read(root);
        return () => Apply(loaded);
    }

    void IRepositoryPart.Loaded() => Announce(Notice.Tasks | Notice.Settings | Notice.TagColors | Notice.TagOrder);

    /// <summary>Everything a reload reads, built whole so a failure leaves the current state untouched.</summary>
    private sealed record Loaded(TaskSet Set, FileLoad<SharedSettings> Settings, FileLoad<TagColorSet> TagColors, IReadOnlyList<ISkippedRepositoryFile> Skipped)
    {
        public static readonly Loaded Empty = new(TaskSet.Empty, new(SharedSettings.Defaults, false, null), new(TagColorSet.Empty, false, null), []);
    }

    private Loaded Read(string root)
    {
        var (tasks, skippedTasks) = folder.Read(root);
        var loadedSettings = settingsFolder.Read(root);
        if (loadedSettings.Failure is not null)
            skippedTasks.Add(SkippedFile.Of(RepositoryLayout.SettingsFileName, loadedSettings.Failure));
        var loadedColors = tagColorsFolder.Read(root);
        if (loadedColors.Failure is not null)
            skippedTasks.Add(SkippedFile.Of(RepositoryLayout.TagColorsFileName, loadedColors.Failure));
        return new Loaded(new TaskSet(tasks), loadedSettings, loadedColors, skippedTasks);
    }

    private void Apply(Loaded loaded)
    {
        set = loaded.Set;
        settings = loaded.Settings;
        tagColors = loaded.TagColors;
        skipped = loaded.Skipped;
    }

    [Flags]
    private enum Notice
    {
        None = 0,
        Tasks = 1,
        Settings = 2,
        LocalSaved = 4,
        TagColors = 8,
        TagOrder = 16,
    }

    /// <summary>Raises the events for what changed. Call it after leaving the lock, so handlers never run inside it.</summary>
    private void Announce(Notice notice)
    {
        var failures = new FailureList();
        Announce(notice, failures);
        failures.ThrowIfAny();
    }

    /// <summary>Attempts every requested notice in order, each even when an earlier one threw, and keeps the failures in <paramref name="failures"/>.</summary>
    private void Announce(Notice notice, FailureList failures)
    {
        if (notice.HasFlag(Notice.Tasks))
            failures.Attempt(() => Changed?.Invoke(this, EventArgs.Empty));
        if (notice.HasFlag(Notice.Settings))
            failures.Attempt(() => settingsChanged?.Invoke(this, EventArgs.Empty));
        if (notice.HasFlag(Notice.TagColors))
            failures.Attempt(() => tagColorsChanged?.Invoke(this, EventArgs.Empty));
        if (notice.HasFlag(Notice.TagOrder))
            failures.Attempt(() => tagOrderChanged?.Invoke(this, EventArgs.Empty));
        if (notice.HasFlag(Notice.LocalSaved))
            failures.Attempt(repository.ReportLocalSave);
    }

    /// <summary>
    /// Runs the removal under the lock. Once <paramref name="remove"/> calls its committed action, the removal is published,
    /// so the notices run even when it then throws; its failure comes first, then the notices'.
    /// </summary>
    private void RemoveAndAnnounce(Action<string?, Action> remove)
    {
        var removed = false;
        var failures = new FailureList();
        failures.Attempt(() => repository.LockedChange(clone => remove(clone, () => removed = true)));
        if (removed)
            Announce(Notice.Tasks | Notice.LocalSaved, failures);
        failures.ThrowIfAny();
    }

    private event EventHandler? settingsChanged;
    private event EventHandler? tagColorsChanged;
    private event EventHandler? tagOrderChanged;

    public TaskItem Add(TaskDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var task = repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            TaskId.ThrowIfInvalid(draft.TaskId, nameof(draft));
            if (set.Contains(draft.TaskId) || folder.FileExists(root, draft.TaskId))
                throw new ArgumentException("A task with this ID exists.", nameof(draft));
            var title = TaskText.SingleLine(draft.Title, nameof(draft));
            var tag = OneTag(draft.Tags, nameof(draft));
            var added = NewTask(draft.TaskId, title, draft.Description, tag);

            Publish(root, added);
            set = set.WithTask(added);
            return added;
        });

        logger.Information("Added task {TaskId} {Title}", task.Id, task.Title);
        Announce(Notice.Tasks | Notice.LocalSaved);
        return task;
    }

    /// <summary>An open task at the top, created now, from a validated title and tag. Call under the lock.</summary>
    private TaskItem NewTask(string id, string title, string description, string? tag)
    {
        var now = Now();
        var rank = RankKey.Before(set.Items.Count == 0 ? null : set.Items[0].Rank);
        return new TaskItem(id, title, TaskText.LineFeeds(description), rank, TagsOf(tag), now, now, null);
    }

    /// <summary>The tags of a task with this validated tag, spelled as the tag is already in use; none for null. Call under the lock.</summary>
    private IReadOnlyList<string> TagsOf(string? tag) => tag is null ? [] : set.Tags.With([], tag);

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

    public IReadOnlyList<TagUse> TagsInUse() => repository.Locked(_ => set.Tags.InUse);

    public IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed) =>
        repository.Locked(_ => set.Tags.With(tags, typed));

    public IReadOnlyList<string> SuggestTags(string typed, IReadOnlyList<string> tags) =>
        repository.Locked(_ => set.Tags.Suggest(typed, tags));

    public IReadOnlyList<TaskItem> Search(string text) => repository.Locked(_ => set.Search(text));

    public IReadOnlyList<TaskItem> Search(string text, string tag)
    {
        var wanted = TaskText.SingleLine(tag, nameof(tag));
        return repository.Locked(_ => set.Search(text, wanted));
    }

    public TaskItem SaveEdit(TaskItem started, TaskDraft edited)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(edited);
        var written = false;
        var result = repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            if (edited.TaskId != started.Id)
                throw new ArgumentException("The edit is of another task.", nameof(edited));
            var title = TaskText.SingleLine(edited.Title, nameof(edited));
            var description = TaskText.LineFeeds(edited.Description);
            var editedTag = OneTag(edited.Tags, nameof(edited));
            var current = Find(started.Id);

            var changed = current with
            {
                Title = title == started.Title ? current.Title : title,
                Description = description == started.Description ? current.Description : description,
                Tags = string.Equals(editedTag, started.Tags.FirstOrDefault(), StringComparison.OrdinalIgnoreCase)
                    ? current.Tags
                    : TagsOf(editedTag),
            };
            if (changed.Title == current.Title && changed.Description == current.Description && changed.Tags.SequenceEqual(current.Tags))
                return current;

            var saved = changed with { Updated = NextUpdated(current) };
            foreach (var (from, to) in editImages.ImageMoves(saved.Id, root))
                files.MoveFile(from, to);
            folder.Write(root, saved);
            set = set.ReplacingTask(saved);
            written = true;
            return saved;
        });

        if (written)
        {
            logger.Information("Saved an edit of task {TaskId} {Title}", result.Id, result.Title);
            Announce(Notice.Tasks | Notice.LocalSaved);
        }

        return result;
    }

    /// <summary>The one tag of the draft, trimmed, or null for none. Throws <see cref="ArgumentException"/> for two or more tags or an invalid one.</summary>
    private static string? OneTag(IReadOnlyList<string> tags, string paramName) =>
        tags.Count > 1 ? throw new ArgumentException("A task has at most one tag.", paramName)
            : tags.Count == 0 ? null : TaskText.SingleLine(tags[0], paramName);

    public TaskItem Move(string taskId, string? previousTaskId, string? nextTaskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        if (previousTaskId is not null)
            TaskId.ThrowIfInvalid(previousTaskId, nameof(previousTaskId));
        if (nextTaskId is not null)
            TaskId.ThrowIfInvalid(nextTaskId, nameof(nextTaskId));
        return Change("Moved", taskId, task =>
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
            var givenBoth = previous is not null && next is not null;
            // A side left open is bounded by the next rank there. With both given, the task already shows there anywhere between them.
            if (previous is null && next is not null)
                before = set.IndexBeyond(next, -1, taskId);
            else if (next is null && previous is not null)
                after = set.IndexBeyond(previous, 1, taskId);
            var items = set.Items;
            if (before < place && place < after)
            {
                logger.Debug("Move of task {TaskId} between {PreviousTaskId} and {NextTaskId} changed nothing: it already shows there", taskId, previousTaskId, nextTaskId);
                return task;
            }
            if (givenBoth && previous!.Rank == next!.Rank)
                throw new ArgumentException("The neighbors share a rank, so no rank of the task's own shows it between them.");
            // Right after the previous neighbor, the new rank stops at the first different rank there, so no other task holds it.
            if (previous is not null)
                after = set.IndexBeyond(previous, 1, taskId);
            var lower = before < 0 ? null : items[before].Rank;
            var upper = after >= items.Count ? null : items[after].Rank;
            var rank = RankKey.Between(lower, upper);
            logger.Debug("Move of task {TaskId} between {PreviousTaskId} and {NextTaskId} chose a rank between {LowerRank} and {UpperRank}: {Rank}", taskId, previousTaskId, nextTaskId, lower, upper, rank);
            return task with { Rank = rank, Updated = NextUpdated(task) };
        });
    }

    public TaskItem Resolve(string taskId) =>
        Change("Resolved", taskId, task => task.Resolved is null ? task with { Resolved = Now(), Updated = NextUpdated(task) } : task);

    public TaskItem Reopen(string taskId) =>
        Change("Reopened", taskId, task => task.Resolved is null ? task : task with { Resolved = null, Updated = NextUpdated(task) });

    public void Delete(string taskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        // Deleting the task file is the commit: once it succeeded the removal is published, even if the image cleanup threw.
        RemoveAndAnnounce((clone, committed) =>
            RemoveTask("Deleted", clone ?? throw new NoRepositoryFolderException(), Find(taskId), committed));
    }

    /// <summary>Deletes the task's file, then its images. Call under the lock; <paramref name="committed"/> runs once the file is gone. Logs the removal as <paramref name="how"/>.</summary>
    private void RemoveTask(string how, string root, TaskItem task, Action committed)
    {
        folder.Delete(root, task.Id);
        set = set.WithoutTask(task.Id);
        committed();
        logger.Information("{How} task {TaskId} {Title}", how, task.Id, task.Title);
        files.DeleteFolder(RepositoryLayout.ImagesFolder(root, task.Id));
        editImages.Discard(task.Id);
    }

    /// <summary>
    /// Saves what <paramref name="change"/> makes of the task, unless it returns the task itself, and logs it as <paramref name="how"/>.
    /// Raises <see cref="Changed"/> after a save.
    /// </summary>
    private TaskItem Change(string how, string taskId, Func<TaskItem, TaskItem> change)
    {
        var written = false;
        var result = repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            var task = Find(taskId);
            var saved = change(task);
            if (ReferenceEquals(saved, task))
                return task;
            folder.Write(root, saved);
            set = set.ReplacingTask(saved);
            written = true;
            return saved;
        });

        if (written)
            Saved(how, result);
        return result;
    }

    /// <summary>Logs the saved task as <paramref name="how"/> and raises the notices of a saved change. Call after leaving the lock.</summary>
    private void Saved(string how, TaskItem task)
    {
        logger.Information("{How} task {TaskId} {Title}", how, task.Id, task.Title);
        Announce(Notice.Tasks | Notice.LocalSaved);
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

    /// <summary>The Updated for a change to <paramref name="task"/>: now, or one millisecond after its current Updated when the clock hasn't passed that, so every change moves Updated strictly forward.</summary>
    private DateTimeOffset NextUpdated(TaskItem task)
    {
        var now = Now();
        var after = task.Updated + TimeSpan.FromMilliseconds(1);
        return now > task.Updated ? now : after;
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

    public TaskDraft? LoadDraft() =>
        repository.Locked<TaskDraft?>(_ =>
        {
            var draft = drafts.Load();
            if (draft is not null && set.Contains(draft.TaskId))
            {
                drafts.Discard();
                return null;
            }

            return draft is null ? null : draft with { Tags = [.. draft.Tags.Take(1)] };
        });

    public void KeepDraft(TaskDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        repository.Locked(_ =>
        {
            if (drafts.Keep(draft))
                logger.Information("Kept the draft of task {TaskId} {Title}", draft.TaskId, draft.Title);
            else
                logger.Information("Discarded the blank draft of task {TaskId}", draft.TaskId);
        });
    }

    public void DiscardDraft()
    {
        repository.Locked(_ => drafts.Discard());
        logger.Information("Discarded the kept draft");
    }

    public void DiscardEditImages(string taskId)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        repository.Locked(_ => editImages.Discard(taskId));
    }

    public string SaveImage(string taskId, byte[] image, string altText)
    {
        TaskId.ThrowIfInvalid(taskId, nameof(taskId));
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(altText);
        return repository.Locked(_ =>
        {
            var encoded = shrinker.Shrink(image);
            var fileName = $"{TaskId.New(time)}.{encoded.Extension}";
            if (set.Contains(taskId))
                editImages.Save(taskId, fileName, encoded.Bytes);
            else
                drafts.SaveImage(taskId, fileName, encoded.Bytes);
            logger.Information("Saved image {FileName} for task {TaskId}", fileName, taskId);
            return ImageMarkdown.Of(altText, taskId, fileName);
        });
    }
}
