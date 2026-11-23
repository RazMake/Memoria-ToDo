using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Serilog;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>Creates the Contacts window.</summary>
public static class ContactsWindow
{
    /// <summary>Creates the Contacts window. It opens only on <see cref="IContactsWindow.Show"/>.</summary>
    public static IContactsWindow Create(IContacts contacts, ITheme theme, TimeProvider time,
        CloseWindowKeys closeWindowKeys, SaveKeys saveKeys, IDragPointer pointer, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(closeWindowKeys);
        ArgumentNullException.ThrowIfNull(saveKeys);
        ArgumentNullException.ThrowIfNull(pointer);
        ArgumentNullException.ThrowIfNull(logger);
        return new Opener(contacts, time, closeWindowKeys, saveKeys, pointer, logger);
    }

    private sealed class Opener : IContactsWindow
    {
        private readonly SingleWindow _window;
        private readonly ContactsAvailability _availability;
        private ContactsMainWindow? _current;

        public Opener(IContacts contacts, TimeProvider time, CloseWindowKeys closeWindowKeys, SaveKeys saveKeys, IDragPointer pointer, ILogger logger)
        {
            _availability = new ContactsAvailability(contacts);
            _window = new SingleWindow(() =>
            {
                var window = new ContactsMainWindow(contacts, time, saveKeys, pointer, logger);
                closeWindowKeys.Attach(window);
                _current = window;
                window.Closed += (_, _) => _current = null;
                return window;
            }, logger);
        }

        public event EventHandler? AvailableChanged
        {
            add => _availability.Changed += value;
            remove => _availability.Changed -= value;
        }

        public bool IsOpen => _window.IsOpen;

        public bool IsAvailable => _availability.IsAvailable;

        public void Show()
        {
            var wasOpen = _window.IsOpen;
            _window.Show();
            if (wasOpen)
            {
                _current?.StartSearch();
            }
        }

        public bool Close() => _current is not { } window || !_window.IsOpen || window.TryClose();
    }
}
