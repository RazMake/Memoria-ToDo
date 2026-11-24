using System.Windows;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>A mentioned contact's details, to show while the pointer rests on the mention.</summary>
public static class ContactCard
{
    /// <summary>
    /// The card for the contact <paramref name="address"/> names, as Contacts' <c>ContactOf</c> finds it, or null when it finds nobody.
    /// The card shows the common fields, and every field while Ctrl is held, switching as Ctrl goes down and up;
    /// it shows the contact as it is now, and nothing on it takes the focus or a click.
    /// Use it on the UI thread.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static FrameworkElement? For(IContacts contacts, ITheme theme, string address)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(address);
        if (ContactCardContent.Of(contacts, address, allDetails: false) is not { } content)
        {
            return null;
        }

        content.AllDetails = ContactCardView.IsCtrlHeld();
        return new ContactCardView(content, theme);
    }
}
