using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Ctrl+Shift+C opens a list of the palette's colors; choosing one colors the selection of the field that opened it.</summary>
internal sealed class ColorPicking
{
    private readonly IReadOnlyList<string> _names;
    private readonly ChoiceList _list;
    private readonly PopupLists _popups;
    private ITextField? _target;

    public ColorPicking(IReadOnlyList<string> names, ChoiceList list, PopupLists popups)
    {
        _names = names;
        _list = list;
        _popups = popups;
        list.Chosen += position => Apply(_names[position]);
    }

    /// <summary>Lists the colors; the one chosen goes to the field.</summary>
    public void Open(ITextField field)
    {
        _target = field;
        _popups.Show(_list, _names);
    }

    private void Apply(string name)
    {
        var field = _target ?? throw new InvalidOperationException("A color was chosen with no color list open.");
        _target = null;
        _list.Close();
        field.Run(text => Formatting.Color(text, name, _names));
        field.FocusField();
    }
}
