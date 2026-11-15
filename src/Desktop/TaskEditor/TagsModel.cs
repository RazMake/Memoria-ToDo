using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's tags and the tag text being typed, with suggestions from the tags in use. It doesn't watch Tasks: whoever
/// does calls <see cref="Refresh"/> when the tags in use change.
/// </summary>
internal sealed class TagsModel
{
    private readonly ITasks _tasks;
    private IReadOnlyList<string> _tags = [];
    private string _typedText = "";

    public TagsModel(ITasks tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        _tasks = tasks;
        Suggestions.Chosen += AddSuggestion;
    }

    /// <summary>The suggested tags for the typed text, open while there are any.</summary>
    public ChoiceList Suggestions { get; } = new();

    /// <summary>Raised when <see cref="Tags"/> or <see cref="TypedText"/> is set.</summary>
    public event Action? Changed;

    /// <summary>The tags in the order they were added. Setting them clears the typed text.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _tags = value;
            _typedText = "";
            Refresh();
            Changed?.Invoke();
        }
    }

    /// <summary>The text typed and not yet added as a tag.</summary>
    public string TypedText
    {
        get => _typedText;
        set
        {
            _typedText = value;
            Refresh();
            Changed?.Invoke();
        }
    }

    /// <summary>Adds the tag, spelled as the tag in use if there is one, and clears the typed text.</summary>
    public void Add(string tag) => Tags = _tasks.WithTag(_tags, tag);

    /// <summary>Adds the typed text as a tag, if there is any, so saving loses none of it.</summary>
    public void AddTypedText()
    {
        if (!string.IsNullOrWhiteSpace(_typedText))
        {
            Add(_typedText);
        }
    }

    /// <summary>Enter: adds the suggestion Down moved to, or else the typed text.</summary>
    public void AddChosenOrTyped()
    {
        if (!Suggestions.ChooseMoved())
        {
            AddTypedText();
        }
    }

    /// <summary>Adds the suggestion at the position, as a click does.</summary>
    public void AddSuggestion(int position) => Add(Suggestions.Items[position]);

    public void Remove(string tag) => Tags = [.. _tags.Where(existing => existing != tag)];

    /// <summary>Moves to the next suggestion, if any are open.</summary>
    public void MoveDown()
    {
        if (Suggestions.IsOpen)
        {
            Suggestions.MoveDown();
        }
    }

    /// <summary>Closes the suggestions. Returns false, doing nothing, when none were open.</summary>
    public bool CloseSuggestions() => Suggestions.Close();

    /// <summary>Lists the suggestions again, after the tags in use changed.</summary>
    public void Refresh()
    {
        var suggestions = _tasks.SuggestTags(_typedText, _tags);
        if (suggestions.Count > 0)
        {
            Suggestions.Show(suggestions);
        }
        else
        {
            Suggestions.Close();
        }
    }

    /// <summary>
    /// Backspace. On an empty line it removes the last tag, if any, and returns true; with text in the line it returns
    /// false, leaving the key to the line.
    /// </summary>
    public bool Backspace()
    {
        if (_typedText.Length > 0)
        {
            return false;
        }

        if (_tags.Count > 0)
        {
            Tags = [.. _tags.Take(_tags.Count - 1)];
        }

        return true;
    }
}
