using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Creating Tasks joins it to the synced repository, which then selects the folder for it.</summary>
[Unit]
public sealed class TaskStoreRepositoryTests
{
    private readonly TasksFixture f = new();

    [Fact]
    public void Refuses_to_create_without_a_repository_and_touches_no_file()
    {
        Assert.Throws<ArgumentNullException>(() => TaskStore.Create(f.Files, f.Encoder, f.Time, f.Device, null!));

        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Refuses_to_create_over_a_repository_that_already_selected_a_folder_and_touches_no_file()
    {
        var selected = new SyncedRepository();
        selected.Select(f.Clone);

        Assert.Throws<InvalidOperationException>(() => TaskStore.Create(f.Files, f.Encoder, f.Time, f.Device, selected));

        Assert.Equal(f.Clone, selected.Folder);
        Assert.Empty(selected.SkippedFiles);
        Assert.Empty(f.Files.AllFilesUnder(f.Device));
    }

    [Fact]
    public void Reads_the_tasks_when_the_repository_selects_the_folder()
    {
        f.Seed(TasksFixture.Id(1), "Seeded");

        f.Repository.Select(f.Clone);

        Assert.Equal(["Seeded"], f.Titles);
    }

    [Fact]
    public void Empties_the_tasks_when_the_repository_selects_no_folder()
    {
        f.SeedAndUse(TasksFixture.Id(1));

        f.Repository.Select(null);

        Assert.Empty(f.Tasks.All);
    }

    [Fact]
    public void Announces_the_change_when_the_repository_selects_the_folder()
    {
        var counter = f.CountChanges();

        f.Repository.Select(f.Clone);

        Assert.Equal(1, counter.Count);
    }
}
