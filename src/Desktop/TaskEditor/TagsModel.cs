using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The task's ordered list of up to <see cref="TagName.MaxPerTask"/> tags, which the pills show: adding at the end, replacing or removing
/// one, and moving one. Also the top tags the menu lists, and Custom: the tag text being typed, the color Enter would give it,
/// and choosing it with Enter or in a clicked color. It doesn't watch Tasks: whoever does calls <see cref="Refresh"/> when the
/// tags in use change.
/// </summary>
internal sealed class TagsModel
{
    private readonly ITasks _tasks;
    private readonly ITagColors _tagColors;
    private readonly ILogger _logger;
    private IReadOnlyList<string> _tags = [];
    private string? _targetTag;
    private string _typedText = "";

    public TagsModel(ITasks tasks, ITagColors tagColors, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(tagColors);
        _tasks = tasks;
        _tagColors = tagColors;
        _logger = logger;
    }

    /// <summary>Raised when the tags, the typed text, the message, or whether Custom is open changes, or after <see cref="Refresh"/>.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised once for each change the user makes to the tags, with the whole list, once <see cref="Tags"/> holds it and any color
    /// is kept: a tag added, replaced, removed, or moved. Never raised for a choice that changes nothing or for setting the tags.
    /// </summary>
    public event Action<IReadOnlyList<string>>? TagsChanged;

    /// <summary>Raised after <see cref="Choose"/> changed the tags, with the place of the tag it put in, so the cursor can follow it.</summary>
    public event Action<int>? Chosen;

