namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>What the form and the details call each contact field. The fields themselves come from Contacts' <c>ContactFields</c>.</summary>
internal static class FieldLabels
{
    private static readonly Dictionary<string, string> ByName = new()
    {
        ["id"] = "ID",
        ["alias"] = "Alias",
        ["fullName"] = "Full name",
        ["firstName"] = "First name",
        ["title"] = "Title",
        ["location"] = "Location",
        ["levelId"] = "Level",
        ["workingType"] = "Working type",
        ["pronounsKey"] = "Pronouns",
        ["careerPathKey"] = "Career path",
        ["reportsTo"] = "Reports to",
        ["dayStartTime"] = "Day start time",
        ["levelStartDate"] = "Level start date",
        ["teamMemberSince"] = "Team member since",
        ["exitDate"] = "Exit date",
    };

    /// <summary>The label of the field with this file name. Throws when the field has no label.</summary>
    public static string Of(string fieldName) => ByName[fieldName];
}
