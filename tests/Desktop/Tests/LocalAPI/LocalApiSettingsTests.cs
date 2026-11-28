// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using System.IO;
using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>What the Settings calls answer: the deletion delay, each tag in use with its color and place, and Rendering's 30 color names.</summary>
[Unit]
public class LocalApiSettingsTests
{
    private static void UseTags(ApiHarness api, params string[] tags)
    {
        foreach (var tag in tags)
            api.Store.Tasks.Add(FakeTaskStore.Item("task-" + tag, tag: tag));
    }

    private static List<string> TagNames(V1.SharedSettings settings) => [.. settings.Tags.Select(look => look.Tag)];

    [Fact]
    public async Task GetSettings_returns_the_deletion_delay()
    {
        await using var api = new ApiHarness();
        api.Store.SettingsControl.SetDeletionDelayDays(30);

        var settings = await api.SettingsCalls.GetSettingsAsync();

        Assert.Equal(30, settings.DeletionDelayDays);
    }

    [Fact]
    public async Task GetSettings_returns_each_tag_in_use_in_the_tags_order_with_its_color_and_whether_it_is_the_default()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home", "work", "errands");
        api.Store.TagOrderControl.Seed("work", "home");
        api.Store.TagColorsControl.Seed("work", "blue-2");

        var settings = await api.SettingsCalls.GetSettingsAsync();

