using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The "@" list starts on the template used last: choosing a contact, by Enter or a click, inserts the mention and then has Contacts
/// move that template to the top of the synced templates file. Nothing else moves a template. A move that fails keeps the mention,
/// says why on the editor's message line, and is logged. Tested over the real Contacts on fake files, so the templates file is
/// what the assertions read.
/// </summary>
[Unit]
public class MentionTemplateFirstTests
{
    private const string WithTitle = "{fullName} ({title})";
    private const string FirstName = "{firstName}";
    private const string FullName = "{fullName}";

    private readonly EditorServices _services = new();
    private readonly InMemoryLogSink _log = new();
    private readonly List<string> _messages = [];
    private readonly ChoiceList _list = new();
    private readonly PopupLists _popups;
    private readonly FakeTextField _field = new();
    private readonly MentionPicking _picking;

    public MentionTemplateFirstTests()
    {
        _popups = new PopupLists(new ChoiceList(), _list);
        _picking = new MentionPicking(_field, _list, _popups, _services.Contacts, report: _messages.Add, logger: _log.CreateLogger());
    }

    private string TemplatesPath => TestContactStore.PathOf(_services.Clone, TestContactStore.TemplatesFile);

    private List<string> TemplatesInFile() => ContactFiles.TemplatesIn(_services.Files.ReadFile(TemplatesPath));

    private void UseContacts(params string[] templates) =>
        _services.UseContacts(templates,
            ContactFiles.Person("ana", "Ana Pop", ("firstName", "Ana"), ("title", "Engineer")),
            ContactFiles.Person("bob", "Bob Stan", ("firstName", "Bob")));

    private void NextTemplate(int times)
    {
        for (var i = 0; i < times; i++)
        {
            _picking.NextTemplate();
        }
    }

    [Fact]
    public void Choosing_with_Enter_moves_the_chosen_template_to_the_top_of_the_templates_file_and_keeps_the_others_in_order()
    {
        UseContacts(WithTitle, FirstName, FullName);
        _field.Type("@");
        NextTemplate(2);
        _popups.MoveDown();

        _popups.ChooseMovedOrClose();

        Assert.Equal("[Ana Pop](contact:ana \"{fullName}\")", _field.Text);
        Assert.Equal([FullName, WithTitle, FirstName], TemplatesInFile());
        Assert.Equal([FullName, WithTitle, FirstName], _services.Contacts.MentionTemplates);
    }

    [Fact]
    public void Moves_the_template_the_mention_used_even_when_the_templates_change_while_it_is_inserted()
    {
        UseContacts(WithTitle, FirstName, FullName);
        _field.Type("@");
        NextTemplate(1);
        var reloaded = false;
        _field.Changed += () =>
        {
            if (reloaded || !_field.Text.Contains("contact:", StringComparison.Ordinal))
            {
                return;
            }

            reloaded = true;
            _services.Contacts.RepositoryOf().Update(_services.Clone,
                () => _services.Files.Seed(_services.Clone, TestContactStore.TemplatesFile, ContactFiles.Templates([WithTitle, FullName, FirstName])));
        };

        _list.Choose(0);

        Assert.True(reloaded);
        Assert.Equal("[Ana](contact:ana \"{firstName}\")", _field.Text);
        Assert.Equal([FirstName, WithTitle, FullName], TemplatesInFile());
        Assert.Equal([FirstName, WithTitle, FullName], _services.Contacts.MentionTemplates);
    }

    [Fact]
    public void Choosing_with_a_click_on_a_row_moves_the_chosen_template_to_the_top_of_the_templates_file()
    {
        UseContacts(WithTitle, FirstName, FullName);
        _field.Type("@");
        NextTemplate(1);

        _list.Choose(1);

        Assert.Equal("[Bob](contact:bob \"{firstName}\")", _field.Text);
        Assert.Equal([FirstName, WithTitle, FullName], TemplatesInFile());
        Assert.Equal([FirstName, WithTitle, FullName], _services.Contacts.MentionTemplates);
    }

