namespace Memoria.ToDo.Shared.Contacts;

/// <summary>What a group's name means: the user's own group, a team group, and the heading shown for it.</summary>
internal static class GroupNames
{
    public const string Me = "me";

    public static bool IsMe(string name) => name.Equals(Me, StringComparison.OrdinalIgnoreCase);

    public static bool IsTeam(string name) =>
        name.Equals("team", StringComparison.OrdinalIgnoreCase) || name.Equals("ex-team", StringComparison.OrdinalIgnoreCase);

    /// <summary>The name with each hyphen-separated word capitalized, hyphens kept.</summary>
    public static string Heading(string name) =>
        string.Join('-', name.Split('-').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
}
