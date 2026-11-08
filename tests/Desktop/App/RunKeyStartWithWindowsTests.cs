using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The Run key adapter, through <see cref="IStartWithWindows"/>. The adapter, <c>RunKeyStartWithWindows</c>, takes
/// the key's path under HKEY_CURRENT_USER and the installed starter's path (null when the app isn't installed)
/// through its constructor. Each test gets its own scratch key, never the real <c>Run</c> key.
/// </summary>
[Integration]
public class RunKeyStartWithWindowsTests
{
    private const string Starter = @"C:\Test Install\MemoriaToDo\Memoria-ToDo.exe";

    [Fact]
    public void Set_true_writes_the_installed_starters_path_and_the_sign_in_argument()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);

        setting.Set(true);

        var entry = key.OnlyValue();
        Assert.Contains(Starter, entry);
        Assert.EndsWith("--at-sign-in", entry);
        Assert.True(entry.IndexOf(Starter, StringComparison.Ordinal) < entry.IndexOf("--at-sign-in", StringComparison.Ordinal));
    }

    [Fact]
    public void Set_true_twice_keeps_one_entry()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);

        setting.Set(true);
        setting.Set(true);

        Assert.Single(key.ValueNames());
    }

    [Fact]
    public void Set_false_removes_the_entry()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);
        setting.Set(true);

        setting.Set(false);

        Assert.Empty(key.ValueNames());
    }

    [Fact]
    public void Set_false_when_there_is_no_entry_does_nothing()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);

        setting.Set(false);

        Assert.Empty(key.ValueNames());
    }

    [Fact]
    public void Set_false_leaves_other_programs_entries()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);
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
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);

        Assert.False(setting.IsOn());
    }

    [Fact]
    public void IsOn_reads_the_entry_so_another_adapter_on_the_same_key_sees_it()
    {
        using var key = new ScratchRunKey();
        new RunKeyStartWithWindows(key.Path, Starter).Set(true);

        Assert.True(new RunKeyStartWithWindows(key.Path, Starter).IsOn());
    }

    [Fact]
    public void IsOn_is_false_after_Set_false()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, Starter);
        setting.Set(true);

        setting.Set(false);

        Assert.False(setting.IsOn());
    }

    [Fact]
    public void Set_true_fails_when_the_app_is_not_installed_and_writes_nothing()
    {
        using var key = new ScratchRunKey();
        IStartWithWindows setting = new RunKeyStartWithWindows(key.Path, installedStarterPath: null);

        Assert.ThrowsAny<Exception>(() => setting.Set(true));

        Assert.Empty(key.ValueNames());
    }

    [Fact]
    public void Set_false_fails_when_the_app_is_not_installed_and_leaves_the_entry()
    {
        using var key = new ScratchRunKey();
        new RunKeyStartWithWindows(key.Path, Starter).Set(true);
        IStartWithWindows notInstalled = new RunKeyStartWithWindows(key.Path, installedStarterPath: null);

        Assert.ThrowsAny<Exception>(() => notInstalled.Set(false));

        Assert.Single(key.ValueNames());
    }

    [Fact]
    public void IsOn_still_reads_the_entry_when_the_app_is_not_installed()
    {
        using var key = new ScratchRunKey();
        new RunKeyStartWithWindows(key.Path, Starter).Set(true);

        Assert.True(new RunKeyStartWithWindows(key.Path, installedStarterPath: null).IsOn());
    }
}