    /// <summary>The task's tags in order. Setting them clears the typed text and the message; it raises no <see cref="TagsChanged"/>.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _typedText = "";
            _targetTag = null;
            ColorMessage = "";
            ReplaceTags(value);
        }
    }

    /// <summary>
    /// The place of the tag the open menu or Custom replaces, or null when it adds at the end. The model remembers the tag, not
    /// the place, so the target follows the tag when the list changes. <see cref="Choose"/> uses it.
    /// </summary>
    public int? Target
    {
        get => _targetTag is null ? null : IndexOfTag(_targetTag) is var index and >= 0 ? index : null;
        set => _targetTag = value is { } place ? _tags[place] : null;
    }

    /// <summary>Whether the tag the menu or Custom replaces is gone from the list, as when a sync removed it, so nothing is left to replace.</summary>
    public bool TargetLost => _targetTag is not null && IndexOfTag(_targetTag) < 0;

    /// <summary>Whether the task has <see cref="TagName.MaxPerTask"/> tags, so no more are added.</summary>
    public bool IsFull => _tags.Count >= TagName.MaxPerTask;

    /// <summary>Replaces the tags and leaves Custom as it is: whether it is open, the typed text, and the message. It raises no <see cref="TagsChanged"/>. <see cref="Target"/> follows its tag, or is <see cref="TargetLost"/>.</summary>
    public void ReplaceTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = [.. tags];
        Changed?.Invoke();
    }

    /// <summary>The tags the menu lists at its top: the most-used tags in use other than the task's tags.</summary>
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
    /// Adds the tag at the end, normalized as Tasks does. Returns false, changing and raising nothing, when the task has the tag or
    /// already has <see cref="TagName.MaxPerTask"/>. Custom stays as it is, so what was typed there stays too.
    /// </summary>
    public bool Add(string tag)
    {
        var chosen = Normalized(tag);
        if (Has(chosen))
        {
            _logger.Debug("The tag is already one of the task's {Count} tags, so it isn't added again", _tags.Count);
            return false;
        }

        if (IsFull)
        {
            _logger.Debug("The task already has {Count} tags, so no tag is added", _tags.Count);
            return false;
        }

        return Change([.. _tags, chosen], "Added");
    }

    /// <summary>
    /// Puts the tag, normalized as Tasks does, in place of the tag at <paramref name="index"/>. Returns false, changing and raising
    /// nothing, when the task has the tag already. Throws <see cref="ArgumentOutOfRangeException"/> for a place outside the list.
    /// </summary>
    public bool Replace(int index, string tag)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _tags.Count);
        var chosen = Normalized(tag);
        if (Has(chosen))
        {
            _logger.Debug("The tag is already one of the task's {Count} tags, so it doesn't replace the tag at {Index}", _tags.Count, index);
            return false;
        }

        var replaced = _tags.ToArray();
        replaced[index] = chosen;
        return Change(replaced, "Replaced");
    }

    /// <summary>Removes the tag at <paramref name="index"/>. Throws <see cref="ArgumentOutOfRangeException"/> for a place outside the list.</summary>
    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _tags.Count);
        Change([.. _tags.Where((_, place) => place != index)], "Removed");
    }

    /// <summary>
    /// Moves the tag at <paramref name="from"/> to place <paramref name="to"/>, raising <see cref="TagsChanged"/> once. Does nothing
    /// when they are the same place. Throws <see cref="ArgumentOutOfRangeException"/> for a place outside the list.
    /// </summary>
    public void Move(int from, int to)
    {
        var moved = TagOrder.Move(_tags, from, to);
        if (from != to)
        {
            Change(moved, "Moved");
        }
    }

    /// <summary>
    /// Chooses the tag from the menu or Custom: it replaces the tag at <see cref="Target"/>, or is added at the end when there is none,
    /// and <see cref="Target"/> is forgotten. Returns false, changing and raising nothing, when the task has the tag or is full.
    /// </summary>
    public bool Choose(string tag)
    {
        var target = Target;
        if (TargetLost)
        {
            _targetTag = null;
            _logger.Information("The tag the choice would replace is gone from the task, so nothing is replaced");
            return false;
        }

        _targetTag = null;
        var changed = target is { } index ? Replace(index, tag) : Add(tag);
        if (changed)
        {
            Chosen?.Invoke(target ?? _tags.Count - 1);
        }

        return changed;
    }

    /// <summary>
    /// Enter: chooses the typed text as the tag, if there is any, keeping no color, and closes Custom, which observers are told of
    /// even when the tag is the one the task has. Returns true when nothing was typed or the tag was chosen; false, leaving
    /// Custom open with the text and the reason in <see cref="ColorMessage"/>, when the task is full.
    /// </summary>
    public bool AddTypedText()
    {
        if (!ColorsShown)
        {
            return true;
        }

        var tag = TypedTag();
        if (IsRefusedAsFull(tag))
        {
            return false;
        }

        ChooseTyped(tag);
        return true;
    }

    /// <summary>
    /// A click on a color: keeps the color for the typed tag, then chooses it and closes Custom. Returns true when nothing
    /// was typed or the tag was chosen; false when the task is full or the color couldn't be kept, which leaves Custom open
    /// with the text and the reason in <see cref="ColorMessage"/>.
    /// </summary>
    public bool AddTypedTextInColor(string color)
    {
        if (!ColorsShown)
        {
            return true;
        }

        var tag = TypedTag();
        if (IsRefusedAsFull(tag) || !TryKeepColor(tag, color))
        {
            return false;
        }

        ChooseTyped(tag);
        return true;
    }

    /// <summary>Raises <see cref="Changed"/>, after the tags in use changed, so whatever shows them draws them again.</summary>
    public void Refresh() => Changed?.Invoke();

    // The typed text, normalized as Tasks does.
    private string TypedTag() => Normalized(_typedText);

    private string Normalized(string tag) => _tasks.WithTag([], tag)[0];

    private bool Has(string normalized) => _tags.Contains(normalized, StringComparer.Ordinal);

    private int IndexOfTag(string tag)
    {
        for (var place = 0; place < _tags.Count; place++)
        {
            if (string.Equals(_tags[place], tag, StringComparison.Ordinal))
            {
                return place;
            }
        }

        return -1;
    }

    // A full task takes no new tag at the end, but still a replacement or a tag it has. Says why in the message when it refuses.
    private bool IsRefusedAsFull(string tag)
    {
        if (!IsFull || _targetTag is not null || Has(tag))
        {
            return false;
        }

        _logger.Information("The task has {Count} tags, so the typed tag isn't added", _tags.Count);
        ColorMessage = $"A task has at most {TagName.MaxPerTask} tags.";
        Changed?.Invoke();
        return true;
    }

    // Closes Custom and chooses the tag; observers hear of the closing even when the tag is the task's own and nothing else changes.
    private void ChooseTyped(string tag)
    {
        HideCustom();
        if (!Choose(tag))
        {
            Changed?.Invoke();
        }
    }

    // Holds the new list, tells whatever shows the tags, then raises TagsChanged. Always true, for the callers that return it.
    private bool Change(IReadOnlyList<string> tags, string what)
    {
        ReplaceTags(tags);
        _logger.Information("{What} a tag, so the task has {Count} tags: {Tags}", what, tags.Count, string.Join(", ", tags));
        TagsChanged?.Invoke(_tags);
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
            _logger.Error(failure, "A tag's color couldn't be kept");
            ColorMessage = TagColorFailures.MessageOf(failure)!;
            Changed?.Invoke();
            return false;
        }

        return true;
    }
}
