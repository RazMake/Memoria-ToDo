using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A call that names a group whose file was skipped throws SkippedFileException naming that file, and changes nothing.</summary>
[Unit]
public sealed class SkippedGroupCallTests
{
    private const string BrokenFile = ContactsFixture.Folder + "/broken.json";
    private const string OtherFile = ContactsFixture.Folder + "/other.json";

    private readonly ContactsFixture f = new();

    public SkippedGroupCallTests()
    {
        f.SeedDataTypes();
        f.SeedGroup("a", 1000, ContactFiles.Person("a-1", "Ana"));
        f.Seed(BrokenFile, "{ not json");
        f.Seed(OtherFile, "{ not json");
        f.SeedGroup("c", 3000, ContactFiles.Person("c-1", "Cy"));
        f.Load();
        f.ForgetWrites();
    }

    private void AssertNothingChanged(byte[] brokenBefore, List<string> snapshot, int notices)
    {
        f.AssertNothingWritten();
        Assert.Equal(brokenBefore, f.Bytes(BrokenFile));
        Assert.Equal(snapshot, f.Snapshot());
        Assert.Equal(notices, f.Notices.Count);
    }

    private void AssertThrowsSkipped(Action call)
    {
        var before = f.Bytes(BrokenFile);
        var snapshot = f.Snapshot();
        var notices = f.Notices.Count;

        var failure = Assert.Throws<SkippedFileException>(call);

        Assert.Equal(BrokenFile, failure.File.RelativePath);
        AssertNothingChanged(before, snapshot, notices);
    }

    [Fact]
    public void Save_into_a_group_whose_file_was_skipped_throws_naming_that_file_and_writes_nothing() =>
        AssertThrowsSkipped(() => f.Contacts.Save("broken", new Contact("x", "Xi")));

    [Fact]
    public void Delete_from_a_group_whose_file_was_skipped_throws_naming_that_file_and_writes_nothing() =>
        AssertThrowsSkipped(() => f.Contacts.Delete("broken", "x"));

    [Fact]
    public void Move_of_a_group_whose_file_was_skipped_throws_naming_that_file_and_writes_nothing() =>
        AssertThrowsSkipped(() => f.Contacts.MoveGroup("broken", "a", "c"));

    [Fact]
    public void Move_next_to_a_previous_group_whose_file_was_skipped_throws_naming_that_file_and_writes_nothing() =>
        AssertThrowsSkipped(() => f.Contacts.MoveGroup("c", "broken", null));

    [Fact]
    public void Move_next_to_a_next_group_whose_file_was_skipped_throws_naming_that_file_and_writes_nothing() =>
        AssertThrowsSkipped(() => f.Contacts.MoveGroup("a", null, "broken"));

    [Fact]
    public void Move_checks_for_a_skipped_group_before_the_neighbors_are_checked_to_be_next_to_each_other() =>
        AssertThrowsSkipped(() => f.Contacts.MoveGroup("a", "broken", "nobody"));

    [Fact]
    public void Move_checks_the_moved_group_before_its_neighbors()
    {
        var failure = Assert.Throws<SkippedFileException>(() => f.Contacts.MoveGroup("broken", "other", null));

        Assert.Equal(BrokenFile, failure.File.RelativePath);
    }

    [Fact]
    public void Move_checks_the_previous_neighbor_before_the_next_one()
    {
        var failure = Assert.Throws<SkippedFileException>(() => f.Contacts.MoveGroup("a", "other", "broken"));

        Assert.Equal(OtherFile, failure.File.RelativePath);
    }

    [Fact]
    public void Move_of_a_group_unknown_and_not_skipped_still_throws_ArgumentException_and_writes_nothing()
    {
        var before = f.Snapshot();

        Assert.Throws<ArgumentException>(() => f.Contacts.MoveGroup("nobody", "a", "c"));

        f.AssertNothingWritten();
        Assert.Equal(before, f.Snapshot());
    }

    [Fact]
    public void Save_into_an_unknown_group_that_is_not_skipped_still_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.Save("nobody", new Contact("x", "Xi")));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Delete_from_an_unknown_group_that_is_not_skipped_still_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => f.Contacts.Delete("nobody", "x"));

        f.AssertNothingWritten();
    }

    [Fact]
    public void Save_into_a_readable_group_still_works_while_another_group_is_skipped()
    {
        f.Contacts.Save("a", new Contact("a-1", "Ana B"));

        Assert.Equal([ContactsFixture.Folder + "/a.json"], f.Written);
    }
}
