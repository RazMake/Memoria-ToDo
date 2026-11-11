using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Compares tasks by what they hold; a task's tags are a list, so record equality would compare list references.</summary>
internal static class TaskAssert
{
    public static void SameTask(TaskItem expected, TaskItem actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Rank, actual.Rank);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.Created, actual.Created);
        Assert.Equal(expected.Updated, actual.Updated);
        Assert.Equal(expected.Resolved, actual.Resolved);
    }
}
