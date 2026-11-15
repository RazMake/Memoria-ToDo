using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The words searched for and the one chosen tag, and the tasks they leave. It says when either changes.</summary>
internal sealed class ListFilter
{
    private string _searchText = "";

    /// <summary>Raised when the search text or the tag changes.</summary>
    public event EventHandler? Changed;

    /// <summary>The words searched for. Setting a different text raises <see cref="Changed"/>.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_searchText != value)
            {
                _searchText = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The tag the list is narrowed to, or null.</summary>
    public string? TagFilter { get; private set; }

    /// <summary>Chooses the tag, or clears the tag when it is the one already chosen.</summary>
    public void ToggleTag(string tag)
    {
        TagFilter = IsChosen(tag) ? null : tag;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when <paramref name="tag"/> is the tag the list is narrowed to, ignoring case.</summary>
    public bool IsChosen(string tag) => string.Equals(tag, TagFilter, StringComparison.OrdinalIgnoreCase);

    public void ClearTag()
    {
        TagFilter = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSearch() => SearchText = "";

    /// <summary>The tasks that match the search and the tag.</summary>
    public IReadOnlyList<TaskItem> Matching(ITasks tasks) =>
        TagFilter is { } tag ? tasks.Search(_searchText, tag) : tasks.Search(_searchText);
}
