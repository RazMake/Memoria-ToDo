using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The folder for files that stay on this device: a full path in, a full path per relative name out.</summary>
[Unit]
public sealed class DeviceStoreTests
{
    private static readonly string Folder = Path.GetFullPath("/device-store");

    private readonly DeviceStore store = new(Folder);

    [Theory]
    [InlineData("")]
    [InlineData("store")]
    [InlineData("..")]
    [InlineData("sub/store")]
    public void Rejects_a_folder_that_is_not_a_full_path(string folder)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DeviceStore(folder));
    }

    [Fact]
    public void Accepts_a_full_path_to_a_folder_that_does_not_exist_because_it_touches_no_file()
    {
        var folder = Path.Combine(Path.GetFullPath("/device-store-that-does-not-exist"), Guid.NewGuid().ToString("N"));

        var created = new DeviceStore(folder);

        Assert.NotNull(created);
    }

    [Fact]
    public void Maps_a_relative_name_to_the_full_path_in_the_stores_folder()
    {
        Assert.Equal(Path.Combine(Folder, "Tasks", "draft.json"), store.PathOf("Tasks/draft.json"));
    }

    [Fact]
    public void Maps_a_name_with_no_folder_to_a_file_directly_in_the_stores_folder()
    {
        Assert.Equal(Path.Combine(Folder, "draft.json"), store.PathOf("draft.json"));
    }

    [Fact]
    public void Gives_a_full_path()
    {
        Assert.True(Path.IsPathFullyQualified(store.PathOf("Tasks/draft.json")));
    }

    [Fact]
    public void Accepts_a_name_that_only_contains_dots()
    {
        Assert.Equal(Path.Combine(Folder, "a..b", "..draft.json"), store.PathOf("a..b/..draft.json"));
    }

    [Fact]
    public void Gives_the_same_path_every_time_for_the_same_name()
    {
        Assert.Equal(store.PathOf("Tasks/draft.json"), store.PathOf("Tasks/draft.json"));
    }

    [Fact]
    public void Rejects_an_empty_name()
    {
        Assert.ThrowsAny<ArgumentException>(() => store.PathOf(""));
    }

    [Theory]
    [InlineData("/draft.json")]
    [InlineData("/other-folder/draft.json")]
    public void Rejects_a_name_that_is_a_rooted_path(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => store.PathOf(name));
    }

    [Fact]
    public void Rejects_a_name_that_is_a_full_path()
    {
        var fullPath = Path.Combine(Folder, "Tasks", "draft.json");

        Assert.ThrowsAny<ArgumentException>(() => store.PathOf(fullPath));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../draft.json")]
    [InlineData("Tasks/../../draft.json")]
    [InlineData("Tasks/../../other/draft.json")]
    public void Rejects_a_name_that_leads_out_of_the_folder(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => store.PathOf(name));
    }

    [Fact]
    public void Rejects_a_name_that_leads_out_of_the_folder_with_the_systems_own_separator()
    {
        var name = string.Join(Path.DirectorySeparatorChar, "..", "draft.json");

        Assert.ThrowsAny<ArgumentException>(() => store.PathOf(name));
    }
}
