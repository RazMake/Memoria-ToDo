using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>What the Contacts window refuses when it is created, and that creating it makes no window. These run with no screen.</summary>
[Unit]
public class ContactsWindowCreateTests
{
    private readonly ContactServices _services = new();

    private IContactsWindow Create(
        bool contacts = true, bool theme = true, bool time = true, bool closeKeys = true, bool saveKeys = true, bool pointer = true) =>
        ContactsWindow.Create(
            contacts ? _services.Contacts : null!, theme ? _services.Theme : null!, time ? _services.Time : null!,
            closeKeys ? _services.CloseWindowKeys : null!, saveKeys ? _services.SaveKeys : null!, pointer ? _services.Pointer : null!);

    [Fact]
    public void Create_rejects_missing_Contacts_by_name() =>
        Assert.Equal("contacts", Assert.Throws<ArgumentNullException>(() => Create(contacts: false)).ParamName);

    [Fact]
    public void Create_rejects_a_missing_theme_by_name() =>
        Assert.Equal("theme", Assert.Throws<ArgumentNullException>(() => Create(theme: false)).ParamName);

    [Fact]
    public void Create_rejects_a_missing_clock_by_name() =>
        Assert.Equal("time", Assert.Throws<ArgumentNullException>(() => Create(time: false)).ParamName);

    [Fact]
    public void Create_rejects_missing_Close_window_keys_by_name() =>
        Assert.Equal("closeWindowKeys", Assert.Throws<ArgumentNullException>(() => Create(closeKeys: false)).ParamName);

    [Fact]
    public void Create_rejects_missing_Save_keys_by_name() =>
        Assert.Equal("saveKeys", Assert.Throws<ArgumentNullException>(() => Create(saveKeys: false)).ParamName);

    [Fact]
    public void Create_rejects_a_missing_drag_pointer_by_name() =>
        Assert.Equal("pointer", Assert.Throws<ArgumentNullException>(() => Create(pointer: false)).ParamName);

    [WpfFact]
    public void A_refused_Create_makes_no_window()
    {
        using var windows = new OpenWindows();

        Assert.Throws<ArgumentNullException>(() => Create(pointer: false));

        Assert.Empty(windows.Visible());
    }

    [WpfFact]
    public void A_new_window_is_not_open_and_shows_nothing_until_Show()
    {
        using var windows = new OpenWindows();

        var window = Create();

        Assert.False(window.IsOpen);
        Assert.Empty(windows.Visible());
    }
}
