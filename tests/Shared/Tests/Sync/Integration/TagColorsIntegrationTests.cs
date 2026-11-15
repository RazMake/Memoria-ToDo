using System.Text;
using System.Text.Json;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>tags.json is a managed file: Sync commits, merges, and pushes it (docs/architecture/sync.md, tasks.md).</summary>
[Integration]
public sealed class TagColorsIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Colors_picked_for_different_tags_on_two_clones_both_survive_on_both_after_syncing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        var b = world.AddDevice("b");
        await a.ConnectAsync(ct);
        await b.ConnectAsync(ct);

        a.Store.TagColors.SetColor("work", "teal");
        b.Store.TagColors.SetColor("home", "red");
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        foreach (var device in new[] { a, b })
        {
            Assert.Equal("teal", device.Store.TagColors.ColorOf("work"));
            Assert.Equal("red", device.Store.TagColors.ColorOf("home"));
        }

        using var onOrigin = JsonDocument.Parse(Encoding.UTF8.GetBytes(world.Origin.ReadOnMain("tags.json")!));
        Assert.Equal(["home", "work"], onOrigin.RootElement.GetProperty("tags").EnumerateArray().Select(e => e.GetProperty("tag").GetString()).Order().ToList());
    }
}
