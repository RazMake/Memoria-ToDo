using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's one tag, held as a list of at most one, the top tags the menu lists, and Custom: the tag text being typed, the
/// color Enter would give it, and choosing it with Enter or in a clicked color. It doesn't watch Tasks: whoever does calls
/// <see cref="Refresh"/> when the tags in use change.
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

    /// <summary>Raised after the user chooses a tag the task didn't have as its tag, once <see cref="Tags"/> holds only it and its color is kept.</summary>
    public event Action<string>? TagAdded;

    /// <summary>Raised after the user clears the tag, once <see cref="Tags"/> is empty, with the tag that was cleared.</summary>
    public event Action<string>? TagRemoved;

    /// <summary>The task's tag: none or one. Setting them clears the typed text and the message; it raises neither TagAdded nor TagRemoved.</summary>
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

    /// <summary>The tags the menu lists at its top: the most-used tags in use other than the task's tag.</summary>
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

    /// <summary>
    /// Chooses the tag as the task's only tag, spelled as the tag in use if there is one, keeping no color. Choosing the tag the task
    /// has, ignoring case, changes and raises nothing. Custom stays as it is, so what was typed there stays too.
    /// </summary>
    public void Add(string tag) => Choose(tag);

    /// <summary>
    /// Enter: chooses the typed text as the tag, if there is any, keeping no color, and closes Custom, which observers are told of
    /// even when the tag is the one the task has. Returns true when nothing was typed or the tag was chosen.
    /// </summary>
    public bool AddTypedText()
    {
        if (!ColorsShown)
        {
            return true;
        }

        ChooseTyped(TypedTag());
        return true;
    }

    /// <summary>
    /// A click on a color: keeps the color for the typed tag, then chooses it and closes Custom. Returns true when nothing
    /// was typed or the tag was chosen; false when the color couldn't be kept, which leaves Custom open with the text and
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

        ChooseTyped(tag);
        return true;
    }

    /// <summary>Clears the task's tag and raises <see cref="TagRemoved"/> with it; does nothing when the task has none. Custom stays as it is.</summary>
    public void Clear()
    {
        if (_tags.Count == 0)
        {
            return;
        }

        var cleared = _tags[0];
        ReplaceTags([]);
        TagRemoved?.Invoke(cleared);
    }

    /// <summary>Raises <see cref="Changed"/>, after the tags in use changed, so whatever shows them draws them again.</summary>
    public void Refresh() => Changed?.Invoke();

    // The typed text, spelled as the tag in use if there is one.
    private string TypedTag() => _tasks.WithTag([], _typedText)[0];

    // Closes Custom and chooses the tag; observers hear of the closing even when the tag is the task's own and nothing else changes.
    private void ChooseTyped(string tag)
    {
        HideCustom();
        if (!Choose(tag))
        {
            Changed?.Invoke();
        }
    }

    // False, raising nothing, when the task has the tag, ignoring case.
    private bool Choose(string tag)
    {
        var chosen = _tasks.WithTag([], tag)[0];
        if (_tags.Count > 0 && string.Equals(_tags[0], chosen, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ReplaceTags([chosen]);
        TagAdded?.Invoke(chosen);
        return true;
    }

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
