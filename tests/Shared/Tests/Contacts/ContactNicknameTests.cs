using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Contacts.Tests;

/// <summary>A contact's nickname: the stored value, falling back to the first name.</summary>
[Unit]
public sealed class ContactNicknameTests
{
    [Fact]
    public void Gives_the_stored_nickname_when_it_has_text()
    {
        Assert.Equal("Nia", new Contact("1", "Ana Popa", FirstName: "Ana", Nickname: "Nia").Nickname);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Gives_the_first_name_when_the_stored_nickname_is_missing_or_empty(string? stored)
    {
        Assert.Equal("Ana", new Contact("1", "Ana Popa", FirstName: "Ana", Nickname: stored).Nickname);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("", null)]
    public void Gives_no_nickname_when_the_nickname_and_the_first_name_are_both_missing_or_empty(string? first, string? stored)
    {
        Assert.Null(new Contact("1", "Ana Popa", FirstName: first, Nickname: stored).Nickname);
    }

    [Fact]
    public void Sets_the_stored_nickname_with_an_init()
    {
        var contact = new Contact("1", "Ana Popa", FirstName: "Ana") with { Nickname = "Nia" };

        Assert.Equal("Nia", contact.Nickname);
        Assert.Equal("Nia", ContactFields.All.Single(field => field.Name == "nickname").TextOf(contact));
    }

    [Fact]
    public void Tells_apart_contacts_that_differ_only_by_the_stored_nickname()
    {
        var stored = new Contact("1", "Ana Popa", FirstName: "Ana", Nickname: "Ana");
        var fallback = new Contact("1", "Ana Popa", FirstName: "Ana");

        Assert.Equal(stored.Nickname, fallback.Nickname);
        Assert.NotEqual(stored, fallback);
    }
}
