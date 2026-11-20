using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// How Task View shows a title's or description's mentions: with the contact's current text, or the stored text when Contacts
/// has none. Which text is a mention is Rendering's, tested there. Search matches the task as saved, not as shown.
/// </summary>
[Unit]
public class LiveMentionsTests
{
    private const string AnaOld = "[Old Name](contact:ana \"{fullName}\")";

    private readonly ListTasks _services = new();

    public LiveMentionsTests() =>
        _services.UseContacts(["{fullName}", "{firstName}"], ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana")), ContactFiles.Person("bob", "Bob Stan"));

    [Fact]
    public void A_title_shows_the_contacts_current_text_for_its_mention()
    {
        var shown = LiveMentions.Of(_services.Contacts, $"Call {AnaOld} today", title: true);

        Assert.Equal("Call [Ana Pop](contact:ana \"{fullName}\") today", shown);
    }

    [Fact]
    public void A_description_shows_the_contacts_current_text_for_its_mention()
    {
        var shown = LiveMentions.Of(_services.Contacts, $"Ask {AnaOld}.\n\nThen {AnaOld}.", title: false);

        Assert.Equal("Ask [Ana Pop](contact:ana \"{fullName}\").\n\nThen [Ana Pop](contact:ana \"{fullName}\").", shown);
    }

    [Fact]
    public void Each_mention_shows_the_text_of_its_own_template()
    {
        var shown = LiveMentions.Of(_services.Contacts, "[x](contact:ana \"{firstName}\") and [y](contact:bob \"{fullName}\")", title: true);

        Assert.Equal("[Ana](contact:ana \"{firstName}\") and [Bob Stan](contact:bob \"{fullName}\")", shown);
    }

    [Fact]
    public void A_mention_of_a_contact_Contacts_does_not_find_keeps_its_stored_text()
    {
        var shown = LiveMentions.Of(_services.Contacts, "Call [Old Name](contact:gone \"{fullName}\") today", title: true);

        Assert.Equal("Call [Old Name](contact:gone \"{fullName}\") today", shown);
    }

    [Fact]
    public void A_text_with_no_mention_is_unchanged()
    {
        const string text = "Call **Ana** [site](https://example.com) today";

        Assert.Equal(text, LiveMentions.Of(_services.Contacts, text, title: true));
        Assert.Equal(text, LiveMentions.Of(_services.Contacts, text, title: false));
    }

    [Fact]
    public void Shows_the_text_a_contact_has_after_it_changes()
    {
        _services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop"));

        Assert.Equal("[Ana Maria Pop](contact:ana \"{fullName}\")", LiveMentions.Of(_services.Contacts, AnaOld, title: true));
    }

    [Fact]
    public void Search_matches_a_mentions_stored_text_and_not_its_live_text()
    {
        _services.Add($"Call {AnaOld} today");
        var filter = new ListFilter(_services.Contacts);

        filter.SearchText = "Old Name";
        var byStored = filter.Matching(_services.Tasks);
        filter.SearchText = "Ana Pop";
        var byLive = filter.Matching(_services.Tasks);

        Assert.Single(byStored);
        Assert.Empty(byLive);
    }
}
