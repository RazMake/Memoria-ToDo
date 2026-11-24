using System.Windows.Threading;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.ContactList.Tests.ContactsWindow;

/// <summary>
/// <c>IsAvailable</c> and <c>AvailableChanged</c>, which follow Contacts' HasContactFiles and its Changed notice from creation, with
/// the window never opened. The notice comes on the thread of the call that changed Contacts, and the window raises its own on the UI
/// thread: each test runs on the UI thread, changes Contacts on another where it matters, and lets the dispatcher catch up.
/// </summary>
[Unit]
public class ContactsWindowAvailabilityTests
{
    private readonly ContactServices _services = new();
    private readonly List<int> _raisedOn = [];

    private IContactsWindow Create()
    {
        var window = _services.CreateWindow();
        window.AvailableChanged += (_, _) => _raisedOn.Add(Environment.CurrentManagedThreadId);
        return window;
    }

    [WpfFact]
    public void Is_not_available_with_no_clone_selected()
    {
        Assert.False(Create().IsAvailable);
    }

    [WpfFact]
    public void Is_available_when_the_clone_has_contact_files_from_creation()
    {
        _services.SeedTypical();
        _services.Load();

        Assert.True(Create().IsAvailable);
    }

    [WpfFact]
    public void Is_available_once_Contacts_has_files_when_asked()
    {
        var window = Create();
        _services.SeedTypical();

        _services.Load();

        Assert.True(window.IsAvailable);
    }

    [WpfFact]
    public async Task Raises_AvailableChanged_on_the_UI_thread_when_files_appear_through_a_change_on_another_thread()
    {
        var window = Create();
        _services.SeedTypical();
        var uiThread = Environment.CurrentManagedThreadId;

        await Task.Run(_services.Load);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Assert.Equal([uiThread], _raisedOn);
        Assert.True(window.IsAvailable);
    }

    [WpfFact]
    public async Task Raises_AvailableChanged_when_the_last_contact_file_goes()
    {
        _services.SeedTypical();
        _services.Load();
        var window = Create();
        foreach (var file in _services.Disk.AllFilesUnder(_services.Clone).ToList())
        {
            _services.Disk.DeleteFile(file);
        }

        _services.Load();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Assert.Single(_raisedOn);
        Assert.False(window.IsAvailable);
    }

    [WpfFact]
    public async Task Raises_nothing_when_Contacts_changes_and_the_answer_stays()
    {
        _services.SeedTypical();
        _services.Load();
        var window = Create();

        _services.Contacts.Save("team", _services.Member("team", "bob") with { Title = "Lead" });
        _services.Load();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Assert.Empty(_raisedOn);
        Assert.True(window.IsAvailable);
    }

    [WpfFact]
    public async Task Raises_once_for_one_change_of_the_answer_however_many_notices_follow()
    {
        var window = Create();
        _services.SeedTypical();

        _services.Load();
        _services.Contacts.Save("team", _services.Member("team", "bob") with { Title = "Lead" });
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

        Assert.Single(_raisedOn);
        Assert.True(window.IsAvailable);
    }
}
