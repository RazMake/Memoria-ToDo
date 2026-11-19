using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The lists an editor's text fields pop up: colors, snippets, and tasks to link to. At most one is open; Down and Enter
/// act on it, and Esc closes it. Fields that share an editor share these.
/// </summary>
internal sealed class PickerLists
{
    private readonly ChoiceList _colorChoices = new();
    private readonly ChoiceList _snippetChoices = new();
    private readonly ChoiceList _taskChoices = new();
    private readonly ChoiceList _mentionChoices = new();
    private readonly List<MentionPicking> _mentionPickings = [];
    private readonly Dictionary<ITextField, Panel> _mentionHosts = [];

    public PickerLists(ITasks tasks, IContacts contacts, IReadOnlyList<PaletteColor> palette)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(palette);
        Contacts = contacts;
        Popups = new PopupLists(_colorChoices, _snippetChoices, _taskChoices, _mentionChoices);
        Colors = new ColorPicking([.. palette.Select(color => color.Name)], _colorChoices, Popups);
        TaskReferences = new TaskReferencePicking(tasks, contacts, _taskChoices, Popups);
        Keys = new KeyMap()
            .Try(ModifierKeys.None, Key.Down, Popups.MoveDown)
            .Try(ModifierKeys.None, Key.Enter, Popups.ChooseMovedOrClose)
            .Try(ModifierKeys.None, Key.Tab, () => _mentionPickings.Any(picking => picking.NextTemplate()));
    }

    public IContacts Contacts { get; }

    /// <summary>Makes "@" in the field open the contact list.</summary>
    public void PickMentionsIn(ITextField field) =>
        _mentionPickings.Add(new MentionPicking(field, _mentionChoices, Popups, Contacts));

    /// <summary>
    /// Shows the contact list in <paramref name="host"/>, which sits under the field, while the list is open for the field,
    /// instead of where <see cref="AddTo"/> put it.
    /// </summary>
    public void ShowMentionsBelow(ITextField field, Panel host) => _mentionHosts.Add(field, host);

    public PopupLists Popups { get; }

    public ColorPicking Colors { get; }

    public TaskReferencePicking TaskReferences { get; }

    /// <summary>The list the "/" key opens in a Description.</summary>
    public ChoiceList SnippetChoices => _snippetChoices;

    /// <summary>Down and Enter, which choose from the open list; to run before a field's own keys.</summary>
    public KeyMap Keys { get; }

    /// <summary>Adds the lists to the panel, where they show when open.</summary>
    public void AddTo(Panel panel)
    {
        panel.Children.Add(new PopupList("ColorList", _colorChoices));
        panel.Children.Add(new PopupList("SnippetList", _snippetChoices));
        panel.Children.Add(new PopupList("TaskReferenceList", _taskChoices));
        var mentionList = new MentionPopupList(_mentionChoices, ShowingMentions);
        panel.Children.Add(mentionList);
        _mentionChoices.Changed += () => MoveUnderItsField(mentionList);
    }

    private MentionPicking? ShowingMentions() => _mentionPickings.FirstOrDefault(picking => picking.IsShowing);

    // The contact list sits under the field it was opened from, when that field has a place for it.
    private void MoveUnderItsField(MentionPopupList list)
    {
        if (ShowingMentions() is { } showing
            && _mentionHosts.TryGetValue(showing.Field, out var host)
            && list.Parent != host)
        {
            ((Panel)list.Parent).Children.Remove(list);
            host.Children.Add(list);
        }
    }
}
