using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>A path relative to the clone, as Git names it.</summary>
[Unit]
public sealed class RepositoryPathTests
{
    [Fact]
    public void Splits_a_path_at_slashes()
    {
        Assert.Equal(["tasks", "deeper", "a.json"], RepositoryPath.Split("tasks/deeper/a.json"));
    }

    [Fact]
    public void Gives_one_part_for_a_path_with_no_slash()
    {
        Assert.Equal(["a.json"], RepositoryPath.Split("a.json"));
    }

    [Fact]
    public void Rejects_a_null_path()
    {
        Assert.Throws<ArgumentNullException>(() => RepositoryPath.Split(null!));
    }

    [Theory]
    [InlineData("/a")]
    [InlineData("\\a")]
    [InlineData("C:/a")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/./b")]
    [InlineData("a/../b")]
    public void Rejects_a_rooted_path_or_one_with_a_dot_or_double_dot_part(string path)
    {
        Assert.Throws<ArgumentException>(() => RepositoryPath.Split(path));
    }
}
