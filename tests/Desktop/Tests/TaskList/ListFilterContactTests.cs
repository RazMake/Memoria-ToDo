using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What a search word that starts with "@" finds: when the rest of it finds one contact, through Contacts' alias-then-ID lookup, the tasks whose
/// title or description mention that contact, whatever the mention's stored text; otherwise a plain word. Every word must still match, and the search
/// matches again when contacts change. The words without "@" are Tasks' search and are tested there.
/// </summary>
[Unit]
public class ListFilterContactTests
{
    private const string Ana = "[Old Name](contact:ana \"{fullName}\")";
    private const string AnaUpper = "[Old Name](contact:ANA \"{fullName}\")";
    private const string Bob = "[Bob Stan](contact:bob \"{fullName}\")";
    private const string AnaTwin = "[Ana Pop](contact:ana2 \"{fullName}\")";

    private readonly ListTasks _tasks = new();
    private readonly ListFilter _filter;
    private int _changed;

    public ListFilterContactTests()
    {
        _tasks.UseContacts(
            ["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap")),
            ContactFiles.Person("ana2", "Ana Pop", ("alias", "anap2")),
            ContactFiles.Person("bob", "Bob Stan"),
            ContactFiles.Person("cy", "Cy One", ("alias", "twin")),
            ContactFiles.Person("dee", "Dee Two", ("alias", "twin")));
        _filter = new ListFilter(_tasks.Contacts);
        _filter.Changed += (_, _) => _changed++;
    }

    private IReadOnlyList<string> Titles(string search)
    {
        _filter.SearchText = search;
        return [.. _filter.Matching(_tasks.Tasks).Select(task => task.Title)];
    }

    [Fact]
    public void An_at_word_with_an_alias_finds_the_tasks_with_a_mention_of_that_contact_in_the_title()
    {
        _tasks.Add($"Call {Ana} today");
        _tasks.Add($"Call {Bob} today");
        _tasks.Add("Call nobody");

        Assert.Equal([$"Call {Ana} today"], Titles("@anap"));
    }

    [Fact]
    public void An_at_word_finds_a_mention_in_the_description()
    {
        _tasks.Add("Plan", $"Ask {Ana} first");
        _tasks.Add("Other", $"Ask {Bob} first");

        Assert.Equal(["Plan"], Titles("@anap"));
    }

    [Fact]
    public void An_at_word_finds_a_mention_whatever_its_stored_text_and_template()
    {
        _tasks.Add("Call [Somebody Else](contact:ana \"{firstName}\") today");

        Assert.Single(Titles("@anap"));
    }

    [Fact]
    public void An_at_word_ignores_the_case_of_the_alias()
    {
        _tasks.Add($"Call {Ana}");

        Assert.Single(Titles("@ANAP"));
    }

    [Fact]
    public void An_at_word_with_an_id_finds_the_contact_when_no_alias_matches()
    {
        _tasks.Add($"Call {Ana}");
        _tasks.Add($"Call {Bob}");

        Assert.Equal([$"Call {Ana}"], Titles("@ana"));
        Assert.Equal([$"Call {Bob}"], Titles("@bob"));
    }

    [Fact]
    public void An_at_word_ignores_the_case_of_the_id()
    {
        _tasks.Add($"Call {Bob}");

        Assert.Single(Titles("@BOB"));
    }

    [Fact]
    public void An_at_word_finds_a_mention_whose_address_holds_the_id_in_another_case()
    {
        _tasks.Add($"Call {AnaUpper}");

        Assert.Single(Titles("@anap"));
    }

    [Fact]
    public void An_at_word_does_not_find_the_same_text_typed_without_a_mention()
    {
        _tasks.Add("Call @anap today");
        _tasks.Add("Call Ana Pop today");

        Assert.Empty(Titles("@anap"));
    }

    [Fact]
    public void An_at_word_does_not_find_a_mention_of_another_contact_with_the_same_text()
    {
        _tasks.Add($"Call {AnaTwin}");
        _tasks.Add($"Call {Ana}");

        Assert.Equal([$"Call {Ana}"], Titles("@anap"));
        Assert.Equal([$"Call {AnaTwin}"], Titles("@anap2"));
    }

    [Fact]
    public void An_at_word_finds_a_task_that_mentions_the_contact_more_than_once_once()
    {
        _tasks.Add($"Call {Ana}", $"Ask {Ana}");

        Assert.Single(Titles("@anap"));
    }

    [Fact]
    public void An_at_word_matches_nothing_when_no_task_mentions_the_contact()
    {
        _tasks.Add($"Call {Bob}");

        Assert.Empty(Titles("@anap"));
    }

    [Fact]
    public void An_at_word_that_finds_no_contact_matches_as_a_plain_word()
    {
        _tasks.Add("Mail @nobody today");
        _tasks.Add("Mail somebody");

        Assert.Equal(["Mail @nobody today"], Titles("@nobody"));
    }

    [Fact]
    public void An_at_word_that_finds_no_contact_matches_a_plain_word_in_the_description_too()
    {
        _tasks.Add("Mail", "write to @nobody");
        _tasks.Add("Other");

        Assert.Equal(["Mail"], Titles("@nobody"));
    }

    [Fact]
    public void A_bare_at_sign_matches_as_a_plain_word()
    {
        _tasks.Add("Mail a@b.example");
        _tasks.Add($"Call {Ana}");

        Assert.Equal(["Mail a@b.example"], Titles("@"));
    }

    [Fact]
    public void An_at_word_naming_two_contacts_matches_as_a_plain_word()
    {
        _tasks.Add("Mail @twin today");
        _tasks.Add("Call [Cy One](contact:cy \"{fullName}\")");
        _tasks.Add("Call [Dee Two](contact:dee \"{fullName}\")");

        Assert.Equal(["Mail @twin today"], Titles("@twin"));
    }

    [Fact]
    public void An_at_word_matches_as_a_plain_word_while_Contacts_has_no_repository()
    {
        var empty = new ListTasks();
        empty.Add("Mail @anap today");
        var filter = new ListFilter(empty.Contacts) { SearchText = "@anap" };

        Assert.Equal(["Mail @anap today"], filter.Matching(empty.Tasks).Select(task => task.Title));
    }

    [Fact]
    public void An_at_word_and_a_plain_word_must_both_match()
    {
        _tasks.Add($"Budget call with {Ana}");
        _tasks.Add($"Lunch with {Ana}");
        _tasks.Add($"Budget call with {Bob}");

        Assert.Equal([$"Budget call with {Ana}"], Titles("@anap budget"));
    }

    [Fact]
    public void The_order_of_an_at_word_and_a_plain_word_does_not_matter()
    {
        _tasks.Add($"Budget call with {Ana}");
        _tasks.Add($"Lunch with {Ana}");

        Assert.Equal(Titles("@anap budget"), Titles("budget @anap"));
        Assert.Single(Titles("budget @anap"));
    }

    [Fact]
    public void The_mention_and_the_plain_word_can_be_in_the_title_and_the_description()
    {
        _tasks.Add($"Call {Ana}", "about the budget");
        _tasks.Add("Budget", $"Ask {Ana}");
        _tasks.Add("Budget only", "nothing");

        Assert.Equal(["Budget", $"Call {Ana}"], Titles("@anap budget"));
    }

    [Fact]
    public void A_plain_word_matches_the_stored_text_of_a_mention_beside_an_at_word()
    {
        _tasks.Add($"Call {Ana}");
        _tasks.Add($"Call {Bob}");

        Assert.Equal([$"Call {Ana}"], Titles("@anap old"));
    }

    [Fact]
    public void Two_at_words_need_a_mention_of_each_contact()
    {
        _tasks.Add($"Meet {Ana}", $"and {Bob}");
        _tasks.Add($"Meet {Ana}");
        _tasks.Add($"Meet {Bob}");

        Assert.Equal([$"Meet {Ana}"], Titles("@anap @bob"));
    }

    [Fact]
    public void An_at_word_with_an_unknown_name_and_a_known_one_needs_the_plain_text_and_the_mention()
    {
        _tasks.Add($"Mail @nobody and {Ana}");
        _tasks.Add($"Call {Ana}");
        _tasks.Add("Mail @nobody");

        Assert.Equal([$"Mail @nobody and {Ana}"], Titles("@nobody @anap"));
    }

    [Fact]
    public void An_empty_search_still_shows_every_task()
    {
        _tasks.Add($"Call {Ana}");
        _tasks.Add("Other");

        Assert.Equal(2, Titles("").Count);
    }

    [Fact]
    public void Tells_the_list_to_refresh_when_contacts_change()
    {
        _tasks.Add($"Call {Ana}");
        _filter.SearchText = "@anap";
        var before = _changed;

        _tasks.ChangeContacts(ContactFiles.Person("ana", "Ana Pop", ("alias", "anap")));

        Assert.True(_changed > before, "The filter didn't tell the list to refresh.");
    }

    [Fact]
    public void An_alias_that_changes_makes_the_old_one_a_plain_word_and_the_new_one_a_contact()
    {
        _tasks.Add($"Call {Ana}");
        _tasks.Add("Mail @anap and @anita");
        _filter.SearchText = "@anap";
        Assert.Equal([$"Call {Ana}"], Titles("@anap"));

        _tasks.ChangeContacts(ContactFiles.Person("ana", "Ana Pop", ("alias", "anita")));

        Assert.Equal(["Mail @anap and @anita"], Titles("@anap"));
        Assert.Equal([$"Call {Ana}"], Titles("@anita"));
    }

    [Fact]
    public void A_word_that_found_no_contact_finds_one_after_the_contact_is_added()
    {
        _tasks.Add($"Call {Ana}");
        _tasks.Add("Mail @newbie");
        Assert.Equal(["Mail @newbie"], Titles("@newbie"));

        _tasks.ChangeContacts(ContactFiles.Person("ana", "Ana Pop", ("alias", "newbie")));

        Assert.Equal([$"Call {Ana}"], Titles("@newbie"));
    }
}
