namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One contact's known fields. Null means the field is missing.</summary>
public sealed record Contact(
    string Id, string FullName, string? Alias = null, string? FirstName = null, string? Title = null,
    string? PronounsKey = null, string? CareerPathKey = null, string? LevelId = null, string? WorkingType = null,
    string? Location = null, string? ReportsTo = null, TimeOnly? DayStartTime = null,
    DateOnly? LevelStartDate = null, DateOnly? TeamMemberSince = null, DateOnly? ExitDate = null,
    string? Nickname = null)
{
    private readonly string? _storedNickname = Nickname;

    /// <summary>
    /// The nickname, or the first name when the stored nickname is null or empty; null when both are.
    /// Init sets the stored value, which equality compares and ContactFields' "nickname" TextOf gives.
    /// </summary>
    public string? Nickname
    {
        get => _storedNickname is { Length: > 0 } ? _storedNickname : FirstName is { Length: > 0 } ? FirstName : null;
        init => _storedNickname = value;
    }

    /// <summary>The nickname as stored, without the first-name fallback.</summary>
    internal string? StoredNickname => _storedNickname;
}
