using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Reads a clone's contact files, and writes group files.</summary>
internal sealed class ContactFolder(IFileSystem files)
{
    /// <summary>Everything readable in the clone. A file that can't be read is skipped, so one bad file doesn't hide the rest.</summary>
    public ContactSnapshot Read(string clone)
    {
        var skipped = new List<ISkippedRepositoryFile>();
        var skippedGroups = new List<string>();
        var groups = new List<GroupFile>();
        var names = files.ListFiles(ContactLayout.Folder(clone))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(file => file.EndsWith(ContactLayout.GroupExtension, StringComparison.Ordinal) && file.Length > ContactLayout.GroupExtension.Length)
            .Select(file => file[..^ContactLayout.GroupExtension.Length])
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (var name in names)
        {
            var load = FileLoad<GroupFile?>.Read(files, ContactLayout.GroupFile(clone, name), bytes => GroupFileFormat.Read(name, bytes), null);
            if (load.Value is { } group)
            {
                groups.Add(group);
            }
            else if (load.Failure is not null)
            {
                skipped.Add(SkippedFile.Of(ContactLayout.GroupRelativePath(name), load.Failure));
                skippedGroups.Add(name);
            }
        }

        SkipDuplicateOwnGroups(names, groups, skipped, skippedGroups);
        var hasContactFiles = names.Count > 0;
        var dataTypes = KeyFields.All.Select(key => ReadDataType(clone, key, hasContactFiles, skipped)).ToList();
        var templates = FileLoad<IReadOnlyList<string>>.Read(files, ContactLayout.File(clone, ContactLayout.TemplatesRelativePath),
            TemplatesFileFormat.Read, TemplatesFileFormat.Default);
        if (templates.Failure is not null)
            skipped.Add(SkippedFile.Of(ContactLayout.TemplatesRelativePath, templates.Failure));

        return new ContactSnapshot(hasContactFiles, groups, dataTypes, templates.Value, skipped, skippedGroups);
    }

    /// <summary>
    /// When several files are the user's own group, such as me.json and Me.json, none is used: each is skipped and reported once.
    /// Unreadable files count too, and keep the reason they were already skipped for.
    /// </summary>
    private static void SkipDuplicateOwnGroups(List<string> names, List<GroupFile> groups, List<ISkippedRepositoryFile> skipped, List<string> skippedGroups)
    {
        var ownNames = names.Where(GroupNames.IsMe).ToList();
        if (ownNames.Count < 2)
            return;
        var failure = new InvalidDataException("Another file's name differs from this one only in case.");
        foreach (var name in ownNames.Where(name => !skippedGroups.Contains(name)))
        {
            skipped.Add(SkippedFile.Of(ContactLayout.GroupRelativePath(name), failure));
            skippedGroups.Add(name);
        }

        groups.RemoveAll(group => group.IsMe);
    }

    public void Write(string clone, GroupFile group) =>
        files.WriteFile(ContactLayout.GroupFile(clone, group.Name), GroupFileFormat.Write(group));

    /// <summary>The templates file as it is on disk now: no value when it's missing or empty, <see cref="FileLoad{T}.Failure"/> when it can't be read.</summary>
    public FileLoad<TemplatesFile?> ReadTemplates(string clone) =>
        FileLoad<TemplatesFile?>.Read(files, ContactLayout.File(clone, ContactLayout.TemplatesRelativePath), TemplatesFileFormat.Parse, null);

    public void WriteTemplates(string clone, TemplatesFile file) =>
        files.WriteFile(ContactLayout.File(clone, ContactLayout.TemplatesRelativePath), TemplatesFileFormat.Write(file));

    private DataType ReadDataType(string clone, KeyField key, bool hasContactFiles, List<ISkippedRepositoryFile> skipped)
    {
        var load = FileLoad<IReadOnlyList<ObjectFields>>.Read(files, ContactLayout.File(clone, key.RelativePath),
            bytes => DataTypeFileFormat.Read(key.ListName, bytes), []);
        if (load.Failure is not null)
            skipped.Add(SkippedFile.Of(key.RelativePath, load.Failure));
        else if (!load.Present && hasContactFiles)
            skipped.Add(SkippedFile.Of(key.RelativePath, new FileNotFoundException("The file is missing.")));
        return new DataType(key.Field, key.RelativePath, load.Present && load.Failure is null, DataTypeFileFormat.Entries(load.Value));
    }
}
