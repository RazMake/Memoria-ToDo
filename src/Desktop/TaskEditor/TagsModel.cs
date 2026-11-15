using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's tags, the + menu's tags, and Custom: the tag text being typed with suggestions from the tags in use, and
/// the color picked for it. It doesn't watch Tasks: whoever does calls <see cref="Refresh"/> when the tags in use change.
/// </summary>
internal sealed class TagsModel
{
    private readonly ITasks _tasks;
    private readonly ITagColors _tagColors;
    private IReadOnlyList<string> _tags = [];
    private string _typedText = "";
    private string? _pickedColor;

    public TagsModel(ITasks tasks, ITagColors tagColors)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(tagColors);
        _tasks = tasks;
        _tagColors = tagColors;
        Suggestions.Chosen += AddSuggestion;
    }

    /// <summary>The suggested tags for the typed text, open while there are any.</summary>
    public ChoiceList Suggestions { get; } = new();

    /// <summary>Raised when the tags, the typed text, the picked color, the message, or whether Custom is open changes.</summary>
    public event Action? Changed;

    /// <summary>Raised after the user adds a tag the task didn't have, once <see cref="Tags"/> holds it and its color is kept.</summary>
    public event Action<string>? TagAdded;

    /// <summary>Raised after the user removes a tag, once <see cref="Tags"/> no longer holds it.</summary>
    public event Action<string>? TagRemoved;

    /// <summary>The tags in order they were added. Setting them clears the typed text, the picked color, and the message; it raises neither TagAdded nor TagRemoved.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _typedText = "";
            _pickedColor = null;
            ColorMessage = "";
            ReplaceTags(value);
        }
    }

    /// <summary>Replaces the tags and leaves Custom as it is: whether it is open, the typed text, the picked color, and the message. It raises neither TagAdded nor TagRemoved.</summary>
    public void ReplaceTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = tags;
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>The tags the + menu lists: the most-used tags in use that the task doesn't have.</summary>
    public IReadOnlyList<string> MenuTags => MostUsedTags.Of(_tasks, _tags);

    /// <summary>Whether Custom, the tag box and the colors, is open.</summary>
    public bool IsCustomOpen { get; private set; }

    /// <summary>The text typed and not yet added as a tag. Typing a tag in use picks the color kept for it, or none.</summary>
    public string TypedText
    {
        get => _typedText;
        set
        {
            _typedText = value;
            PickColorKeptForTypedText();
            Refresh();
            Changed?.Invoke();
        }
    }

    /// <summary>The name of the color picked for the typed tag, or null when none is.</summary>
    public string? PickedColor
    {
        get => _pickedColor;
        set
        {
            _pickedColor = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Why the typed tag wasn't added, or empty.</summary>
    public string ColorMessage { get; private set; } = "";

    public void OpenCustom()
    {
        IsCustomOpen = true;
        Changed?.Invoke();
    }

    /// <summary>Closes Custom, dropping the typed text, the picked color, and the message. Returns false, doing nothing, when it was closed.</summary>
    public bool CloseCustom()
    {
        if (!IsCustomOpen)
        {
            return false;
        }

        IsCustomOpen = false;
        _typedText = "";
        _pickedColor = null;
        ColorMessage = "";
        Refresh();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Adds the tag, spelled as the tag in use if there is one. Custom stays as it is, so what was typed there and the color picked for it stay too.</summary>
    public void Add(string tag)
    {
        var before = _tags.Count;
        ReplaceTags(_tasks.WithTag(_tags, tag));
        if (_tags.Count > before)
        {
            TagAdded?.Invoke(_tags[^1]);
        }
    }

    /// <summary>
    /// Adds the typed text as a tag, if there is any, keeping the picked color for it first, and closes Custom. Returns
    /// true when nothing was typed or the tag was added; false when the color couldn't be kept, which leaves Custom open
    /// with the text, the color, and the reason.
    /// </summary>
    public bool AddTypedText()
    {
        if (string.IsNullOrWhiteSpace(_typedText))
        {
            return true;
        }

        return Accept(_tasks.WithTag([], _typedText)[0]);
    }

    /// <summary>Enter: adds the suggestion Down moved to, or else the typed text.</summary>
    public void AddChosenOrTyped()
    {
        if (!Suggestions.ChooseMoved())
        {
            AddTypedText();
        }
    }

    /// <summary>Accepts the suggestion at the position, as a click does, as Enter accepts the typed text: see <see cref="AddTypedText"/>.</summary>
    public void AddSuggestion(int position) => Accept(Suggestions.Items[position]);

    public void Remove(string tag)
    {
        var remaining = _tags.Where(existing => existing != tag).ToList();
        var removed = remaining.Count < _tags.Count;
        Tags = remaining;
        if (removed)
        {
            TagRemoved?.Invoke(tag);
        }
    }

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
            Remove(_tags[^1]);
        }

        return true;
    }

    // Keeps the picked color for the tag, if any, then adds it and closes Custom, dropping what was typed there. Fails, changing nothing, when the color can't be kept.
    private bool Accept(string tag)
    {
        if (_pickedColor is { } color && !TryKeepColor(tag, color))
        {
            return false;
        }

        IsCustomOpen = false;
        _typedText = "";
        _pickedColor = null;
        ColorMessage = "";
        Add(tag);
        return true;
    }

    // A typed tag that has a kept color shows it as picked; one in use without a color picks none; a new tag leaves the pick alone.
    private void PickColorKeptForTypedText()
    {
        var typed = _typedText.Trim();
        if (typed.Length == 0)
        {
            return;
        }

        var kept = _tagColors.ColorOf(typed);
        if (kept is not null || _tasks.TagsInUse().Any(use => string.Equals(use.Tag, typed, StringComparison.OrdinalIgnoreCase)))
        {
            _pickedColor = kept;
        }
    }

    private bool TryKeepColor(string tag, string color)
    {
        try
        {
            _tagColors.SetColor(tag, color);
        }
        catch (Exception failure) when (TagColorFailures.MessageOf(failure) is not null)
        {
            ColorMessage = TagColorFailures.MessageOf(failure)!;
            IsCustomOpen = true;
            Changed?.Invoke();
            return false;
        }

        return true;
    }
}
