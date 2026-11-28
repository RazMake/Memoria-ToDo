namespace Memoria.ToDo.Shared.Platform;

/// <summary>The one format of the link address that names a contact: <c>contact:</c> and the ID.</summary>
public static class ContactAddress
{
    private const string Prefix = "contact:";

    /// <summary><c>contact:</c> followed by the ID.</summary>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty.</exception>
    public static string Of(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return Prefix + id;
    }

    /// <summary>The ID in a contact address, or null when the address isn't one.</summary>
    public static string? IdOf(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.Length > Prefix.Length && address.StartsWith(Prefix, StringComparison.Ordinal)
            ? address[Prefix.Length..]
            : null;
    }
}
