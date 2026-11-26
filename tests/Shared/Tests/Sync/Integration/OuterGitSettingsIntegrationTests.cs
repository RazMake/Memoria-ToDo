using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// Owner: sync.md, "Sync reads only its clone's own git settings". Each test connects a device first, so Sync has already run git,
/// then gives libgit2 bad settings outside the clone and checks that they change nothing Sync does.
/// </summary>
[Integration]
[Collection(OuterGitSettings.Collection)]
public sealed class OuterGitSettingsIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    private const string StrictLineEndings = "[core]\n\tautocrlf = true\n\tsafecrlf = true\n";

    [Fact]
    public async Task A_sync_pass_pushes_a_task_and_settings_with_their_line_breaks_unchanged_when_outer_git_settings_convert_line_endings()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var device = world.AddDevice("a");
            await device.ConnectAsync(ct);
            var task = device.AddTask("Buy milk", "Two litres");

            ISyncStatus status;
            using (new OuterGitSettings(StrictLineEndings))
            {
                status = await device.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
            }

            Assert.True(status.Failure is null, $"The pass failed: {status.Failure}: {status.Details}");
            foreach (var file in new[] { $"tasks/{task.Id}.json", "settings.json" })
            {
                var pushed = world.Origin.ReadBytesOnMain(file);
                Assert.NotNull(pushed);
                Assert.Contains((byte)'\n', pushed);
                Assert.DoesNotContain((byte)'\r', pushed);
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(device.CloneFolder, file), ct), pushed);
            }
        });
    }

    [Fact]
    public async Task A_second_devices_clone_has_the_origins_files_byte_for_byte_when_outer_git_settings_convert_line_endings()
    {
        var ct = TestContext.Current.CancellationToken;
        string[] files = ["root.txt", "notes/lines.txt"];
        await SyncWorld.RunAsync(async world =>
        {
            var first = world.AddDevice("a");
            var second = world.AddDevice("b");
            await first.ConnectAsync(ct);

            using (new OuterGitSettings(StrictLineEndings))
            {
                await second.ConnectAsync(ct);
            }

            foreach (var file in files)
            {
                var origin = world.Origin.ReadBytesOnMain(file);
                Assert.Equal("one\ntwo\n"u8.ToArray(), origin);
                Assert.Equal(origin, await File.ReadAllBytesAsync(Path.Combine(second.CloneFolder, file), ct));
            }
        }, path => OriginRepository.CreateWith(path,
            files.ToDictionary(file => file, _ => "one\ntwo\n")));
    }

    [Fact]
    public async Task A_setup_refresh_shows_the_saved_origin_address_and_the_next_pass_succeeds_when_an_outer_url_rewrite_changes_it_to_one_sync_rejects()
    {
        var ct = TestContext.Current.CancellationToken;
        await SyncWorld.RunAsync(async world =>
        {
            var device = world.AddDevice("a");
            await device.ConnectAsync(ct);
            var saved = device.Sync.Setup.State.RepositoryAddress;
            Assert.Equal(world.Address, saved);
            var prefix = world.Address[..^"origin.git".Length];

            ISyncStatus status;
            string? shown;
            using (new OuterGitSettings($"[url \"https://example.invalid/\"]\n\tinsteadOf = {prefix}\n"))
            {
                await device.Sync.Setup.SetDeviceNameAsync("laptop", ct);
                shown = device.Sync.Setup.State.RepositoryAddress;
                status = await device.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
            }

            Assert.Equal(saved, shown);
            Assert.True(status.Failure is null, $"The pass failed: {status.Failure}: {status.Details}");
            Assert.Equal(saved, device.Sync.Setup.State.RepositoryAddress);
        });
    }
}
