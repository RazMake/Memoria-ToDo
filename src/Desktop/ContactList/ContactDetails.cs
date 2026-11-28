namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>One value of a detail field. <see cref="Target"/> is the contact a click selects, for "Reports to" and "Direct reports".</summary>
internal sealed record DetailValue(string Text, ContactRef? Target);

/// <summary>One field of the details: its label and its values (one, except "Direct reports").</summary>
internal sealed record DetailField(string Label, IReadOnlyList<DetailValue> Values);

/// <summary>The details of a selected contact: its full name, its group's heading, and the fields it has, team fields only in team groups.</summary>
internal sealed record ContactDetails(string Heading, string GroupHeading, IReadOnlyList<DetailField> Fields);
