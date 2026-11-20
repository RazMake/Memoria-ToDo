namespace Memoria.ToDo.Desktop.Logs;

/// <summary>The Logs window, as Shell uses it.</summary>
public interface ILogsWindow
{
    /// <summary>Opens the window and loads the log, or brings an open window to the front.</summary>
    void Show();
}
