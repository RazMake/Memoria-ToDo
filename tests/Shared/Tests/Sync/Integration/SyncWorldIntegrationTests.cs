using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// Owner: technology-stack.md, "Sync's integration tests run an SSH server inside the test process". Each test makes the
/// world's cleanup fail by holding a file open in its scratch folder, then checks which failure <see cref="SyncWorld.RunAsync"/>
/// reports. The test releases the file and deletes the scratch folder itself.
/// </summary>
[Integration]
public sealed class SyncWorldIntegrationTests
{
    private sealed class ScenarioFailure : Exception;

    [Fact]
    public async Task RunAsync_reports_the_scenarios_own_failure_when_the_cleanup_also_fails()
    {
        var ownFailure = new ScenarioFailure();
        string? root = null;
        FileStream? held = null;
        try
        {
            var thrown = await Assert.ThrowsAsync<ScenarioFailure>(() => SyncWorld.RunAsync(world =>
            {
                root = world.Root;
                held = HoldFile(world.Root);
                throw ownFailure;
            }));

            Assert.Same(ownFailure, thrown);
        }
        finally
        {
            ReleaseAndDelete(held, root);
        }
    }

    [Fact]
    public async Task RunAsync_fails_with_the_cleanup_failure_naming_the_scratch_folder_when_only_the_cleanup_fails()
    {
        string? root = null;
        FileStream? held = null;
        try
        {
            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => SyncWorld.RunAsync(world =>
            {
                root = world.Root;
                held = HoldFile(world.Root);
                return Task.CompletedTask;
            }));

            Assert.NotNull(root);
            Assert.Contains(root, thrown.Message);
        }
        finally
        {
            ReleaseAndDelete(held, root);
        }
    }

    private static FileStream HoldFile(string root) =>
        new(Path.Combine(root, "held.txt"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);

    private static void ReleaseAndDelete(FileStream? held, string? root)
    {
        held?.Dispose();
        if (root is not null)
            SyncWorld.DeleteScratch(root);
    }
}
