using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Adding parts to the synced repository, and the skipped files they report together.</summary>
[Unit]
public sealed class SyncedRepositoryPartsTests
{
    private readonly RepositoryRig rig = new();

    [Fact]
    public void Refuses_a_part_added_after_the_first_selection()
    {
        rig.Repository.Select(RepositoryRig.Clone);

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Add(new FakeRepositoryPart()));
    }

    [Fact]
    public void Refuses_a_part_added_after_a_selection_of_no_folder()
    {
        rig.Repository.Select(null);

        Assert.Throws<InvalidOperationException>(() => rig.Repository.Add(new FakeRepositoryPart()));
    }

    [Fact]
    public void Refuses_a_missing_part()
    {
        Assert.Throws<ArgumentNullException>(() => rig.Repository.Add(null!));
    }

    [Fact]
    public void Refuses_the_same_part_twice()
    {
        var part = rig.Add("a");

        Assert.Throws<ArgumentException>(() => rig.Repository.Add(part));
    }

    [Fact]
    public void Reports_no_skipped_files_with_no_parts()
    {
        Assert.Empty(rig.Repository.SkippedFiles);
    }

    [Fact]
    public void Reports_every_parts_skipped_files_in_the_order_the_parts_were_added()
    {
        var a = rig.Add("a");
        rig.Add("quiet");
        var c = rig.Add("c");
        var a1 = new FakeSkippedFile("tasks/a1.json", "Unreadable.");
        var a2 = new FakeSkippedFile("tasks/a2.json", "Newer.", RequiresUpdate: true);
        var c1 = new FakeSkippedFile("contacts/c1.json", "Unreadable.");
        a.SkippedFiles = [a1, a2];
        c.SkippedFiles = [c1];

        Assert.Equal([a1, a2, c1], rig.Repository.SkippedFiles);
    }
}
