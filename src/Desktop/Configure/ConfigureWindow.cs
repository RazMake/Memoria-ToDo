namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Creates the Configure window.</summary>
public static class ConfigureWindow
{
    /// <summary>Creates Configure. The window opens only on <see cref="IConfigureWindow.Show"/>.</summary>
    public static IConfigureWindow Create() => new ConfigureShow();
}

/// <summary>The Configure window, as Shell uses it.</summary>
public interface IConfigureWindow
{
    /// <summary>Opens the window, or brings it forward.</summary>
    void Show();
}
