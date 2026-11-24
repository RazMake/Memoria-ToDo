namespace Memoria.ToDo.Api.Contract;

/// <summary>The local API's pipe name, the one .NET home of the rule.</summary>
public static class LocalApiPipe
{
    /// <summary><c>Memoria-ToDo.LocalApi.&lt;domain&gt;.&lt;user&gt;</c>. Throws ArgumentNullException for a null argument and ArgumentException for an empty or blank one.</summary>
    public static string NameFor(string domain, string user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        return $"Memoria-ToDo.LocalApi.{domain}.{user}";
    }

    /// <summary>The name for the signed-in account: <see cref="NameFor"/> of <see cref="Environment.UserDomainName"/> and <see cref="Environment.UserName"/>.</summary>
    public static string ForCurrentUser() => NameFor(Environment.UserDomainName, Environment.UserName);
}
