using Memoria.ToDo.Desktop.TaskEditor;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>The shared fake of <see cref="IImageFileChooser"/>. A test sets the path it answers.</summary>
public sealed class FakeImageFileChooser : IImageFileChooser
{
    /// <summary>The path the user "chooses"; null means the user cancels.</summary>
    public string? Path { get; set; }

    /// <summary>How many times the dialog was opened.</summary>
    public int Opened { get; private set; }

    /// <summary>What the user does while the dialog is open, such as another window becoming active. Runs inside <see cref="Choose"/>.</summary>
    public Action? WhileOpen { get; set; }

    /// <inheritdoc />
    public string? Choose()
    {
        Opened++;
        WhileOpen?.Invoke();
        return Path;
    }
}
