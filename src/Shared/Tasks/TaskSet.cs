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

    public bool Contains(string id) => Items.Any(task => task.Id == id);

    public TaskSet WithTask(TaskItem task) => new(Items.Append(task));

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
