using System.IO;
using System.Text.Json;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>Making the app's log: where its folder is, and what it refuses before it does anything else.</summary>
[Unit]
public sealed class AppLogSettingsTests
{
    [Fact]
    public void Keeps_its_files_in_the_logs_folder_of_the_apps_folder_under_LocalAppData()
    {
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName.Value, "logs"),
            AppLog.Folder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_missing_folder_name_before_it_writes_anything(string? folder)
    {
        Assert.ThrowsAny<ArgumentException>(() => AppLog.Create(folder!));
    }
}

/// <summary>The app's log on a real folder: the files it writes, their names and limits, and what a line holds.</summary>
[Integration]
public sealed class AppLogFilesTests : IDisposable
{
    private const long OneMegabyte = 1024 * 1024;

    private readonly LogFolder _folder = new();
    private readonly DateTime _today = DateTime.Now; // Taken before every write, so it's the day the sink writes or the day before it.

    public void Dispose() => _folder.Dispose();

    /// <summary>The name the sink gives a file written in this test: for the day before the write or the day after it, as midnight may pass between.</summary>
    private string[] WrittenNames(int? number = null) =>
        [.. new[] { LogFolder.FileName(_today, number), LogFolder.FileName(DateTime.Now, number) }.Distinct()];

    [Fact]
    public void Writes_to_a_file_named_after_todays_local_date_and_makes_the_folder_when_it_is_missing()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("App").Information("Started");
        }

        Assert.Contains(Assert.Single(_folder.Names()), WrittenNames());
        Assert.Single(_folder.Lines());
    }

    [Fact]
    public void Starts_the_next_numbered_file_once_a_file_reaches_1_MB_and_writes_the_crossing_entry_whole()
    {
        var text = new string('x', 100_000);
        using (var log = AppLog.Create(_folder.Path))
        {
            for (var number = 0; number < 30; number++)
            {
                log.ForModule("App").Information("Entry {Number} {Text}", number, text);
            }
        }

        var names = _folder.Names();
        Assert.Contains(names[0], WrittenNames());
        Assert.Contains(names[1], WrittenNames(1));
        Assert.True(names.Length >= 3, string.Join(", ", names));
        Assert.All(names, name =>
        {
            var content = File.ReadAllText(Path.Combine(_folder.Path, name));
            var beforeLastLine = content.TrimEnd('\r', '\n').LastIndexOf('\n') + 1;
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(content.AsSpan(0, beforeLastLine)) < OneMegabyte, name);
        });
        Assert.Equal(30, _folder.Lines().Length);
    }

    [Fact]
    public void Deletes_the_files_of_days_more_than_14_days_ago_when_it_starts_and_keeps_newer_ones()
    {
        _folder.Leave(LogFolder.FileName(_today.AddDays(-20)));
        _folder.Leave(LogFolder.FileName(_today.AddDays(-15)));
        _folder.Leave(LogFolder.FileName(_today.AddDays(-13)));
        _folder.Leave(LogFolder.FileName(_today.AddDays(-5)));

        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("App").Information("Started");
        }

        var names = _folder.Names();
        Assert.Equal(3, names.Length);
        Assert.Equal([LogFolder.FileName(_today.AddDays(-13)), LogFolder.FileName(_today.AddDays(-5))], names[..2]);
        Assert.Contains(names[2], WrittenNames());
    }

    [Fact]
    public void Keeps_at_most_30_files_dropping_the_oldest_and_never_deletes_the_file_it_writes()
    {
        var older = _today.AddDays(-10);
        var newer = _today.AddDays(-5);
        _folder.Leave(LogFolder.FileName(older));
        _folder.Leave(LogFolder.FileName(newer));
        for (var number = 1; number < 20; number++)
        {
            _folder.Leave(LogFolder.FileName(older, number));
            _folder.Leave(LogFolder.FileName(newer, number));
        }

        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("App").Information("The marker entry");
        }

        var names = _folder.Names();
        Assert.Equal(30, names.Length);
        Assert.All(
            Enumerable.Range(0, 20).Select(number => LogFolder.FileName(newer, number == 0 ? null : number)),
            name => Assert.Contains(name, names));
        Assert.Contains(_folder.Lines(), line => line.Contains("The marker entry", StringComparison.Ordinal));
    }

    [Fact]
    public void Writes_each_entry_as_one_compact_json_line_with_the_time_in_UTC_the_template_level_component_and_exception()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("Sync").Warning(new IOException("disk boom"), "Skipped {Path}", "tasks/a.json");
        }

        var line = Assert.Single(_folder.Lines());
        using var json = JsonDocument.Parse(line);
        var entry = json.RootElement;
        Assert.EndsWith("Z", entry.GetProperty("@t").GetString());
        Assert.Equal("Skipped {Path}", entry.GetProperty("@mt").GetString());
        Assert.Equal("Warning", entry.GetProperty("@l").GetString());
        Assert.Contains("disk boom", entry.GetProperty("@x").GetString());
        Assert.Equal("Sync", entry.GetProperty("SourceContext").GetString());
        Assert.Equal("tasks/a.json", entry.GetProperty("Path").GetString());
    }

    [Fact]
    public void Writes_an_entry_with_an_exception_on_one_line_though_the_exception_text_spans_lines()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            try
            {
                throw new InvalidOperationException("first line\nsecond line");
            }
            catch (InvalidOperationException failure)
            {
                log.ForModule("App").Error(failure, "Failed");
            }
        }

        var line = Assert.Single(_folder.Lines());
        Assert.Contains("second line", JsonDocument.Parse(line).RootElement.GetProperty("@x").GetString());
    }

    [Fact]
    public void Keeps_no_secret_in_the_file()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("Sync").Error(
                new InvalidOperationException("git failed for ssh://git:hunter2@example.com/repo.git"),
                "Cloning {Address}",
                "ssh://git:hunter2@example.com/repo.git");
        }

        var line = Assert.Single(_folder.Lines());
        Assert.DoesNotContain("hunter2", line);
        Assert.Contains("***", line);
        JsonDocument.Parse(line).Dispose();
    }

    [Fact]
    public void Writes_every_entry_still_queued_when_disposed()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            for (var number = 0; number < 500; number++)
            {
                log.ForModule("App").Information("Entry {Number}", number);
            }
        }

        Assert.Equal(500, _folder.Lines().Length);
    }

    [Fact]
    public void Does_not_write_Verbose_or_Debug_entries()
    {
        using (var log = AppLog.Create(_folder.Path))
        {
            log.ForModule("App").Verbose("A detail");
            log.ForModule("App").Debug("Another detail");
            log.ForModule("App").Information("Kept");
        }

        Assert.Contains("Kept", Assert.Single(_folder.Lines()));
    }

    [Fact]
    public void Writes_to_the_next_numbered_file_when_todays_file_is_held_by_another_process()
    {
        // Both days a write may fall on are held, as midnight may pass before the sink opens its file.
        var held = new[] { _today, _today.AddDays(1) }
            .Select(day => _folder.Leave(LogFolder.FileName(day)))
            .Select(path => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            .ToList();
        try
        {
            using var log = AppLog.Create(_folder.Path);
            log.ForModule("App").Information("Written beside the held file");
        }
        finally
        {
            held.ForEach(file => file.Dispose());
        }

        var written = _folder.Names().Where(name => name.Contains("_001", StringComparison.Ordinal)).ToList();
        Assert.Contains(Assert.Single(written), WrittenNames(1));
        Assert.Contains("Written beside the held file", File.ReadAllText(Path.Combine(_folder.Path, written[0])));
    }

    [Fact]
    public void Writes_a_Warning_naming_an_old_file_it_cannot_delete_keeps_the_file_and_reports_no_write_problem()
    {
        var oldName = LogFolder.FileName(_today.AddDays(-20));
        var old = _folder.Leave(oldName);
        using (new FileStream(old, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            using var log = AppLog.Create(_folder.Path);
            log.ForModule("App").Information("Written beside the stuck file");
            log.Dispose(); // Writes what is queued before the problem is read.
            Assert.Null(log.WriteProblem);
        }

        Assert.True(File.Exists(old));
        var warnings = _folder.Lines()
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Where(entry => entry.TryGetProperty("@l", out var level) && level.GetString() == "Warning")
            .Where(entry => entry.GetRawText().Contains(oldName, StringComparison.Ordinal))
            .ToList();
        Assert.Single(warnings);
    }

    [Fact]
    public void Keeps_the_app_running_when_the_folder_cannot_be_made_and_says_why_as_the_write_problem()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_folder.Path)!);
        File.WriteAllText(_folder.Path, "A file where the log folder should be.");
        var log = AppLog.Create(_folder.Path);

        log.ForModule("App").Information("Cannot be written");
        log.Dispose();

        Assert.False(string.IsNullOrWhiteSpace(log.WriteProblem));
    }
}
