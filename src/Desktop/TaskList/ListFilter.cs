using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The words searched for, and the tasks they leave. It says when they change, or when contacts change, since a word
/// that starts with "@" may then name another contact. Such a word, when the rest of it finds one contact, leaves the tasks
/// whose title or description mention that contact; every other word is Tasks' search. Every word must match.
/// </summary>
internal sealed class ListFilter : IDisposable
{
    private readonly IContacts _contacts;
    private string _searchText = "";

    public ListFilter(IContacts contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        _contacts = contacts;
        contacts.Changed += OnContactsChanged;
    }

    /// <summary>Raised when the search text changes, or when contacts change, on the thread that made the change.</summary>
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

    /// <summary>Stops following contacts.</summary>
    public void Dispose() => _contacts.Changed -= OnContactsChanged;

    /// <summary>The tasks that match the search.</summary>
    public IReadOnlyList<TaskItem> Matching(ITasks tasks)
    {
        var plain = new List<string>();
        var contactIds = new List<string>();
        foreach (var word in _searchText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length > 1 && word[0] == '@' && _contacts.Find(word[1..]) is { } contact)
            {
                contactIds.Add(contact.Id);
            }
            else
            {
                plain.Add(word);
            }
        }

        var found = tasks.Search(string.Join(' ', plain));
        return contactIds.Count == 0 ? found : [.. found.Where(task => MentionsEvery(task, contactIds))];
    }

    private void OnContactsChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    // Whether the title and description between them mention every one of the contacts, by the ID in each mention's address.
    private static bool MentionsEvery(TaskItem task, List<string> contactIds)
    {
        var mentioned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mention in Mentions.In(task.Title, title: true).Concat(Mentions.In(task.Description, title: false)))
        {
            mentioned.Add(ContactAddress.IdOf(mention.Address)!);
        }

        return contactIds.All(mentioned.Contains);
    }
}
