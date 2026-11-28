using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The menu the pill opens, without a screen: whether it is open, its top tags, Search and its matches, which item the keys are on, and
/// what choosing an item or pressing a key does. Items are numbered from 0: the top tags, then Search, then Custom…. The
/// menu follows the tags model, so an open menu stays right when the task's tag or the tags in use change.
/// </summary>
internal sealed class TagMenuModel
{
    private readonly ITasks _tasks;
    private readonly TagsModel _tags;
    private IReadOnlyList<string> _topTags = [];
    private IReadOnlyList<string> _matches = [];
    private string _searchText = "";

    public TagMenuModel(ITasks tasks, TagsModel tags)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(tags);
        _tasks = tasks;
        _tags = tags;
        tags.Changed += OnTagsChanged;
    }

    /// <summary>Raised when the menu opens or closes, the key position, the matches, or the lists change.</summary>
    public event Action? Changed;

    public bool IsOpen { get; private set; }

    /// <summary>The tags listed at the top of the open menu, most-used first; empty while it is closed.</summary>
    public IReadOnlyList<string> TopTags => _topTags;

    /// <summary>The item number of Search.</summary>
    public int SearchIndex => _topTags.Count;

    /// <summary>The item number of Custom….</summary>
    public int CustomIndex => _topTags.Count + 1;

    /// <summary>The item the keys are on, or -1 when none. Moving onto Search puts the cursor in its box.</summary>
    public int FocusedIndex { get; private set; } = -1;

    /// <summary>The text in the Search box, as typed. Setting it while the menu is closed is rejected; closing the menu clears it.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!IsOpen)
            {
                throw new InvalidOperationException("The Search text can only be set while the menu is open.");
            }

            _searchText = value;
            FocusedIndex = SearchIndex;
            HighlightedMatch = -1;
            _matches = FindMatches();
            Changed?.Invoke();
        }
    }

    /// <summary>The tags that match the Search text, most-used first; empty while the box is empty.</summary>
    public IReadOnlyList<string> Matches => _matches;

    /// <summary>The match the keys highlighted, or -1 when none is.</summary>
    public int HighlightedMatch { get; private set; } = -1;

    /// <summary>Whether the matches submenu shows, which it does while Search holds text.</summary>
    public bool IsSubmenuOpen => IsOpen && _searchText.Length > 0;

    /// <summary>Whether the submenu shows its "No matching tags" line.</summary>
    public bool ShowsNoMatch => IsSubmenuOpen && _matches.Count == 0;

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _topTags = _tags.MenuTags;
        Changed?.Invoke();
    }

    /// <summary>Closes the menu, dropping the Search text. Returns false, doing nothing, when it was closed.</summary>
    public bool Close()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsOpen = false;
        _topTags = [];
        _matches = [];
        _searchText = "";
        FocusedIndex = -1;
        HighlightedMatch = -1;
        Changed?.Invoke();
        return true;
    }

    /// <summary>A click on the pill: opens the menu, or closes it when it is open.</summary>
    public void Toggle()
    {
        if (!Close())
        {
            Open();
        }
    }

    /// <summary>Esc. Closes the menu and drops the Search text. Returns false, doing nothing, when it was closed.</summary>
    public bool Escape() => Close();

    /// <summary>The cursor went into the Search box, by a click: the keys are on Search now.</summary>
    public void FocusSearch()
    {
        if (IsOpen && FocusedIndex != SearchIndex)
        {
            FocusedIndex = SearchIndex;
            HighlightedMatch = -1;
            Changed?.Invoke();
        }
    }

    /// <summary>Down: moves through the tags, Search, and Custom…, staying on Custom…. In Search it moves through the matches, or to Custom… when there are none.</summary>
    public void MoveDown()
    {
        if (FocusedIndex == SearchIndex && _matches.Count > 0)
        {
            HighlightedMatch = Math.Min(HighlightedMatch + 1, _matches.Count - 1);
        }
        else
        {
            MoveTo(Math.Min(FocusedIndex + 1, CustomIndex));
        }

        Changed?.Invoke();
    }

    /// <summary>Up: moves back through Custom…, Search, and the tags, staying on the first. In Search it moves back through the matches; from the first, or with none, it goes to the last tag.</summary>
    public void MoveUp()
    {
        if (FocusedIndex == SearchIndex && HighlightedMatch >= 0)
        {
            HighlightedMatch--;
        }
        else if (FocusedIndex >= 0)
        {
            MoveTo(Math.Max(FocusedIndex - 1, 0));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// A character typed while the cursor is outside Search: a letter or digit goes at the end of the Search text and the
    /// cursor into its box. Returns false, doing nothing, for any other character or while the menu is closed.
    /// </summary>
    public bool TypeOutsideSearch(char typed)
    {
        if (!IsOpen || !char.IsLetterOrDigit(typed))
        {
            return false;
        }

        SearchText += typed;
        return true;
    }

    /// <summary>Enter: chooses the tag the keys are on, or opens Custom…. In Search it chooses the highlighted match, or the first; with no match it does nothing.</summary>
    public void Enter()
    {
        if (FocusedIndex < 0)
        {
            return;
        }

        if (FocusedIndex < SearchIndex)
        {
            ChooseTag(_topTags[FocusedIndex]);
        }
        else if (FocusedIndex == SearchIndex)
        {
            if (_matches.Count > 0)
            {
                ChooseMatch(Math.Max(HighlightedMatch, 0));
            }
        }
        else
        {
            ChooseCustom();
        }
    }

    /// <summary>Chooses the tag, replacing the tag the menu opened from or adding it at the end, and closes the menu, dropping the Search text. Custom stays as it is.</summary>
    public void ChooseTag(string tag)
    {
        Close();
        _tags.Choose(tag);
    }

    /// <summary>Chooses the match at the position, as <see cref="ChooseTag"/> does.</summary>
    public void ChooseMatch(int position) => ChooseTag(_matches[position]);

    /// <summary>Closes the menu, dropping the Search text, and opens Custom.</summary>
    public void ChooseCustom()
    {
        Close();
        _tags.OpenCustom();
    }

    // Leaving Search drops the highlight, so coming back to it starts with none.
    private void MoveTo(int index)
    {
        FocusedIndex = index;
        HighlightedMatch = -1;
    }

    private IReadOnlyList<string> FindMatches() =>
        _searchText.Length == 0 ? [] : _tasks.SuggestTags(_searchText, _tags.Tags);

    // An open menu keeps the item the keys are on while its lists change under it.
    private void OnTagsChanged()
    {
        if (!IsOpen)
        {
            return;
        }

        if (_tags.TargetLost)
        {
            Close();
            return;
        }

        var wasSearch = FocusedIndex == SearchIndex;
        var wasCustom = FocusedIndex == CustomIndex;
        _topTags = _tags.MenuTags;
        _matches = FindMatches();
        if (wasSearch)
        {
            FocusedIndex = SearchIndex;
        }
        else if (wasCustom)
        {
            FocusedIndex = CustomIndex;
        }
        else
        {
            FocusedIndex = Math.Min(FocusedIndex, _topTags.Count - 1);
        }

        HighlightedMatch = Math.Min(HighlightedMatch, _matches.Count - 1);
        Changed?.Invoke();
    }
}
