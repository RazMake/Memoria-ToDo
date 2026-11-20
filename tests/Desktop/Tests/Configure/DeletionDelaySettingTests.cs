using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The deletion delay behind its box: what it shows, how it validates text, and what it says when reading or
/// saving fails. The message wording is part of the spec, so the tests state it literally.
/// </summary>
[Unit]
public class DeletionDelaySettingTests
{
    private static (DeletionDelaySetting Setting, FakeSharedTaskSettings Settings) Open()
    {
        var settings = new FakeSharedTaskSettings();
        var setting = new DeletionDelaySetting(settings, Serilog.Core.Logger.None);
        setting.Refresh();
        return (setting, settings);
    }

    [Fact]
    public void Shows_the_stored_delay_after_a_refresh()
    {
        var (setting, settings) = Open();
        settings.SetDeletionDelayDays(20);

        setting.Refresh();

        Assert.Equal("20", setting.Shown);
        Assert.Null(setting.Message);
    }

    [Fact]
    public void Saves_a_valid_delay_and_shows_it()
    {
        var (setting, settings) = Open();

        setting.Commit("30");

        Assert.Equal([30], settings.SavedDelays);
        Assert.Equal("30", setting.Shown);
        Assert.Null(setting.Message);
    }

    [Fact]
    public void Trims_the_text_before_saving()
    {
        var (setting, settings) = Open();

        setting.Commit("  30 ");

        Assert.Equal([30], settings.SavedDelays);
    }

    [Theory]
    [InlineData("many")]
    [InlineData("")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void Text_that_is_not_a_whole_number_changes_nothing_and_says_so(string text)
    {
        var (setting, settings) = Open();

        setting.Commit(text);

        Assert.Empty(settings.SavedDelays);
        Assert.Equal("7", setting.Shown);
        Assert.Equal($"The deletion delay wasn't changed: \"{text}\" isn't a whole number of days.", setting.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    public void A_number_outside_1_to_365_changes_nothing_and_says_so(string text)
    {
        var (setting, settings) = Open();

        setting.Commit(text);

        Assert.Empty(settings.SavedDelays);
        Assert.Equal("7", setting.Shown);
        Assert.Equal($"The deletion delay wasn't changed: {text} is outside the allowed range.", setting.Message);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("365")]
    public void Accepts_the_ends_of_the_range(string text)
    {
        var (setting, _) = Open();

        setting.Commit(text);

        Assert.Equal(text, setting.Shown);
        Assert.Null(setting.Message);
    }

    [Fact]
    public void A_later_valid_delay_clears_the_message_about_an_invalid_one()
    {
        var (setting, _) = Open();
        setting.Commit("many");

        setting.Commit("12");

        Assert.Null(setting.Message);
    }

    [Fact]
    public void A_save_the_system_refuses_keeps_the_shown_delay_and_shows_why()
    {
        var (setting, settings) = Open();
        settings.SetFailure = new UnauthorizedAccessException("Access denied.");

        setting.Commit("30");

        Assert.Equal("7", setting.Shown);
        Assert.Equal("The deletion delay wasn't saved: Access denied.", setting.Message);
    }

    [Fact]
    public void Unreadable_settings_when_saving_keep_the_shown_delay_and_show_why()
    {
        var (setting, settings) = Open();
        settings.SetFailure = new InvalidDataException("not json");

        setting.Commit("30");

        Assert.Equal("7", setting.Shown);
        Assert.Equal("The deletion delay wasn't saved: not json", setting.Message);
    }

    [Fact]
    public void Unreadable_settings_at_the_start_show_no_invented_delay_and_say_why()
    {
        var settings = new FakeSharedTaskSettings { ReadFailure = new InvalidDataException("not json") };
        var setting = new DeletionDelaySetting(settings, Serilog.Core.Logger.None);

        var failure = Record.Exception(setting.Refresh);

        Assert.Null(failure);
        Assert.Equal("", setting.Shown);
        Assert.Equal("The deletion delay couldn't be read: not json", setting.Message);
    }

    [Fact]
    public void Settings_that_become_unreadable_keep_the_shown_delay_and_say_why()
    {
        var (setting, settings) = Open();
        settings.ReadFailure = new InvalidDataException("not json");

        setting.Refresh();

        Assert.Equal("7", setting.Shown);
        Assert.Equal("The deletion delay couldn't be read: not json", setting.Message);
    }

    [Fact]
    public void A_later_readable_refresh_shows_the_stored_delay_and_removes_the_read_failure()
    {
        var (setting, settings) = Open();
        settings.ReadFailure = new InvalidDataException("not json");
        setting.Refresh();
        settings.ReadFailure = null;
        settings.SetDeletionDelayDays(20);

        setting.Refresh();

        Assert.Equal("20", setting.Shown);
        Assert.Null(setting.Message);
    }

    [Fact]
    public void A_refresh_keeps_the_message_about_a_rejected_text()
    {
        var (setting, _) = Open();
        setting.Commit("many");

        setting.Refresh();

        Assert.NotNull(setting.Message);
    }
}
