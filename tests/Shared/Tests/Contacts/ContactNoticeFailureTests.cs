using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>
/// When Changed or the local save report fails after Save, Delete, MoveGroup, or MoveTemplateFirst saved the change, Contacts still raises
/// both, keeps the change, and throws NoticeFailedException of the contact, holding every failure in order, even one.
/// A failure of the write is thrown as before, and a reload never throws NoticeFailedException.
/// </summary>
[Unit]
public sealed class ContactNoticeFailureTests
{
    private readonly ContactsFixture f = new();

    public ContactNoticeFailureTests()
    {
        f.SeedGroup("team", 1000, ContactFiles.Person("1", "Ana"), ContactFiles.Person("2", "Bo"));
        f.SeedGroup("friends", 2000, ContactFiles.Person("4", "Di"));
        f.SeedGroup("club", 3000, ContactFiles.Person("5", "Ed"));
        f.Seed(ContactsFixture.TemplatesFile, ContactFiles.Templates("{fullName}", "{firstName}", "{title}"));
        f.SeedDataTypes();
        f.Load();
        f.ForgetWrites();
    }

    /// <summary>A call that saves a change, and the ID of the contact it saves, or null when it saves none.</summary>
    public sealed record Call(string Name, string? SavedId, Action<IContacts> Run);

    public static IEnumerable<TheoryDataRow<Call>> Calls() =>
    [
        Row("Save a contact", "1", c => c.Save("team", new Contact("1", "Ana Popa"))),
        Row("Save with a change", "1", c => c.Save("team", "1", _ => new Contact("1", "Ana Popa"))),
        Row("Delete", null, c => c.Delete("team", "2")),
        Row("MoveGroup", null, c => c.MoveGroup("club", "team", "friends")),
        Row("MoveTemplateFirst", null, c => c.MoveTemplateFirst("{title}")),
    ];

    private static TheoryDataRow<Call> Row(string name, string? savedId, Action<IContacts> run) =>
        new(new Call(name, savedId, run)) { TestDisplayName = name };

    private List<string> NoticesSince(int count) => [.. f.Notices.Skip(count)];

    private Exception FailChanged()
    {
        var failure = new IOException("Changed failed.");
        f.Contacts.Changed += (_, _) => throw failure;
        return failure;
    }

    private Exception FailReport()
    {
        var failure = new InvalidOperationException("Report failed.");
        f.Repository.LocalSaved += (_, _) => throw failure;
        return failure;
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_NoticeFailedException_of_the_contact_holding_the_one_failure_when_a_Changed_handler_fails(Call call)
    {
        var failure = FailChanged();

        var thrown = Assert.Throws<NoticeFailedException<Contact>>(() => call.Run(f.Contacts));

        Assert.Equal([failure], thrown.InnerExceptions);
        Assert.Same(failure, thrown.InnerException);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_NoticeFailedException_of_the_contact_holding_the_one_failure_when_the_local_save_report_fails(Call call)
    {
        var failure = FailReport();

        var thrown = Assert.Throws<NoticeFailedException<Contact>>(() => call.Run(f.Contacts));

        Assert.Equal([failure], thrown.InnerExceptions);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_NoticeFailedException_of_the_contact_holding_Changed_s_failure_then_the_report_s_when_both_fail(Call call)
    {
        var changed = FailChanged();
        var report = FailReport();

        var thrown = Assert.Throws<NoticeFailedException<Contact>>(() => call.Run(f.Contacts));

        Assert.Equal([changed, report], thrown.InnerExceptions);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Keeps_the_change_and_still_reports_the_local_save_when_a_Changed_handler_fails(Call call)
    {
        var before = f.Notices.Count;
        FailChanged();

        Assert.Throws<NoticeFailedException<Contact>>(() => call.Run(f.Contacts));

        Assert.Equal(["Changed", "LocalSaved"], NoticesSince(before));
        Assert.NotEmpty(f.Written);
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Carries_the_contact_as_saved_for_a_save_and_null_for_the_other_changes(Call call)
    {
        FailReport();

        var thrown = Assert.Throws<NoticeFailedException<Contact>>(() => call.Run(f.Contacts));

        if (call.SavedId is null)
        {
            Assert.Null(thrown.Saved);
        }
        else
        {
            var saved = Assert.IsType<Contact>(thrown.Saved);
            Assert.Equal(call.SavedId, saved.Id);
            Assert.Equal("Ana Popa", saved.FullName);
        }
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void Throws_the_write_s_failure_as_it_is_and_raises_no_notice_when_the_write_fails_even_though_notice_handlers_would_fail(Call call)
    {
        var failure = new IOException("disk full");
        var before = f.Notices.Count;
        FailChanged();
        FailReport();
        f.Files.FailWritesAfter(0, failure);

        var thrown = Assert.Throws<IOException>(() => call.Run(f.Contacts));

        Assert.Same(failure, thrown);
        Assert.Equal(before, f.Notices.Count);
    }

    [Fact]
    public void Throws_a_reload_s_notice_failure_as_it_is_not_as_NoticeFailedException()
    {
        var failure = FailChanged();

        var thrown = Assert.Throws<IOException>(() => f.Repository.Update(f.Clone, () => { }));

        Assert.Same(failure, thrown);
    }
}
