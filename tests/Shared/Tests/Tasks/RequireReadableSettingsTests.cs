using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>A caller can require settings.json and tags.json to be readable, and gets SkippedFileException naming the skipped one.</summary>
[Unit]
public sealed class RequireReadableSettingsTests
{
    private readonly TasksFixture f = new();
    private int notices;

    public RequireReadableSettingsTests()
    {
        f.Store.Changed += (_, _) => notices++;
        f.Store.Settings.Changed += (_, _) => notices++;
        f.Store.TagColors.Changed += (_, _) => notices++;
        f.Store.TagOrder.Changed += (_, _) => notices++;
        f.Repository.LocalSaved += (_, _) => notices++;
    }

    private void AssertThrowsNaming(string path)
    {
        var broken = f.Files.AllFilesUnder(f.Clone).ToDictionary(file => file, file => f.Files.ReadFile(file));
        var before = notices;

        var failure = Assert.Throws<SkippedFileException>(f.Store.RequireReadableSettings);

        Assert.Equal(path, failure.File.RelativePath);
        Assert.Equal(f.Repository.SkippedFiles.Single(file => file.RelativePath == path).Reason, failure.File.Reason);
        Assert.Equal(before, notices);
        Assert.Equal(broken.Keys.Order(), f.Files.AllFilesUnder(f.Clone).Order());
        foreach (var (file, bytes) in broken)
            Assert.Equal(bytes, f.Files.ReadFile(file));
    }

    [Fact]
    public void Throws_naming_settings_json_when_it_was_skipped()
    {
        f.WriteText(f.SettingsFile, "{ not json");
        f.Tasks.UseRepositoryFolder(f.Clone);

        AssertThrowsNaming("settings.json");
    }

    [Fact]
    public void Throws_naming_tags_json_when_it_was_skipped()
    {
        f.WriteText(f.TagColorsFile, "{ not json");
        f.Tasks.UseRepositoryFolder(f.Clone);

        AssertThrowsNaming("tags.json");
    }

    [Fact]
    public void Throws_naming_settings_json_when_both_were_skipped()
    {
        f.WriteText(f.SettingsFile, "{ not json");
        f.WriteText(f.TagColorsFile, "{ not json");
        f.Tasks.UseRepositoryFolder(f.Clone);

        AssertThrowsNaming("settings.json");
    }

    [Fact]
    public void Throws_for_a_settings_file_that_needs_a_newer_app()
    {
        f.SeedSettings(version: 99);
        f.Tasks.UseRepositoryFolder(f.Clone);

        AssertThrowsNaming("settings.json");
    }

    [Fact]
    public void Does_nothing_when_both_files_are_readable()
    {
        f.SeedSettings();
        f.SeedTagColors();
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = notices;

        f.Store.RequireReadableSettings();

        Assert.Equal(before, notices);
    }

    [Fact]
    public void Does_nothing_when_both_files_are_missing_and_creates_neither()
    {
        f.Tasks.UseRepositoryFolder(f.Clone);
        var before = notices;

        f.Store.RequireReadableSettings();

        Assert.False(f.Files.FileExists(f.SettingsFile));
        Assert.False(f.Files.FileExists(f.TagColorsFile));
        Assert.Equal(before, notices);
    }

    [Fact]
    public void Does_nothing_when_no_repository_is_selected()
    {
        f.Store.RequireReadableSettings();

        Assert.Equal(0, notices);
    }

    [Fact]
    public void Does_nothing_after_the_repository_deselects_a_clone_whose_file_was_skipped()
    {
        f.WriteText(f.SettingsFile, "{ not json");
        f.Tasks.UseRepositoryFolder(f.Clone);
        f.Tasks.UseRepositoryFolder(null);

        f.Store.RequireReadableSettings();
    }

    [Fact]
    public void Does_nothing_after_a_reload_makes_the_file_readable()
    {
        f.WriteText(f.SettingsFile, "{ not json");
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Repository.Update(f.Clone, () => f.SeedSettings());

        f.Store.RequireReadableSettings();
    }

    [Fact]
    public void Throws_after_a_reload_skips_a_file_that_was_readable()
    {
        f.SeedSettings();
        f.Tasks.UseRepositoryFolder(f.Clone);

        f.Repository.Update(f.Clone, () => f.WriteText(f.TagColorsFile, "{ not json"));

        AssertThrowsNaming("tags.json");
    }
}
