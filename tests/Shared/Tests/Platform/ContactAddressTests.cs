using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The one format of the link address that names a contact.</summary>
[Unit]
public sealed class ContactAddressTests
{
    [Fact]
    public void Makes_an_address_of_the_prefix_and_the_ID()
    {
        Assert.Equal("contact:x", ContactAddress.Of("x"));
    }

    [Fact]
    public void Rejects_an_empty_ID()
    {
        Assert.Throws<ArgumentException>(() => ContactAddress.Of(""));
    }

    [Fact]
    public void Rejects_a_null_ID()
    {
        Assert.Throws<ArgumentNullException>(() => ContactAddress.Of(null!));
    }

    [Fact]
    public void Reads_the_ID_in_a_contact_address()
    {
        Assert.Equal("razvanp", ContactAddress.IdOf("contact:razvanp"));
    }

    [Fact]
    public void Reads_back_the_ID_it_made_an_address_of()
    {
        Assert.Equal("a:b/c", ContactAddress.IdOf(ContactAddress.Of("a:b/c")));
    }

    [Theory]
    [InlineData("task:01900000-0000-7000-8000-000000000001")]
    [InlineData("https://example.com/contact:x")]
    [InlineData("contact:")]
    [InlineData("Contact:x")]
    [InlineData("CONTACT:x")]
    [InlineData("contact")]
    [InlineData(" contact:x")]
    [InlineData("")]
    public void Gives_no_ID_for_an_address_that_is_not_a_contact_address(string address)
    {
        Assert.Null(ContactAddress.IdOf(address));
    }
}
