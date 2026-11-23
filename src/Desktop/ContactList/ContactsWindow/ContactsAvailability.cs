using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// Whether the clone has contact files, as Contacts' HasContactFiles says, and the notice when that changes. Made on the UI thread;
/// Contacts' Changed comes on any thread, so each notice is checked on the UI thread, and the event is raised there.
/// </summary>
internal sealed class ContactsAvailability
{
    private readonly IContacts _contacts;
    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private bool _reported;

    public ContactsAvailability(IContacts contacts)
    {
        _contacts = contacts;
        _reported = contacts.HasContactFiles;
        contacts.Changed += (_, _) => _dispatcher.BeginInvoke(Check);
    }

    public bool IsAvailable => _contacts.HasContactFiles;

    /// <summary>Raised on the UI thread when <see cref="IsAvailable"/> differs from what it was when last raised.</summary>
    public event EventHandler? Changed;

    private void Check()
    {
        var now = _contacts.HasContactFiles;
        if (now != _reported)
        {
            _reported = now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
