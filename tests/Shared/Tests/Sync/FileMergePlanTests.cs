using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// How Sync decides each file of a merge from its base, local and remote contents (docs/architecture/sync.md). Paths absent
/// from the plan's actions end as the remote has them.
/// </summary>
[Unit]
public sealed class FileMergePlanTests
{
    private const string Task = "tasks/t1.json";
    private const string Image = "images/t1/a.png";
    private const string TagColors = "tags.json";
    private const string Group = "autocomplete/contacts/team.json";
    private const string DataType = "autocomplete/contacts/dataTypes/pronouns.json";
    private const string Unreadable = FakeContactFileRules.Unreadable;

    [Fact]
    public void Merges_a_contact_file_both_sides_changed_through_contacts_merge()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, "b", "l", "r"), (DataType, "b", "l", "r"));

        Assert.Equal("b|l|r", WrittenText(plan, Group));
        Assert.Equal("b|l|r", WrittenText(plan, DataType));
        Assert.Equal(2, contacts.MergedPaths.Count);
        Assert.Contains(Group, contacts.MergedPaths);
        Assert.Contains(DataType, contacts.MergedPaths);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Asks_contacts_to_merge_a_contact_file_only_one_side_changed()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, "b", "l", "b"));

        Assert.Equal("b|l|b", WrittenText(plan, Group));
        Assert.Equal([Group], contacts.MergedPaths);
    }

    [Fact]
    public void Merges_a_contact_file_that_exists_on_one_side_only()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, null, "l", null));

        Assert.Equal("-|l|-", WrittenText(plan, Group));
    }

    [Fact]
    public void Does_not_ask_contacts_to_merge_a_contact_file_both_sides_have_identically()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, "b", "same", "same"), (DataType, null, "same", "same"));

        Assert.Empty(plan.Actions);
        Assert.Empty(contacts.MergedPaths);
    }

    [Fact]
    public void Does_not_ask_contacts_to_merge_a_contact_file_both_sides_lack()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, "b", null, null));

        Assert.Empty(plan.Actions);
        Assert.Empty(contacts.MergedPaths);
    }

    [Fact]
    public void Deletes_the_remote_copy_of_a_contact_file_when_contacts_merge_returns_nothing()
    {
        var contacts = HoldingContacts();
        contacts.MergeWith = (_, _, _, _) => null;

        var plan = DecideWith(contacts, (Group, "b", "b", "r"));

        Assert.IsType<Remove>(plan.Actions[Group]);
    }

    [Fact]
    public void Writes_nothing_when_contacts_merge_returns_what_the_remote_has()
    {
        var contacts = HoldingContacts();
        contacts.MergeWith = (_, _, _, remote) => remote;

        var plan = DecideWith(contacts, (Group, "b", "l", "r"));

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Leaves_other_files_under_autocomplete_as_the_remote_has_them_and_never_asks_contacts_to_merge_them()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(
            contacts,
            ("autocomplete/notes.md", "b", "l", "r"),
            ("autocomplete/contacts/deeper/team.json", "b", "l", "r"),
            ("autocomplete/contacts/dataTypes/unknown.json", "b", "l", "r"),
            ("autocomplete/contacts/local-only.txt", null, "l", null));

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Skipped);
        Assert.Empty(contacts.MergedPaths);
    }

    [Fact]
    public void Takes_the_changed_side_of_an_unreadable_contact_file_when_only_one_side_changed()
    {
        var contacts = HoldingContacts();

        var localChanged = DecideWith(contacts, (Group, "v1", Unreadable, "v1"));
        var remoteChanged = DecideWith(contacts, (DataType, "v1", "v1", Unreadable));

        Assert.Equal(Unreadable, WrittenText(localChanged, Group));
        Assert.Empty(localChanged.Skipped);
        Assert.Empty(remoteChanged.Actions);
        Assert.Empty(remoteChanged.Skipped);
    }

    [Fact]
    public void Keeps_the_local_copy_and_reports_an_unreadable_contact_file_both_sides_changed()
    {
        var contacts = HoldingContacts();

        var localUnreadable = DecideWith(contacts, (Group, "v1", Unreadable, "v2"));
        var remoteUnreadable = DecideWith(contacts, (DataType, "v1", "v2", Unreadable));

        Assert.Equal(Unreadable, WrittenText(localUnreadable, Group));
        Assert.Equal(Group, Assert.Single(localUnreadable.Skipped).RelativePath);
        Assert.Equal("v2", WrittenText(remoteUnreadable, DataType));
        Assert.Equal(DataType, Assert.Single(remoteUnreadable.Skipped).RelativePath);
    }

    [Fact]
    public void Keeps_a_malformed_local_contact_file_and_reports_it_when_the_remote_deleted_it()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, "v1", Unreadable, null));

        Assert.Equal(Unreadable, WrittenText(plan, Group));
        Assert.Equal(Group, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Keeps_a_malformed_local_contact_file_and_reports_it_when_the_remote_deleted_it_and_the_base_has_the_same_malformed_content()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, Unreadable, Unreadable, null));

        Assert.Equal(Unreadable, WrittenText(plan, Group));
        Assert.Equal(Group, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Keeps_a_local_contact_edit_and_reports_it_when_the_remote_deleted_a_file_whose_base_was_unreadable()
    {
        var contacts = HoldingContacts();

        var plan = DecideWith(contacts, (Group, Unreadable, "v2", null));

        Assert.Equal("v2", WrittenText(plan, Group));
        Assert.Equal(Group, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Still_lets_a_malformed_local_task_follow_the_remote_deleting_it_when_contacts_hold_other_paths()
    {
        var plan = DecideWith(HoldingContacts(), (Task, "v1", Rules.Unreadable, null), (Group, "v1", Unreadable, null));

        Assert.False(plan.Actions.ContainsKey(Task));
        Assert.DoesNotContain(plan.Skipped, file => file.RelativePath == Task);
        Assert.Equal(Unreadable, WrittenText(plan, Group));
    }

    [Fact]
    public void Writes_the_merged_task_when_only_the_local_side_edited_it()
    {
        var plan = Decide((Task, "v1", "v2", "v1"));

        Assert.Equal("v2", WrittenText(plan, Task));
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Leaves_a_file_both_sides_have_identically_as_it_is()
    {
        var plan = Decide((Task, "v1", "v2", "v2"));

        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Lets_a_malformed_local_task_follow_the_remote_deleting_it()
    {
        var plan = Decide((Task, "v1", Rules.Unreadable, null));

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Keeps_a_malformed_local_task_and_reports_it_when_the_remote_changed_it()
    {
        var plan = Decide((Task, "v1", Rules.Unreadable, "v2"));

        Assert.Equal(Rules.Unreadable, WrittenText(plan, Task));
        Assert.Equal(Task, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Keeps_a_local_edit_and_reports_it_when_the_remote_made_the_task_unreadable()
    {
        var plan = Decide((Task, "v1", "v2", Rules.Unreadable));

        Assert.Equal("v2", WrittenText(plan, Task));
        Assert.Equal(Task, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Keeps_a_local_edit_and_reports_it_when_the_remote_deleted_a_task_whose_base_was_unreadable()
    {
        var plan = Decide((Task, Rules.Unreadable, "v2", null));

        Assert.Equal("v2", WrittenText(plan, Task));
        Assert.Equal(Task, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Takes_the_images_of_the_local_side_when_a_locally_edited_task_survives_the_remote_deleting_it()
    {
        var plan = Decide((Task, "v1", "v2", null), (Image, "png", "png", null));

        Assert.Equal("v2", WrittenText(plan, Task));
        Assert.Equal("png", WrittenText(plan, Image));
    }

    [Fact]
    public void Takes_the_images_of_the_remote_side_when_a_remotely_edited_task_survives_the_local_deletion()
    {
        var plan = Decide((Task, "v1", null, "v2"), (Image, "png", null, "png"));

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Does_not_restore_an_image_the_surviving_side_removed()
    {
        var plan = Decide((Task, "v1", "v2", null), (Image, "png", null, "png"));

        Assert.IsType<Remove>(plan.Actions[Image]);
    }

    [Fact]
    public void Deletes_an_unedited_task_and_its_images_when_the_remote_deleted_them()
    {
        var plan = Decide((Task, "v1", "v1", null), (Image, "png", "png", null));

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Merges_a_tag_colors_file_both_sides_changed_through_the_tag_colors_merge()
    {
        var plan = Decide((TagColors, "b", "l", "r"));

        Assert.Equal("b|l|r", WrittenText(plan, TagColors));
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Leaves_a_tag_colors_file_both_sides_have_identically_as_it_is()
    {
        Assert.Empty(Decide((TagColors, "b", "same", "same")).Actions);
    }

    [Fact]
    public void Keeps_an_unreadable_local_tag_colors_file_and_reports_it_when_the_remote_changed_it()
    {
        var plan = Decide((TagColors, "v1", Rules.Unreadable, "v2"));

        Assert.Equal(Rules.Unreadable, WrittenText(plan, TagColors));
        Assert.Equal(TagColors, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Keeps_a_local_tag_colors_edit_and_reports_it_when_the_remote_made_the_file_unreadable()
    {
        var plan = Decide((TagColors, "v1", "v2", Rules.Unreadable));

        Assert.Equal("v2", WrittenText(plan, TagColors));
        Assert.Equal(TagColors, Assert.Single(plan.Skipped).RelativePath);
    }

    [Fact]
    public void Ends_unmanaged_files_as_the_remote_has_them_whatever_the_local_side_did()
    {
        var plan = Decide(("notes.txt", "x", "local", "remote"), ("local-only.txt", null, "local", null), ("gone.txt", "x", "local", null));

        Assert.Empty(plan.Actions);
        Assert.Empty(plan.Skipped);
    }

    private static Plan Decide(params (string Path, string? Base, string? Local, string? Remote)[] files) => DecideWith(new FakeContactFileRules(), files);

    private static Plan DecideWith(FakeContactFileRules contacts, params (string Path, string? Base, string? Local, string? Remote)[] files)
    {
        var versions = files.ToDictionary(f => f.Path, f => new FileVersions(Bytes(f.Base), Bytes(f.Local), Bytes(f.Remote)));
        var actions = new Dictionary<string, PathAction>();
        var skipped = FileMergePlan.Decide(versions.Keys, path => versions[path], new Rules(), contacts, (path, action) => actions.Add(path, action));
        return new Plan(actions, skipped);
    }

    /// <summary>Contacts' rules holding a group file and a data type file.</summary>
    private static FakeContactFileRules HoldingContacts()
    {
        var contacts = new FakeContactFileRules();
        contacts.Hold(Group, DataType);
        return contacts;
    }

    private static byte[]? Bytes(string? text) => text is null ? null : Encoding.UTF8.GetBytes(text);

    private static string WrittenText(Plan plan, string path) =>
        Encoding.UTF8.GetString(Assert.IsType<Write>(plan.Actions[path]).Content);

    /// <summary>The actions the plan applied, and the files it reported as skipped.</summary>
    private sealed record Plan(IReadOnlyDictionary<string, PathAction> Actions, IReadOnlyList<ISkippedRepositoryFile> Skipped);

    /// <summary>Tasks' layout and a text-based task merge: a file reading <see cref="Unreadable"/> is malformed, a deletion yields to an edit.</summary>
    private sealed class Rules : IRepositoryFileRules
    {
        public const string Unreadable = "unreadable";

        public string TasksFolderName => "tasks";

        public string ImagesFolderName => "images";

        public string SettingsFileName => "settings.json";

        public string TagColorsFileName => "tags.json";

        public RepositoryFileKind Classify(string relativePath) =>
            relativePath.StartsWith("tasks/", StringComparison.Ordinal) ? RepositoryFileKind.Task
            : relativePath.StartsWith("images/", StringComparison.Ordinal) ? RepositoryFileKind.Image
            : relativePath == SettingsFileName ? RepositoryFileKind.Settings
            : relativePath == TagColorsFileName ? RepositoryFileKind.TagColors
            : RepositoryFileKind.Unmanaged;

        public string? TaskIdInPath(string relativePath) => relativePath.Split('/') is [_, var id, ..] ? Path.GetFileNameWithoutExtension(id) : null;

        public byte[]? MergeTask(string expectedTaskId, byte[]? basis, byte[]? local, byte[]? remote)
        {
            if (new[] { basis, local, remote }.Any(file => file is not null && Encoding.UTF8.GetString(file) == Unreadable))
                throw new InvalidDataException("The task can't be read.");

            if (local is null || remote is null)
            {
                var survivor = local ?? remote;
                return survivor is not null && basis is not null && survivor.SequenceEqual(basis) ? null : survivor;
            }

            return local.SequenceEqual(remote) || (basis is not null && basis.SequenceEqual(local)) ? remote : local;
        }

        public byte[]? MergeSettings(byte[]? basis, byte[]? local, byte[]? remote) => throw new NotSupportedException();

        /// <summary>Joins the three versions, so a test sees that the plan used this merge and with which inputs.</summary>
        public byte[]? MergeTagColors(byte[]? basis, byte[]? local, byte[]? remote)
        {
            if (new[] { basis, local, remote }.Any(file => file is not null && Encoding.UTF8.GetString(file) == Unreadable))
                throw new InvalidDataException("The tag colors can't be read.");

            return basis is null && local is null && remote is null
                ? null
                : Encoding.UTF8.GetBytes(string.Join('|', new[] { basis, local, remote }.Select(file => file is null ? "-" : Encoding.UTF8.GetString(file))));
        }
    }
}