    [Fact]
    public void Choosing_with_the_first_template_leaves_the_templates_file_in_order()
    {
        UseContacts(WithTitle, FirstName);
        _field.Type("@");

        _list.Choose(0);

        Assert.Equal([WithTitle, FirstName], TemplatesInFile());
        Assert.DoesNotContain(_log.Events, entry => entry.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void The_next_opening_starts_on_the_template_used_last()
    {
        UseContacts(WithTitle, FirstName, FullName);
        _field.Type("@");
        NextTemplate(1);
        _list.Choose(0);

        _field.Type(" @");

        Assert.True(_list.IsOpen);
        Assert.Equal("Ana", _picking.Header);
    }

    [Fact]
    public void After_a_choice_Tab_goes_through_the_other_templates_in_the_files_new_order()
    {
        UseContacts(WithTitle, FirstName, FullName);
        _field.Type("@");
        NextTemplate(2);
        _list.Choose(0);
        _field.Type(" @");

        Assert.Equal("Ana Pop", _picking.Header);
        _picking.NextTemplate();
        Assert.Equal("Ana Pop (Engineer)", _picking.Header);
        _picking.NextTemplate();
        Assert.Equal("Ana", _picking.Header);
        _picking.NextTemplate();
        Assert.Equal("Ana Pop", _picking.Header);
    }

    [Fact]
    public void Undo_takes_the_mention_back_but_the_template_stays_first()
    {
        UseContacts(WithTitle, FirstName);
        _field.Type("see @bo");
        NextTemplate(1);
        _list.Choose(0);

        _field.Undo();

        Assert.Equal("see @bo", _field.Text);
        Assert.Equal([FirstName, WithTitle], TemplatesInFile());
    }

    [Fact]
    public void Tab_and_a_click_on_the_header_do_not_move_a_template()
    {
        UseContacts(WithTitle, FirstName, FullName);
        _field.Type("@");

        NextTemplate(2);

        Assert.Equal([WithTitle, FirstName, FullName], TemplatesInFile());
        Assert.Equal([WithTitle, FirstName, FullName], _services.Contacts.MentionTemplates);
    }

    [Fact]
    public void Closing_the_list_without_choosing_does_not_move_a_template()
    {
        UseContacts(WithTitle, FirstName);
        _field.Type("@");
        NextTemplate(1);
        _popups.CloseOpen();
        _field.Type(" @");
        NextTemplate(1);
        _popups.ChooseMovedOrClose();
        _field.LoseFocus();

        Assert.Equal([WithTitle, FirstName], TemplatesInFile());
        Assert.Equal([WithTitle, FirstName], _services.Contacts.MentionTemplates);
    }

    [Fact]
    public void A_move_that_fails_because_the_file_cannot_be_read_now_keeps_the_mention_as_one_undo_step_and_says_why()
    {
        UseContacts(WithTitle, FirstName);
        _field.Type("see @bo");
        NextTemplate(1);
        _services.Files.WriteFile(TemplatesPath, Encoding.UTF8.GetBytes("{ not json"));

        _list.Choose(0);

        Assert.Equal("see [Bob](contact:bob \"{firstName}\")", _field.Text);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(_messages)));
        Assert.NotEmpty(_log.Events.At(LogEventLevel.Error));
        Assert.Equal([WithTitle, FirstName], _services.Contacts.MentionTemplates);

        _field.Undo();

        Assert.Equal("see @bo", _field.Text);
    }

    [Fact]
    public void A_move_the_file_kept_before_a_notice_failed_leaves_the_template_first_keeps_the_mention_and_says_so_on_the_message_line()
    {
        UseContacts(WithTitle, FirstName);
        _field.Type("@");
        NextTemplate(1);
        _services.Contacts.Changed += (_, _) => throw new IOException("Changed failed.");

        _list.Choose(0);

        Assert.Equal("[Ana](contact:ana \"{firstName}\")", _field.Text);
        Assert.Equal([FirstName, WithTitle], TemplatesInFile());
        Assert.Equal(["Saved, but the app couldn't finish updating: Changed failed."], _messages);
        Assert.Single(_log.Events.At(LogEventLevel.Error));
    }

    [Fact]
    public void A_move_that_fails_because_the_file_cannot_be_written_keeps_the_mention_and_says_why()
    {
        UseContacts(WithTitle, FirstName);
        _field.Type("@");
        NextTemplate(1);
        _services.Files.FailWritesUnder(_services.Clone);

        _list.Choose(0);

        Assert.Equal("[Ana](contact:ana \"{firstName}\")", _field.Text);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(_messages)));
        Assert.NotEmpty(_log.Events.At(LogEventLevel.Error));
        Assert.Equal([WithTitle, FirstName], _services.Contacts.MentionTemplates);
        Assert.Equal([WithTitle, FirstName], TemplatesInFile());
    }
}
