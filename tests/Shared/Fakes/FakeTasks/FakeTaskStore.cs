using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// The shared fake of <see cref="ITaskStore"/> for callers that only read tasks and change them through
/// <see cref="ITasks"/> and <see cref="IDirectTaskChanges"/>, such as the local API. It keeps tasks in memory, in the order
/// <see cref="Tasks"/> lists them (the rank order), and records every call. A test can make every call fail with
/// <see cref="Failure"/>, run an action inside each call with <see cref="OnCall"/> to hold it, or set <see cref="NoRepository"/>.
/// What a caller with no editor must never use (the draft, an open edit's images, <see cref="ITasks.Add"/>, <see cref="ITasks.SaveEdit"/>, and
/// <see cref="ITasks.DiscardEditImages"/>, which <see cref="ITasks.Delete"/> already does) is
/// recorded in <see cref="ForbiddenCalls"/> and fails. Settings, tag colors, and tag order are the shared in-memory fakes (<see cref="SettingsControl"/>, <see cref="TagColorsControl"/>,
/// <see cref="TagOrderControl"/>); synchronization isn't faked.
/// </summary>
public sealed class FakeTaskStore : ITaskStore
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Lock gate = new();
    private readonly List<string> calls = [];
    private readonly List<string> forbidden = [];
    private readonly List<TaskItem> tasks = [];
    private int stamps;

    public FakeTaskStore() => DirectChanges = new FakeDirectTaskChanges(this);

    /// <summary>Makes a task for seeding: open unless <paramref name="resolved"/> is set, created and updated at the given times. <paramref name="tags"/> wins over <paramref name="tag"/> when both are given.</summary>
    public static TaskItem Item(
        string id, string title = "Title", string description = "", string? tag = null,
        DateTimeOffset? created = null, DateTimeOffset? updated = null, DateTimeOffset? resolved = null,
        IReadOnlyList<string>? tags = null) =>
        new(id, title, description, "rank-" + id, tags ?? (tag is null ? [] : [tag]), created ?? Stamp, updated ?? created ?? Stamp, resolved);

    /// <summary>The error a stale update throws, carrying the task as it is now.</summary>
    public static StaleTaskException StaleOf(TaskItem current) => new(current);

    /// <summary>When true, the store has no folder: reads are empty and changes throw <see cref="NoRepositoryFolderException"/>.</summary>
    public bool NoRepository { get; set; }

    /// <summary>When set, every call throws it, after <see cref="OnCall"/> ran.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Runs first inside every call, on the calling thread, so a test can hold the call until it signals. Null when not set.</summary>
    public Action? OnCall { get; set; }

    /// <summary>The tasks, open and resolved, in rank order. Change it only while no call runs.</summary>
    public List<TaskItem> Tasks
    {
        get
        {
            lock (gate)
                return tasks;
        }
    }

    /// <summary>Every call a caller may make, such as "Move a b c", "Resolve a", "Search milk", "Direct.Add", in order.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (gate)
                return [.. calls];
        }
    }

    /// <summary>Every call to the draft, the open edit's images, <see cref="ITasks.Add"/>, or <see cref="ITasks.SaveEdit"/>. A caller with no editor must leave this empty.</summary>
    public IReadOnlyList<string> ForbiddenCalls
    {
        get
        {
            lock (gate)
                return [.. forbidden];
        }
    }

    /// <summary>The direct changes this store gives, with what each call got.</summary>
    public FakeDirectTaskChanges DirectChanges { get; }

    /// <inheritdoc />
    public IDirectTaskChanges Direct => DirectChanges;

    /// <inheritdoc />
    public IReadOnlyList<TaskItem> All
    {
        get
        {
            lock (gate)
                return NoRepository ? [] : [.. tasks];
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> on the calling thread.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public IReadOnlyList<TagUse> TagsInUse()
    {
        Enter("TagsInUse", changes: false);
        return [.. All.SelectMany(task => task.Tags).GroupBy(tag => tag, StringComparer.Ordinal)
            .Select(group => new TagUse(group.Key, group.Count())).OrderByDescending(use => use.Count).ThenBy(use => use.Tag, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public IReadOnlyList<TaskItem> Search(string text)
    {
        Enter("Search " + text, changes: false);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return [.. All.Where(task => words.All(word =>
            task.Title.Contains(word, StringComparison.OrdinalIgnoreCase)
            || task.Description.Contains(word, StringComparison.OrdinalIgnoreCase)
            || task.Tags.Any(tag => tag.Contains(word, StringComparison.OrdinalIgnoreCase))))];
    }

    /// <inheritdoc />
    public IReadOnlyList<TaskItem> Search(string text, IReadOnlyCollection<string> tags)
    {
        Enter($"Search {text}|{string.Join(",", tags)}", changes: false);
        var wanted = Normalized(tags);
        if (tags.Count == 0)
            throw new ArgumentException("Name at least one tag.", nameof(tags));
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return [.. All.Where(task => wanted.All(task.Tags.Contains) && words.All(word =>
            task.Title.Contains(word, StringComparison.OrdinalIgnoreCase)
            || task.Description.Contains(word, StringComparison.OrdinalIgnoreCase)
            || task.Tags.Any(t => t.Contains(word, StringComparison.OrdinalIgnoreCase))))];
    }

    /// <summary>The tags normalized with repeats dropped, first place kept. Throws <see cref="ArgumentException"/> for a tag that's empty once normalized.</summary>
    internal static List<string> Normalized(IEnumerable<string> tags) =>
        [.. tags.Select(tag => TagName.Normalize(tag) is { Length: > 0 } normalized ? normalized : throw new ArgumentException("A tag can't be empty.", nameof(tags)))
            .Distinct(StringComparer.Ordinal)];

    /// <inheritdoc />
    public string LinkAddress(string taskId) => "task:" + taskId;

    /// <inheritdoc />
    public string? TaskIdIn(string address) => address.StartsWith("task:", StringComparison.Ordinal) ? address[5..] : null;

    /// <inheritdoc />
    public TaskItem Move(string taskId, string? previousTaskId, string? nextTaskId)
    {
        Enter($"Move {taskId} {previousTaskId ?? "-"} {nextTaskId ?? "-"}", changes: true);
        return Replace(Find(taskId), task => task);
    }

    /// <inheritdoc />
    public TaskItem Resolve(string taskId)
    {
        Enter("Resolve " + taskId, changes: true);
        return Replace(Find(taskId), task => task with { Resolved = Next() });
    }

    /// <inheritdoc />
    public TaskItem Reopen(string taskId)
    {
        Enter("Reopen " + taskId, changes: true);
        return Replace(Find(taskId), task => task with { Resolved = null });
    }

    /// <inheritdoc />
    public void Delete(string taskId)
    {
        Enter("Delete " + taskId, changes: true);
        var task = Find(taskId);
        lock (gate)
            tasks.Remove(task);
    }

    /// <summary>Records a call of the direct changes, runs the hold and the failure, and throws when there's no folder.</summary>
    internal void EnterDirect(string call) => Enter(call, changes: true);

    internal TaskItem Find(string taskId)
    {
        lock (gate)
            return tasks.FirstOrDefault(task => task.Id == taskId && !NoRepository) ?? throw new TaskNotFoundException();
    }

    internal TaskItem Replace(TaskItem task, Func<TaskItem, TaskItem> change)
    {
        var changed = change(task) with { Updated = Next() };
        lock (gate)
            tasks[tasks.IndexOf(task)] = changed;
        return changed;
    }

    internal TaskItem Insert(TaskItem task)
    {
        lock (gate)
            tasks.Insert(0, task);
        return task;
    }

    internal DateTimeOffset Next() => Stamp.AddMinutes(Interlocked.Increment(ref stamps));

    private void Enter(string call, bool changes)
    {
        lock (gate)
            calls.Add(call);
        OnCall?.Invoke();
        if (Failure is not null)
            throw Failure;
        if (changes && NoRepository)
            throw new NoRepositoryFolderException();
    }

    private T Forbid<T>(string call)
    {
        lock (gate)
            forbidden.Add(call);
        throw new NotSupportedException(call + " is for a caller with an editor.");
    }

    /// <inheritdoc />
    public TaskItem Add(TaskDraft draft) => Forbid<TaskItem>("Add");

    /// <inheritdoc />
    public IReadOnlyList<string> WithTag(IReadOnlyList<string> tags, string typed) => Forbid<IReadOnlyList<string>>("WithTag");

    /// <inheritdoc />
    public IReadOnlyList<string> SuggestTags(string typed, IReadOnlyList<string> tags) => Forbid<IReadOnlyList<string>>("SuggestTags");

    /// <inheritdoc />
    public TaskDraft StartDraft() => Forbid<TaskDraft>("StartDraft");

    /// <inheritdoc />
    public TaskDraft? LoadDraft() => Forbid<TaskDraft?>("LoadDraft");

    /// <inheritdoc />
    public void KeepDraft(TaskDraft draft) => Forbid<object>("KeepDraft");

    /// <inheritdoc />
    public void DiscardDraft() => Forbid<object>("DiscardDraft");

    /// <inheritdoc />
    public string SaveImage(string taskId, byte[] image, string altText) => Forbid<string>("SaveImage");

    /// <inheritdoc />
    public void DiscardEditImages(string taskId) => Forbid<object>("DiscardEditImages");

    /// <inheritdoc />
    public TaskItem SaveEdit(TaskItem started, TaskDraft edited) => Forbid<TaskItem>("SaveEdit");

    /// <summary>The in-memory shared settings behind <see cref="Settings"/>. Set its failures to model a missing repository or an unreadable file.</summary>
    public FakeSharedTaskSettings SettingsControl { get; } = new();

    /// <summary>The in-memory tag colors behind <see cref="TagColors"/>.</summary>
    public FakeTagColors TagColorsControl { get; } = new();

    /// <summary>The in-memory tag order behind <see cref="TagOrder"/>. Its places are those of the tags in use that <see cref="ITagOrder.Sort"/> last got.</summary>
    public FakeTagOrder TagOrderControl { get; } = new();

    /// <inheritdoc />
    public ISharedTaskSettings Settings => SettingsControl;

    /// <inheritdoc />
    public ITagColors TagColors => TagColorsControl;

    /// <inheritdoc />
    public ITagOrder TagOrder => TagOrderControl;

    /// <inheritdoc />
    public string EditImagesFolder => throw new NotSupportedException();

    /// <inheritdoc />
    public ITaskSynchronization Synchronization => throw new NotSupportedException();

    /// <summary>When set, <see cref="RequireReadableSettings"/> throws <see cref="SkippedFileException"/> naming it, as the store does for a skipped settings.json or tags.json.</summary>
    public ISkippedRepositoryFile? SkippedSettingsFile { get; set; }

    /// <inheritdoc />
    public void RequireReadableSettings()
    {
        lock (gate)
            calls.Add("RequireReadableSettings");
        if (SkippedSettingsFile is { } file)
            throw new SkippedFileException(file);
    }
}

/// <summary>What a caller asked <see cref="FakeTaskStore.Direct"/> to do, and the fake's answers, which follow the interface's documentation.</summary>
public sealed class FakeDirectTaskChanges(FakeTaskStore store) : IDirectTaskChanges
{
    private readonly Lock gate = new();
    private readonly List<(string Title, string Description, IReadOnlyList<string> Tags)> adds = [];
    private readonly List<(string Id, DateTimeOffset BaseUpdated, string? Title, string? Description, IReadOnlyList<string>? Tags)> updates = [];
    private readonly List<(string Id, byte[] Image, string AltText)> images = [];

    /// <summary>Each Add call's arguments.</summary>
    public IReadOnlyList<(string Title, string Description, IReadOnlyList<string> Tags)> Adds
    {
        get
        {
            lock (gate)
                return [.. adds];
        }
    }

    /// <summary>Each Update call's arguments.</summary>
    public IReadOnlyList<(string Id, DateTimeOffset BaseUpdated, string? Title, string? Description, IReadOnlyList<string>? Tags)> Updates
    {
        get
        {
            lock (gate)
                return [.. updates];
        }
    }

    /// <summary>Each AddImage call's arguments.</summary>
    public IReadOnlyList<(string Id, byte[] Image, string AltText)> Images
    {
        get
        {
            lock (gate)
                return [.. images];
        }
    }

    /// <inheritdoc />
    public TaskItem Add(string title, string description, IReadOnlyList<string> tags)
    {
        lock (gate)
            adds.Add((title, description, tags));
        store.EnterDirect("Direct.Add");
        var normalized = FakeTaskStore.Normalized(tags);
        if (normalized.Count > TagName.MaxPerTask)
            throw new ArgumentException($"A task has at most {TagName.MaxPerTask} tags.", nameof(tags));
        var next = store.Next();
        return store.Insert(new TaskItem("new-" + Guid.NewGuid().ToString("N")[..8], title, description, "rank-new", normalized, next, next, null));
    }

    /// <inheritdoc />
    public TaskItem Update(string taskId, DateTimeOffset baseUpdated, string? title, string? description, IReadOnlyList<string>? tags)
    {
        lock (gate)
            updates.Add((taskId, baseUpdated, title, description, tags));
        store.EnterDirect("Direct.Update " + taskId);
        var task = store.Find(taskId);
        if (task.Updated != baseUpdated)
            throw FakeTaskStore.StaleOf(task);
        var normalized = tags is null ? null : FakeTaskStore.Normalized(tags);
        if (normalized is { Count: > TagName.MaxPerTask } && !normalized.SequenceEqual(task.Tags))
            throw new ArgumentException($"A task has at most {TagName.MaxPerTask} tags.", nameof(tags));
        return store.Replace(task, t => t with
        {
            Title = title ?? t.Title,
            Description = description ?? t.Description,
            Tags = normalized ?? t.Tags,
        });
    }

    /// <inheritdoc />
    public TaskItem AddImage(string taskId, byte[] image, string altText)
    {
        lock (gate)
            images.Add((taskId, image, altText));
        store.EnterDirect("Direct.AddImage " + taskId);
        var task = store.Find(taskId);
        return store.Replace(task, t => t with { Description = t.Description + $"\n![{altText}](images/{taskId}/image.png)" });
    }
}
