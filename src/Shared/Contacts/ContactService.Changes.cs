using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

internal sealed partial class ContactService
{
    public void Save(string groupName, Contact contact)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        ArgumentNullException.ThrowIfNull(contact);
        Save(groupName, contact.Id, _ => contact);
    }

    public void Save(string groupName, string id, Func<Contact?, Contact> change)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(change);
        Contact? saved = null;
        Change(
            current =>
            {
                var group = WritableGroup(current, groupName);
                saved = change(MemberWithId(group, id)?.Contact) ?? throw new ArgumentException("The change returned no contact.", nameof(change));
                return [SaveInto(current, group, saved)];
            },
            written => logger.Information("Saved contact {ContactId} {ContactName} in {GroupFile}", saved!.Id, saved.FullName, GroupFileOf(written[0])));
    }

    public void Delete(string groupName, string id)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        ArgumentNullException.ThrowIfNull(id);
        Contact? deleted = null;
        Change(
            current =>
            {
                if (GroupNames.IsMe(groupName))
                    throw new ArgumentException("The user's own contact can't be deleted.", nameof(groupName));
                var group = WritableGroup(current, groupName);
                var member = MemberWithId(group, id) ?? throw new ArgumentException($"The group has no contact '{id}'.", nameof(id));
                deleted = member.Contact;
                return [group.WithMembers(group.Members.Where(other => !ReferenceEquals(other, member)))];
            },
            written => logger.Information("Deleted contact {ContactId} {ContactName} from {GroupFile}", deleted!.Id, deleted.FullName, GroupFileOf(written[0])));
    }

    public void MoveGroup(string groupName, string? previousGroup, string? nextGroup)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        Change(current => GroupOrder.Plan(current.GroupFiles, groupName, previousGroup, nextGroup), _ =>
            logger.Information("Moved the group in {GroupFile}", ContactLayout.GroupRelativePath(groupName)));
    }

    private static string GroupFileOf(GroupFile group) => ContactLayout.GroupRelativePath(group.Name);

    /// <summary>
    /// Writes the group files the plan names, one by one, under the lock, then announces what was written outside it.
    /// A write that fails partway still publishes the files already written. Then Changed is raised and the local save reported,
    /// each even when the other fails; one failure is thrown as it is, several together in that order: the write's, Changed's, the report's.
    /// <paramref name="logWritten"/> logs the operation under the lock once files were written, given the files written, so it is logged even when a notice then fails.
    /// </summary>
    private void Change(Func<ContactSnapshot, IReadOnlyList<GroupFile>> plan, Action<IReadOnlyList<GroupFile>> logWritten)
    {
        var (wrote, writeFailure) = repository.LockedChange(clone =>
        {
            var root = clone ?? throw new NoRepositoryFolderException();
            var written = new List<GroupFile>();
            Exception? failed = null;
            try
            {
                foreach (var group in plan(snapshot))
                {
                    folder.Write(root, group);
                    written.Add(group);
                }
            }
            catch (Exception e) when (written.Count > 0)
            {
                failed = e;
            }

            if (written.Count > 0)
            {
                snapshot = snapshot.With(written);
                if (failed is null)
                    logWritten(written);
            }

            return (written.Count > 0, failed);
        });
        var failures = new FailureList();
        if (writeFailure is not null)
            failures.Add(writeFailure);
        if (wrote)
        {
            failures.Attempt(() => Changed?.Invoke(this, EventArgs.Empty));
            failures.Attempt(repository.ReportLocalSave);
        }

        failures.ThrowIfAny();
    }

    private static GroupFile SaveInto(ContactSnapshot current, GroupFile group, Contact contact)
    {
        if (ContactRules.Problem(contact) is { } problem)
            throw new ArgumentException(problem, nameof(contact));
        var existing = MemberWithId(group, contact.Id);
        if (existing is null && group.IsMe)
            throw new ArgumentException("The user's own contact can't be added or get a new ID.", nameof(contact));
        CheckNames(current, existing?.Contact, contact);
        CheckKeys(current, existing?.Contact, contact);
        return group.WithMembers(existing is null
            ? group.Members.Append(StoredContact.Of(contact))
            : group.Members.Select(member => ReferenceEquals(member, existing) ? member.With(contact) : member));
    }

    private static GroupFile WritableGroup(ContactSnapshot current, string groupName) =>
        current.GroupNamed(groupName) ?? throw (current.IsSkipped(groupName)
            ? new InvalidOperationException($"The file of group '{groupName}' was skipped, so it can't be changed.")
            : new ArgumentException($"There is no group '{groupName}'.", nameof(groupName)));

    private static StoredContact? MemberWithId(GroupFile group, string id) =>
        group.Members.FirstOrDefault(member => member.Contact.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>A name another contact holds is refused, unless the contact being replaced held it already.</summary>
    private static void CheckNames(ContactSnapshot current, Contact? old, Contact contact)
    {
        var oldNames = old is null ? [] : NameIndex.NamesOf(old).ToList();
        foreach (var name in NameIndex.NamesOf(contact))
            if (current.Names.IsHeldByAnother(name, old) && !oldNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Another contact holds the name '{name}'.", nameof(contact));
    }

    /// <summary>A key must be one its data type lists, unless the contact being replaced held it already.</summary>
    private static void CheckKeys(ContactSnapshot current, Contact? old, Contact contact)
    {
        foreach (var field in KeyFields.All)
        {
            if (field.KeyOf(contact) is not { Length: > 0 } key || key == (old is null ? null : field.KeyOf(old)))
                continue;
            var listed = current.DataTypes.Any(type => type.KeyField == field.Field
                && type.Entries.Any(entry => entry.Id.Equals(key, StringComparison.OrdinalIgnoreCase)));
            if (!listed)
                throw new ArgumentException($"'{key}' isn't listed for '{field.Field}'.", nameof(contact));
        }
    }
}
