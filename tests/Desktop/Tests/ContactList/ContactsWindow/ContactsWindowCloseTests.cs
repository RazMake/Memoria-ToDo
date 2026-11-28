using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>Closing the Contacts window when it isn't open. It builds no window, since Show makes it, so these run with no screen.</summary>
[Unit]
public class ContactsWindowCloseTests
{
    [Fact]
    public void Close_when_the_window_is_not_open_returns_true()
    {
        var window = ContactServices.Typical().CreateWindow();

        Assert.True(window.Close());
    }

    [Fact]
    public void Close_when_the_window_is_not_open_leaves_it_not_open()
    {
        var window = ContactServices.Typical().CreateWindow();

        window.Close();

        Assert.False(window.IsOpen);
    }
}
