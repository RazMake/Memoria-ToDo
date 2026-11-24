using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Api.Contract.Tests;

/// <summary>The pipe's name, which both clients must compute the same way.</summary>
[Unit]
public class LocalApiPipeTests
{
    [Fact]
    public void The_name_is_Memoria_ToDo_LocalApi_then_the_domain_then_the_user()
    {
        Assert.Equal("Memoria-ToDo.LocalApi.CORP.razvan", LocalApiPipe.NameFor("CORP", "razvan"));
    }

    [Fact]
    public void The_name_keeps_the_domain_and_user_as_they_are_spelled()
    {
        Assert.Equal("Memoria-ToDo.LocalApi.MyPc.Ana Lee", LocalApiPipe.NameFor("MyPc", "Ana Lee"));
    }

    [Fact]
    public void Different_users_get_different_names()
    {
        Assert.NotEqual(LocalApiPipe.NameFor("PC", "ana"), LocalApiPipe.NameFor("PC", "bob"));
    }

    [Fact]
    public void Different_domains_get_different_names()
    {
        Assert.NotEqual(LocalApiPipe.NameFor("PC1", "ana"), LocalApiPipe.NameFor("PC2", "ana"));
    }

    [Fact]
    public void A_missing_domain_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => LocalApiPipe.NameFor(null!, "ana"));
    }

    [Fact]
    public void A_missing_user_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => LocalApiPipe.NameFor("PC", null!));
    }

    [Fact]
    public void A_blank_domain_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => LocalApiPipe.NameFor(" ", "ana"));
    }

    [Fact]
    public void A_blank_user_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => LocalApiPipe.NameFor("PC", ""));
    }

    [Fact]
    public void The_name_for_the_current_user_uses_the_signed_in_domain_and_user()
    {
        Assert.Equal(
            $"Memoria-ToDo.LocalApi.{Environment.UserDomainName}.{Environment.UserName}",
            LocalApiPipe.ForCurrentUser());
    }
}
