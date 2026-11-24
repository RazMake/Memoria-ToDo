using System.ComponentModel;
using System.Globalization;
using Memoria.ToDo.Shared.Contacts;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>The tools for contacts.</summary>
internal sealed class ContactTools(ToolRunner run, CloneCheck clone, IContacts contacts)
{
    [McpServerTool(ReadOnly = true), Description("Lists the contact groups in the order the user sees them, each with its contacts, and the user's own contact. Also lists the data types, such as pronouns and career paths, each with the key field that takes its entries' IDs, and names the files that couldn't be read, with the reason.")]
    public Task<string> ListContacts() => run.Run(() =>
    {
        clone.Require();
        return new
        {
            groups = contacts.Groups.Select(group => new { name = group.Name, heading = group.Heading, isTeam = group.IsTeam, contacts = group.Members }).ToList(),
            dataTypes = contacts.DataTypes.Select(type => new { keyField = type.KeyField, entries = type.Entries }).ToList(),
            me = contacts.Me,
            skippedFiles = SkippedFiles.Of(contacts.SkippedFiles),
        };
    });

    [McpServerTool(ReadOnly = true), Description("Gets the one contact with this alias, or else this ID.")]
    public Task<string> FindContact([Description("The alias or the ID.")] string name) => run.Run(() =>
    {
        clone.Require();
        return FindOne(name);
    });

    [McpServerTool(ReadOnly = true), Description("Finds the contacts that have every word of the text in a known field, ignoring case.")]
    public Task<string> SearchContacts([Description("The words to find.")] string text) => run.Run(() =>
    {
        clone.Require();
        return new { contacts = contacts.Search(text) };
    });

    [McpServerTool(ReadOnly = true), Description("Gets a contact's manager and direct reports.")]
    public Task<string> GetReportingLine([Description("The contact's alias or ID.")] string name) => run.Run(() =>
    {
        clone.Require();
        var contact = FindOne(name);
        return new { manager = contacts.ManagerOf(contact), directReports = contacts.DirectReportsOf(contact) };
    });

    [McpServerTool(ReadOnly = true), Description("Gets the Markdown link that mentions a contact, to put in a task's description, and the templates that can shape its text.")]
    public Task<string> GetContactMention(
        [Description("The contact's alias or ID.")] string name,
        [Description("One of the templates, such as {fullName}. The first one when left out.")] string? template = null) => run.Run(() =>
    {
        clone.Require();
        return new
        {
            mention = contacts.MentionOf(FindOne(name), template ?? contacts.MentionTemplates[0]),
            templates = contacts.MentionTemplates,
        };
    });

    [McpServerTool(Destructive = false), Description("Adds a contact to a group, or changes the contact in it that has the same ID. On an existing contact, a field you leave out keeps its value and an empty value clears it; a new contact gets only the fields you give. Answers the contact.")]
    public Task<string> SaveContact(
        [Description("The group's name.")] string group,
        [Description("The contact's ID.")] string id,
        [Description("The full name. Required for a new contact; an existing one keeps its name when left out.")] string? fullName = null,
        string? alias = null,
        string? firstName = null,
        string? title = null,
        string? pronounsKey = null,
        string? careerPathKey = null,
        string? levelId = null,
        string? workingType = null,
        string? location = null,
        string? reportsTo = null,
        [Description("HH:mm.")] string? dayStartTime = null,
        [Description("yyyy-MM-dd.")] string? levelStartDate = null,
        [Description("yyyy-MM-dd.")] string? teamMemberSince = null,
        [Description("yyyy-MM-dd.")] string? exitDate = null,
        string? nickname = null) => run.Run(() =>
    {
        clone.Require();
        Contact? saved = null;
        contacts.Save(group, id, held =>
        {
            // A new contact without a full name is left for Contacts to refuse.
            var current = held ?? new Contact(id, fullName ?? "");
            var contact = current with
            {
                Id = id,
                FullName = fullName ?? current.FullName,
                Alias = Text(alias, current.Alias),
                FirstName = Text(firstName, current.FirstName),
                Title = Text(title, current.Title),
                PronounsKey = Text(pronounsKey, current.PronounsKey),
                CareerPathKey = Text(careerPathKey, current.CareerPathKey),
                LevelId = Text(levelId, current.LevelId),
                WorkingType = Text(workingType, current.WorkingType),
                Location = Text(location, current.Location),
                ReportsTo = Text(reportsTo, current.ReportsTo),
                DayStartTime = TimeOf(dayStartTime, current.DayStartTime),
                LevelStartDate = DateOf(levelStartDate, current.LevelStartDate),
                TeamMemberSince = DateOf(teamMemberSince, current.TeamMemberSince),
                ExitDate = DateOf(exitDate, current.ExitDate),
            };
            if (nickname is not null)
            {
                // Set apart: reading the held nickname gives the first name when none is stored, which would be stored by mistake.
                contact = contact with { Nickname = Text(nickname, null) };
            }

            saved = contact;
            return contact;
        });
        return saved!;
    }, [id, group]);

    [McpServerTool(Destructive = true), Description("Deletes a contact from a group for good; it can't be restored.")]
    public Task<string> DeleteContact(
        [Description("The group's name.")] string group,
        [Description("The contact's ID.")] string id) => run.Run(() =>
    {
        clone.Require();
        contacts.Delete(group, id);
        return new { deleted = id };
    }, [id, group]);

    [McpServerTool(Destructive = false), Description("Moves a group to show after previousGroup and before nextGroup, which must be neighbors; leave out previousGroup for the start, nextGroup for the end.")]
    public Task<string> MoveContactGroup(
        [Description("The group to move.")] string group,
        [Description("The group it shows after.")] string? previousGroup = null,
        [Description("The group it shows before.")] string? nextGroup = null) => run.Run(() =>
    {
        clone.Require();
        contacts.MoveGroup(group, previousGroup, nextGroup);
        return new { groups = contacts.Groups.Select(item => item.Name).ToList() };
    }, [group]);

    private Contact FindOne(string name) =>
        contacts.Find(name) ?? throw new ArgumentException($"No single contact has the alias or ID {name}.");

    // A field left out keeps its current value; an empty one clears it.
    private static string? Text(string? given, string? current) => given is null ? current : given.Length == 0 ? null : given;

    private static TimeOnly? TimeOf(string? text, TimeOnly? current) =>
        text is null ? current
        : text.Length == 0 ? null
        : ContactTime.TryRead(text, out var time) ? time
        : throw new ArgumentException("The day start time must be written HH:mm, such as 09:30.");

    private static DateOnly? DateOf(string? text, DateOnly? current) =>
        text is null ? current
        : text.Length == 0 ? null
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date
        : throw new ArgumentException($"A date must be written yyyy-MM-dd, such as 2026-11-23, not {text}.");
}
