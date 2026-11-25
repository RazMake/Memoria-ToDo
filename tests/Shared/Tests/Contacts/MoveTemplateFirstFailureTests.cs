using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A move that can't happen, or whose write fails, leaves the file and the list as they were.</summary>
[Unit]
public sealed class MoveTemplateFirstFailureTests
{
    private const string File = ContactsFixture.TemplatesFile;

    private readonly ContactsFixture f = new();

    public MoveTemplateFirstFailureTests()
    {
        f.Seed(File, ContactFiles.Templates("{fullName}", "{firstName}", "{title}"));
        f.LoadWithContacts();
    }

    [Fact]
    public void Rejects_a_null_template_before_writing()
    {
        Assert.Throws<ArgumentNullException>(() => f.Contacts.MoveTemplateFirst(null!));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Needs_a_clone()
    {
        f.Repository.Select(null);

        Assert.Throws<NoRepositoryFolderException>(() => f.Contacts.MoveTemplateFirst("{firstName}"));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Rejects_a_move_made_inside_a_save_change_which_holds_the_repository_lock_before_writing()
    {
        Assert.Throws<InvalidOperationException>(() => f.Contacts.Save("team", "1", current =>
        {
            f.Contacts.MoveTemplateFirst("{firstName}");
            return current!;
        }));

        f.AssertNothingWritten();
        Assert.Equal(["{fullName}", "{firstName}", "{title}"], f.TemplatesInFile());
    }

    [Fact]
    public void Throws_the_file_system_s_exception_and_keeps_the_file_the_list_and_the_notices_when_the_write_fails()
    {
        var failure = new IOException("disk full");
        var before = f.Bytes(File);
        var notices = f.Notices.Count;
        f.Files.FailWritesAfter(0, failure);

        var thrown = Assert.Throws<IOException>(() => f.Contacts.MoveTemplateFirst("{title}"));

        Assert.Same(failure, thrown);
        Assert.Equal(before, f.Bytes(File));
        Assert.Equal(["{fullName}", "{firstName}", "{title}"], f.Contacts.MentionTemplates);
        Assert.Equal(notices, f.Notices.Count);
    }
}
