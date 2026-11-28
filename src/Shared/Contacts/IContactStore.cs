namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Contacts and the rules Sync applies to its files.</summary>
public interface IContactStore
{
    /// <summary>The contacts in the selected clone.</summary>
    IContacts Contacts { get; }

    /// <summary>Sync's file rules; they never read or write files.</summary>
    IContactFileRules Files { get; }
}
