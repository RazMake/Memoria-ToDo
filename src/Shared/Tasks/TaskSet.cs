namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The tasks in rank order, with the tags they use.</summary>
internal sealed class TaskSet
{
    public static readonly TaskSet Empty = new([]);

    public TaskSet(IEnumerable<TaskItem> tasks)
    {
        var sorted = tasks.ToList();
        sorted.Sort(static (a, b) =>
        {
            var byRank = string.CompareOrdinal(a.Rank, b.Rank);
            return byRank != 0 ? byRank : string.CompareOrdinal(a.Id, b.Id);
        });
        Items = sorted.AsReadOnly();
        Tags = TagCatalog.Of(sorted);
    }

    public IReadOnlyList<TaskItem> Items { get; }

    public TagCatalog Tags { get; }

    public bool Contains(string id) => IndexOf(id) >= 0;

    /// <summary>The task's place in <see cref="Items"/>, or -1 when it isn't there.</summary>
    public int IndexOf(string id)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id)
                return i;
        }

        return -1;
    }

    public TaskSet WithTask(TaskItem task) => new(Items.Append(task));

    /// <summary>The set with the task of the same ID replaced by <paramref name="task"/>.</summary>
    public TaskSet ReplacingTask(TaskItem task) => new(Items.Where(item => item.Id != task.Id).Append(task));

    public TaskSet WithoutTask(string id) => new(Items.Where(item => item.Id != id));

    /// <summary><see cref="Search(string)"/>, narrowed to the tasks that have <paramref name="tag"/>, ignoring case.</summary>
    public IReadOnlyList<TaskItem> Search(string text, string tag) =>
        Search(text).Where(task => TagCatalog.Has(task.Tags, tag)).ToList();

    /// <summary>The tasks that hold every word of <paramref name="text"/> in their title, description, or tags, ignoring case.</summary>
    public IReadOnlyList<TaskItem> Search(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return Items.Where(task => words.All(word => Holds(task, word))).ToList();
    }

    private static bool Holds(TaskItem task, string word) =>
        task.Title.Contains(word, StringComparison.OrdinalIgnoreCase)
        || task.Description.Contains(word, StringComparison.OrdinalIgnoreCase)
        || task.Tags.Any(tag => tag.Contains(word, StringComparison.OrdinalIgnoreCase));
}
