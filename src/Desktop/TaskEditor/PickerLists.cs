using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
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
    private readonly ITheme _theme;
    private readonly ChoiceList _colorChoices = new();
    private readonly ChoiceList _snippetChoices = new();
    private readonly ChoiceList _taskChoices = new();
    private readonly ChoiceList _mentionChoices = new();
    private readonly List<MentionPicking> _mentionPickings = [];
    private readonly Dictionary<ITextField, IPopupPlace> _mentionPlaces = [];

    public PickerLists(ITasks tasks, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _theme = theme;
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(palette);
        Contacts = contacts;
        Popups = new PopupLists(_colorChoices, _snippetChoices, _taskChoices, _mentionChoices);
        Colors = new ColorPicking([.. palette.Select(color => color.Name)], _colorChoices, Popups);
        TaskReferences = new TaskReferencePicking(tasks, contacts, _taskChoices, Popups);
        Keys = Popups.AddKeysTo(new KeyMap())
            .Try(ModifierKeys.None, Key.Tab, NextMentionTemplate);
    }

    public IContacts Contacts { get; }

    /// <summary>Makes "@" in the field open the contact list in a popup under it.</summary>
    public void PickMentionsIn(StyledTextEditor field)
    {
        _mentionPickings.Add(new MentionPicking(field, _mentionChoices, Popups, Contacts));
        _mentionPlaces.Add(field, field);
    }

    public PopupLists Popups { get; }

    public ColorPicking Colors { get; }

    public TaskReferencePicking TaskReferences { get; }

    /// <summary>The list the "/" key opens in a Description.</summary>
    public ChoiceList SnippetChoices => _snippetChoices;

    /// <summary>Down, Up, Enter, and Tab, which act on the open list; to run before a field's own keys.</summary>
    public KeyMap Keys { get; }

    /// <summary>Adds the lists to the panel, where they show when open; the contact list is a popup, which goes with the panel.</summary>
    public void AddTo(Panel panel)
    {
        panel.Children.Add(new PopupList("ColorList", _colorChoices));
        panel.Children.Add(new PopupList("SnippetList", _snippetChoices));
        panel.Children.Add(new PopupList("TaskReferenceList", _taskChoices));
        var popup = new MentionPopup(_mentionChoices, ShowingMentions, _mentionPlaces,         NextMentionTemplate, _theme);
        panel.Children.Add(popup);
        PopupClosers.Attach(popup, () => _mentionChoices.Close());
    }

    private bool NextMentionTemplate() => _mentionPickings.Any(picking => picking.NextTemplate());

    private MentionPicking? ShowingMentions() => _mentionPickings.FirstOrDefault(picking => picking.IsShowing);
}
