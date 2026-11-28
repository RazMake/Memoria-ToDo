using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// A sync pass first deletes the tasks that are due for automatic deletion. When that deletion was saved and then a notice failed,
/// the pass treats it as saved: it logs one Error entry and carries on, so the deletion is committed and pushed in the same pass.
/// A deletion that only partly happened still fails the pass.
/// </summary>
[Integration]
public sealed class ExpiredDeletionNoticeIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);
    private static readonly Dictionary<string, string> Seed = new() { ["README.md"] = "seed readme" };

    [Fact]
    public async Task Pushes_the_deletion_shows_synced_and_logs_one_Error_when_a_notice_fails_after_the_automatic_deletion()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new InMemoryLogSink();
        await SyncWorld.RunAsync(async world =>
        {
            var a = world.AddDevice("a", syncLogger: logs.CreateLogger());
            await a.ConnectAsync(ct);
            var task = a.AddTask("Done", "Resolved long ago");
            a.Store.Resolve(task.Id);
            Assert.Equal(SyncState.Idle, (await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);
            Assert.NotNull(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
            a.Time.Advance(TimeSpan.FromDays(30));
            var failure = new InvalidOperationException("Changed failed.");
            var thrown = false; // only the deletion's notice fails; the reloads' notices succeed
            a.Store.Changed += (_, _) =>
            {
                if (thrown) return;
                thrown = true;
                throw failure;
            };
            var before = logs.Events.Count;

            var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

            Assert.Equal(SyncState.Idle, status.State);
            Assert.Null(status.Failure);
            Assert.DoesNotContain(task.Id, a.Store.All.Select(t => t.Id));
            Assert.Null(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
            var error = Assert.Single(logs.After(before), entry => entry.Level == LogEventLevel.Error);
            Assert.IsAssignableFrom<NoticeFailedException>(error.Exception);
            Assert.True(error.HasException(failure));
        }, path => OriginRepository.CreateWith(path, Seed));
    }

    [Fact]
    public async Task Fails_the_pass_when_the_automatic_deletion_could_not_remove_a_task_s_images_and_a_notice_failed_too()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var a = world.AddDevice("a");
            await a.ConnectAsync(ct);
            var task = a.AddTask("Done", "Has an image");
            var image = Path.Combine(a.CloneFolder, "images", task.Id, "x.png");
            Directory.CreateDirectory(Path.GetDirectoryName(image)!);
            await File.WriteAllBytesAsync(image, [1, 2, 3], ct);
            a.Store.Resolve(task.Id);
            Assert.Equal(SyncState.Idle, (await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);
            a.Time.Advance(TimeSpan.FromDays(30));
            var thrown = false; // only the deletion's notice fails; the reloads' notices succeed
            a.Store.Changed += (_, _) =>
            {
                if (thrown) return;
                thrown = true;
                throw new InvalidOperationException("Changed failed.");
            };
            using var hold = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.None);

            var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

            Assert.Equal(SyncState.Error, status.State);
            Assert.NotNull(status.Failure);
        }, path => OriginRepository.CreateWith(path, Seed));
    }
}
