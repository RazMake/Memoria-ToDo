namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One contact's known fields. Null means the field is missing.</summary>
public sealed record Contact(
    string Id, string FullName, string? Alias = null, string? FirstName = null, string? Title = null,
    string? PronounsKey = null, string? CareerPathKey = null, string? LevelId = null, string? WorkingType = null,
    string? Location = null, string? ReportsTo = null, TimeOnly? DayStartTime = null,
    DateOnly? LevelStartDate = null, DateOnly? TeamMemberSince = null, DateOnly? ExitDate = null);
