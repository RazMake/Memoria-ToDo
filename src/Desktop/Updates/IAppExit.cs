namespace Memoria.ToDo.Desktop.Updates;

/// <summary>What Updates needs from Shell to restart the app. Shell implements it.</summary>
public interface IAppExit
{
    /// <summary>Whether any of the app's windows is open.</summary>
    bool AnyWindowOpen { get; }

    /// <summary>
    /// Closes Task View as its close button does. False when it stays open, such as when the user cancels
    /// its prompt about a changed edit. The other windows close with the app at <see cref="End"/>.
    /// </summary>
    bool CloseWindows();

    /// <summary>Ends the app through its normal exit, which calls <see cref="IUpdateLifetime.StopAsync"/>.</summary>
    void End();
}
