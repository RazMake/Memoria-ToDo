namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The Contacts window, as Shell uses it.</summary>
public interface IContactsWindow
{
    /// <summary>Opens the window, or brings it forward, empties its search, and puts the cursor there.</summary>
    void Show();

    /// <summary>
    /// Closes the window as its close button does, first asking Save, Discard, or Cancel when a form
    /// has changes. True when the window is closed, or wasn't open; false when the user cancels,
    /// the save fails, or the prompt already shows.
    /// </summary>
    bool Close();

    /// <summary>Whether the window is open now.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Whether the clone has contact files, as Contacts' HasContactFiles says. Shell offers the
    /// tray item and the shortcut only while this is true.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Raised on the UI thread when <see cref="IsAvailable"/> changes.</summary>
    event EventHandler? AvailableChanged;
}
