namespace Memoria.ToDo.Desktop.Logs;

/// <summary>One component of the list, with its check box. Checking or clearing it, other than through <see cref="Set"/>, tells the model.</summary>
internal sealed class ComponentChoice
{
    private readonly Action _changed;
    private bool _isChecked;

    public ComponentChoice(string name, bool isChecked, Action changed)
    {
        Name = name;
        _isChecked = isChecked;
        _changed = changed;
    }

    public string Name { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                _changed();
            }
        }
    }

    /// <summary>Checks or clears the box without telling the model, for the model's own change of many boxes.</summary>
    internal void Set(bool isChecked) => _isChecked = isChecked;
}
