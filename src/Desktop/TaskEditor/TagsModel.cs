using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's tags, the top tags the + menu lists, and Custom: the tag text being typed, the color Enter would give it, and
/// adding it with Enter or in a clicked color. It doesn't watch Tasks: whoever does calls <see cref="Refresh"/> when the
/// tags in use change.
/// </summary>
internal sealed class TagsModel
{
    private readonly ITasks _tasks;
    private readonly ITagColors _tagColors;
    private IReadOnlyList<string> _tags = [];
    private string _typedText = "";

    public TagsModel(ITasks tasks, ITagColors tagColors)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(tagColors);
        _tasks = tasks;
        _tagColors = tagColors;
    }

    /// <summary>Raised when the tags, the typed text, the message, or whether Custom is open changes, or after <see cref="Refresh"/>.</summary>
    public event Action? Changed;

    /// <summary>Raised after the user adds a tag the task didn't have, once <see cref="Tags"/> holds it and its color is kept.</summary>
    public event Action<string>? TagAdded;

    /// <summary>Raised after the user removes a tag, once <see cref="Tags"/> no longer holds it.</summary>
    public event Action<string>? TagRemoved;

    /// <summary>The tags in order they were added. Setting them clears the typed text and the message; it raises neither TagAdded nor TagRemoved.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _typedText = "";
            ColorMessage = "";
            ReplaceTags(value);
        }
    }

    /// <summary>Replaces the tags and leaves Custom as it is: whether it is open, the typed text, and the message. It raises neither TagAdded nor TagRemoved.</summary>
    public void ReplaceTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = tags;
        Changed?.Invoke();
    }

    /// <summary>The tags the + menu lists at its top: the most-used tags in use that the task doesn't have.</summary>
    public IReadOnlyList<string> MenuTags => MostUsedTags.Of(_tasks, _tags);

    /// <summary>Whether Custom, the New tag box and the colors, is open.</summary>
    public bool IsCustomOpen { get; private set; }

    /// <summary>The text typed and not yet added as a tag. Changing it clears the message.</summary>
    public string TypedText
    {
        get => _typedText;
        set
        {
            _typedText = value;
            ColorMessage = "";
            Changed?.Invoke();
        }
    }

    /// <summary>Whether Custom shows its colors, which it does while the box holds text.</summary>
    public bool ColorsShown => !string.IsNullOrWhiteSpace(_typedText);

    /// <summary>The name of the color Enter would give the typed tag, its kept color or its default, or null while the colors don't show.</summary>
    public string? MarkedColor => ColorsShown ? TagColorOf.Of(_tagColors, TypedTag()).Name : null;

    /// <summary>Why the typed tag wasn't added, or empty.</summary>
    public string ColorMessage { get; private set; } = "";

    public void OpenCustom()
    {
        IsCustomOpen = true;
        Changed?.Invoke();
    }

    /// <summary>Closes Custom, dropping the typed text and the message. Returns false, doing nothing, when it was closed.</summary>
    public bool CloseCustom()
    {
        if (!IsCustomOpen)
        {
            return false;
        }

        HideCustom();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Adds the tag, spelled as the tag in use if there is one, keeping no color. Custom stays as it is, so what was typed there stays too.</summary>
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
    /// Enter: adds the typed text as a tag, if there is any, keeping no color, and closes Custom. Returns true when
    /// nothing was typed or the tag was added.
    /// </summary>
    public bool AddTypedText()
    {
        if (!ColorsShown)
        {
            return true;
        }

        var tag = TypedTag();
        HideCustom();
        Add(tag);
        return true;
    }

    /// <summary>
    /// A click on a color: keeps the color for the typed tag, then adds it and closes Custom. Returns true when nothing
    /// was typed or the tag was added; false when the color couldn't be kept, which leaves Custom open with the text and
    /// the reason in <see cref="ColorMessage"/>.
    /// </summary>
    public bool AddTypedTextInColor(string color)
    {
        if (!ColorsShown)
        {
            return true;
        }

        var tag = TypedTag();
        if (!TryKeepColor(tag, color))
        {
            return false;
        }

        HideCustom();
        Add(tag);
        return true;
    }

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

    /// <summary>Raises <see cref="Changed"/>, after the tags in use changed, so whatever shows them draws them again.</summary>
    public void Refresh() => Changed?.Invoke();

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

    // The typed text, spelled as the tag in use if there is one.
    private string TypedTag() => _tasks.WithTag([], _typedText)[0];

    // Closes Custom, dropping what was typed there, without raising Changed: the caller does.
    private void HideCustom()
    {
        IsCustomOpen = false;
        _typedText = "";
        ColorMessage = "";
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
            Changed?.Invoke();
            return false;
        }

        return true;
    }
}
