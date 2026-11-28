namespace Memoria.ToDo.Desktop.App;

/// <summary>The app as the MCP bridge sees it: whether it runs, and a way to start it.</summary>
internal interface IBridgeApp
{
    /// <summary>Whether the app's one-instance signal is claimed. Looking never signals the app.</summary>
    bool IsRunning();

    /// <summary>Starts the app as a start at sign-in does, and doesn't wait for it. Throws when it can't be started.</summary>
    void Start();
}
