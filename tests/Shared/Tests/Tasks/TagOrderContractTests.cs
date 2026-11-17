using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>Runs the tag order contract on the fake, so the fake can't drift from the real one.</summary>
[Unit]
public sealed class FakeTagOrderContractTests : TagOrderContract
{
    protected override ITagOrder Order { get; } = new FakeTagOrder();
}

/// <summary>Runs the tag order contract on the real tag order of the task store.</summary>
[Unit]
public sealed class TaskStoreTagOrderContractTests : TagOrderContract
{
    private readonly TasksFixture f = new();

    public TaskStoreTagOrderContractTests()
    {
        f.Seed(TasksFixture.Id(1), tags: ["a", "b", "c"]);
        f.Tasks.UseRepositoryFolder(f.Clone);
    }

    protected override ITagOrder Order => f.Store.TagOrder;
}
