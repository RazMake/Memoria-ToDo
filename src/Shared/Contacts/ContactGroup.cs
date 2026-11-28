namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One group file. The user's own group is named "me". IsTeam is true for the team and ex-team groups.</summary>
public sealed record ContactGroup(string Name, string Heading, int Position, bool IsTeam, IReadOnlyList<Contact> Members);
