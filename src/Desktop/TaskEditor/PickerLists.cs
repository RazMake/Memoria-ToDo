using System.Windows.Controls;
using System.Windows.Input;
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

    public PickerLists(ITasks tasks, IReadOnlyList<PaletteColor> palette)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(palette);
        Popups = new PopupLists(_colorChoices, _snippetChoices, _taskChoices);
        Colors = new ColorPicking([.. palette.Select(color => color.Name)], _colorChoices, Popups);
        TaskReferences = new TaskReferencePicking(tasks, _taskChoices, Popups);
        Keys = new KeyMap()
            .Try(ModifierKeys.None, Key.Down, Popups.MoveDown)
            .Try(ModifierKeys.None, Key.Enter, Popups.ChooseMovedOrClose);
    }

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
    }
}
