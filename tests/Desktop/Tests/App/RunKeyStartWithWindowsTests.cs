using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The Run key adapter, through <see cref="IStartWithWindows"/>. The adapter, <c>RunKeyStartWithWindows</c>, takes
/// the key's path under HKEY_CURRENT_USER, the installed starter's path (null when the app isn't installed), and a
/// Serilog logger, last, through its constructor. Each test gets its own scratch key, never the real <c>Run</c> key.
/// </summary>
[Integration]
public class RunKeyStartWithWindowsTests
{
    private const string Starter = @"C:\Test Install\MemoriaToDo\Memoria-ToDo.exe";

    private readonly InMemoryLogSink log = new();

    private RunKeyStartWithWindows Adapter(ScratchRunKey key, string? starter = Starter) => new(key.Path, starter, log.CreateLogger());

    [Fact]
    public void Creating_the_adapter_without_a_logger_is_refused()
    {
        using var key = new ScratchRunKey();

        var error = Assert.Throws<ArgumentNullException>(() => new RunKeyStartWithWindows(key.Path, Starter, null!));

        Assert.Equal("logger", error.ParamName);
    }

    [Fact]
    public void Turning_it_on_logs_one_information_entry_saying_on_and_no_problem()
    {
        using var key = new ScratchRunKey();

        Adapter(key).Set(true);

        Assert.True(log.Events.SingleAt(LogEventLevel.Information).HasFlag(true));
        log.Events.AssertNoProblems();
    }

    [Fact]
    public void Turning_it_off_logs_one_information_entry_saying_off_and_no_problem()
    {
        using var key = new ScratchRunKey();
        var setting = Adapter(key);
        setting.Set(true);
        var before = log.Events.Count;

        setting.Set(false);

        Assert.True(log.After(before).SingleAt(LogEventLevel.Information).HasFlag(false));
        log.Events.AssertNoProblems();
    }

    [Fact]
    public void Reading_whether_it_is_on_logs_nothing()
    {
        using var key = new ScratchRunKey();

        Adapter(key).IsOn();

        Assert.Empty(log.Events);
    }

    [Fact]
    public void A_change_refused_because_the_app_is_not_installed_logs_no_change_and_leaves_the_failure_to_the_caller()
    {
        using var key = new ScratchRunKey();

        Assert.ThrowsAny<Exception>(() => Adapter(key, starter: null).Set(true));

        Assert.DoesNotContain(log.Events, entry => entry.Level == LogEventLevel.Information);
        log.Events.AssertNoProblems();
    }

    [Fact]
    public void Set_true_writes_the_installed_starters_path_and_the_sign_in_argument()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = Adapter(key);

        setting.Set(true);
        setting.Set(true);

        var entry = key.OnlyValue();
        Assert.Contains(Starter, entry);
        Assert.EndsWith("--at-sign-in", entry);
        Assert.True(entry.IndexOf(Starter, StringComparison.Ordinal) < entry.IndexOf("--at-sign-in", StringComparison.Ordinal));
        Assert.True(setting.IsOn());
    }

    [Fact]
    public void Set_false_removes_the_entry()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = Adapter(key);
        setting.Set(true);

        setting.Set(false);

        Assert.Empty(key.ValueNames());
        Assert.False(setting.IsOn());
    }

    [Fact]
    public void Set_false_leaves_other_programs_entries()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = Adapter(key);
        using (var open = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key.Path, writable: true)!)
        {
            open.SetValue("Another program", @"C:\Other\other.exe");
        }
        setting.Set(true);

        setting.Set(false);

        Assert.Equal(["Another program"], key.ValueNames());
    }

    [Fact]
    public void IsOn_is_false_when_the_key_holds_no_entry()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = Adapter(key);

        Assert.False(setting.IsOn());
    }

    [Fact]
    public void Set_false_fails_when_the_app_is_not_installed_and_leaves_the_entry()
    {
        using var key = new ScratchRunKey();
        Adapter(key).Set(true);
        IStartWithWindows notInstalled = Adapter(key, starter: null);

        Assert.ThrowsAny<Exception>(() => notInstalled.Set(false));

        Assert.Single(key.ValueNames());
    }

    [Fact]
    public void Set_true_fails_when_the_app_is_not_installed_and_writes_nothing()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = Adapter(key, starter: null);

        Assert.ThrowsAny<Exception>(() => setting.Set(true));

        Assert.Empty(key.ValueNames());
    }

    [Fact]
    public void IsOn_still_reads_the_entry_when_the_app_is_not_installed()
    {
        using var key = new ScratchRunKey();
        Adapter(key).Set(true);

        Assert.True(Adapter(key, starter: null).IsOn());
    }
}
