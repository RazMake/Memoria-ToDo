using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The words searched for, and the tasks they leave. It says when they change.</summary>
internal sealed class ListFilter
{
    private string _searchText = "";

    /// <summary>Raised when the search text changes.</summary>
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

    public void ClearSearch() => SearchText = "";

    /// <summary>The tasks that match the search.</summary>
    public IReadOnlyList<TaskItem> Matching(ITasks tasks) => tasks.Search(_searchText);
}