        Assert.Equal(["errands", "work", "home"], TagNames(settings));
        Assert.Equal(
            [(TagColors.DefaultFor("errands").Name, true), ("blue-2", false), (TagColors.DefaultFor("home").Name, true)],
            settings.Tags.Select(look => (look.Color, look.ColorIsDefault)));
    }

    [Fact]
    public async Task GetSettings_returns_Renderings_30_color_names_in_its_order()
    {
        await using var api = new ApiHarness();

        var settings = await api.SettingsCalls.GetSettingsAsync();

        Assert.Equal(30, settings.Colors.Count);
        Assert.Equal("red-1", settings.Colors[0]);
        Assert.Equal("gray-3", settings.Colors[29]);
        Assert.Equal(TagColors.All.Select(color => color.Name), settings.Colors);
    }

    [Fact]
    public async Task GetSettings_returns_no_tags_when_no_repository_is_set_but_still_the_color_names()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");
        api.Store.NoRepository = true;

        var settings = await api.SettingsCalls.GetSettingsAsync();

        Assert.Empty(settings.Tags);
        Assert.Equal(30, settings.Colors.Count);
    }

    [Fact]
    public async Task GetSettings_fails_with_FAILED_PRECONDITION_when_the_settings_file_was_skipped()
    {
        await using var api = new ApiHarness();
        api.Store.SettingsControl.ReadFailure = new InvalidDataException("settings.json can't be read.");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.GetSettingsAsync());

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task SetDeletionDelay_saves_the_days_and_returns_the_settings()
    {
        await using var api = new ApiHarness();

        var settings = await api.SettingsCalls.SetDeletionDelayAsync(45);

        Assert.Equal([45], api.Store.SettingsControl.SavedDelays);
        Assert.Equal(45, settings.DeletionDelayDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task SetDeletionDelay_outside_1_to_365_fails_with_INVALID_ARGUMENT(int days)
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetDeletionDelayAsync(days));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.SettingsControl.SavedDelays);
    }

    [Fact]
    public async Task SetDeletionDelay_fails_with_FAILED_PRECONDITION_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();
        api.Store.SettingsControl.SetFailure = new NoRepositoryFolderException();

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetDeletionDelayAsync(10));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task SetTagColor_keeps_the_color_and_returns_the_settings_with_it_marked_as_not_the_default()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");

        var settings = await api.SettingsCalls.SetTagColorAsync("home", "purple-3");

        Assert.Equal([("home", "purple-3")], api.Store.TagColorsControl.Kept);
        var look = Assert.Single(settings.Tags);
        Assert.Equal(("home", "purple-3", false), (look.Tag, look.Color, look.ColorIsDefault));
    }

    [Fact]
    public async Task SetTagColor_accepts_every_one_of_the_30_color_names()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");

        foreach (var color in TagColors.All)
            await api.SettingsCalls.SetTagColorAsync("home", color.Name);

        Assert.Equal(TagColors.All.Select(color => color.Name), api.Store.TagColorsControl.Kept.Select(kept => kept.Color));
    }

    [Theory]
    [InlineData("")]
    [InlineData("purple")]
    [InlineData("blue-4")]
    [InlineData("BLUE-1")]
    [InlineData("red-1 ")]
    public async Task SetTagColor_with_a_name_that_is_not_one_of_the_30_fails_with_INVALID_ARGUMENT_and_keeps_nothing(string color)
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetTagColorAsync("home", color));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.TagColorsControl.Kept);
    }

    [Fact]
    public async Task SetTagColor_for_an_invalid_tag_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.Store.TagColorsControl.Failure = new ArgumentException("A tag can't be empty.");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetTagColorAsync("", "red-1"));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task SetTagColor_fails_with_FAILED_PRECONDITION_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();
        api.Store.TagColorsControl.Failure = new NoRepositoryFolderException();

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetTagColorAsync("home", "red-1"));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task SetTagColor_fails_with_FAILED_PRECONDITION_when_tags_json_was_skipped()
    {
        await using var api = new ApiHarness();
        api.Store.TagColorsControl.Failure = new InvalidDataException("tags.json can't be read.");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetTagColorAsync("home", "red-1"));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task MoveTag_gives_the_tag_its_new_place_and_returns_the_tags_in_the_new_order()
    {
        await using var api = new ApiHarness();
        UseTags(api, "a", "b", "c");
        api.Store.TagOrderControl.Seed("a", "b", "c");

        var settings = await api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "c", NextTag = "a" });

        Assert.Equal([("c", null, "a")], api.Store.TagOrderControl.Moves);
        Assert.Equal(["c", "a", "b"], TagNames(settings));
    }

    [Fact]
    public async Task MoveTag_passes_both_neighbors_on()
    {
        await using var api = new ApiHarness();
        UseTags(api, "a", "b", "c");
        api.Store.TagOrderControl.Seed("a", "b", "c");

        var settings = await api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "a", PreviousTag = "b", NextTag = "c" });

        Assert.Equal([("a", "b", "c")], api.Store.TagOrderControl.Moves);
        Assert.Equal(["b", "a", "c"], TagNames(settings));
    }

    [Fact]
    public async Task MoveTag_fails_with_FAILED_PRECONDITION_when_tags_json_was_skipped()
    {
        await using var api = new ApiHarness();
        UseTags(api, "a", "b");
        api.Store.TagOrderControl.Failure = new InvalidDataException("tags.json can't be read.");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "a", NextTag = "b" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task MoveTag_fails_with_FAILED_PRECONDITION_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();
        api.Store.TagOrderControl.Failure = new NoRepositoryFolderException();

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "a", NextTag = "b" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    private static void SkipSettings(ApiHarness api, string relativePath = "tags.json") =>
        api.Store.SkippedSettingsFile = new FakeSkippedFile(relativePath, "Not JSON.");

    [Fact]
    public async Task GetSettings_asks_the_store_to_require_readable_settings_before_reading_anything()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");

        await api.SettingsCalls.GetSettingsAsync();

        Assert.Equal("RequireReadableSettings", api.Store.Calls[0]);
    }

    [Theory]
    [InlineData("tags.json")]
    [InlineData("settings.json")]
    public async Task GetSettings_fails_with_FAILED_PRECONDITION_naming_the_file_and_reads_nothing_when_the_store_says_it_was_skipped(string file)
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");
        SkipSettings(api, file);
        var callsBefore = api.Store.Calls.Count;

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.GetSettingsAsync());

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Contains(file, failure.Status.Detail);
        Assert.Equal(["RequireReadableSettings"], api.Store.Calls.Skip(callsBefore));
    }

    [Fact]
    public async Task SetDeletionDelay_fails_with_FAILED_PRECONDITION_and_saves_nothing_when_the_store_says_a_file_was_skipped()
    {
        await using var api = new ApiHarness();
        SkipSettings(api);

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetDeletionDelayAsync(45));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Contains("tags.json", failure.Status.Detail);
        Assert.Empty(api.Store.SettingsControl.SavedDelays);
    }

    [Fact]
    public async Task SetTagColor_fails_with_FAILED_PRECONDITION_and_keeps_nothing_when_the_store_says_a_file_was_skipped()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");
        SkipSettings(api, "settings.json");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetTagColorAsync("home", "red-1"));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Contains("settings.json", failure.Status.Detail);
        Assert.Empty(api.Store.TagColorsControl.Kept);
    }

    [Fact]
    public async Task MoveTag_fails_with_FAILED_PRECONDITION_and_moves_nothing_when_the_store_says_a_file_was_skipped()
    {
        await using var api = new ApiHarness();
        UseTags(api, "a", "b");
        SkipSettings(api);

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "a", NextTag = "b" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
        Assert.Empty(api.Store.TagOrderControl.Moves);
    }

    [Fact]
    public async Task SetTagColor_with_an_invalid_color_fails_with_INVALID_ARGUMENT_before_the_store_is_asked_about_skipped_files()
    {
        await using var api = new ApiHarness();
        UseTags(api, "home");
        SkipSettings(api);
        var callsBefore = api.Store.Calls.Count;

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.SetTagColorAsync("home", "purple"));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.Calls.Skip(callsBefore));
    }

    [Fact]
    public async Task MoveTag_with_a_neighbor_that_is_not_next_to_the_place_fails_with_INVALID_ARGUMENT()
    {
        await using var api = new ApiHarness();
        api.Store.TagOrderControl.Failure = new ArgumentException("The tags aren't neighbors.");

        var failure = await CallOutcome.FailsAsync(api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "a", PreviousTag = "z", NextTag = "b" }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }
}
