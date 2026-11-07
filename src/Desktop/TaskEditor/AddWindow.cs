namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Creates the Add window.</summary>
public static class AddWindow
{
    /// <summary>Creates the Add window. It opens only on <see cref="IAddWindow.Show"/>.</summary>
    public static IAddWindow Create() => new AddWindowView();
}

/// <summary>The Add window, as Shell uses it.</summary>
public interface IAddWindow
{
    /// <summary>Shows the Add window, with the draft if there is one.</summary>
    void Show();
}
