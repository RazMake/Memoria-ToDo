using System.IO;
using System.Security;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Platform;
using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Start with Windows as the app's entry in a <c>Run</c> registry key of the current user. The key is the only record.</summary>
internal sealed class RunKeyStartWithWindows : IStartWithWindows
{
    private readonly string _runKeyPath;
    private readonly string? _installedStarterPath;

    /// <param name="runKeyPath">The key's path under HKEY_CURRENT_USER.</param>
    /// <param name="installedStarterPath">The installed starter's path, or null when the app isn't installed, which only lets <see cref="IsOn"/> work.</param>
    public RunKeyStartWithWindows(string runKeyPath, string? installedStarterPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runKeyPath);
        if (installedStarterPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(installedStarterPath);
        }

        _runKeyPath = runKeyPath;
        _installedStarterPath = installedStarterPath;
    }

    private static string EntryName => AppName.Value;

    public bool IsOn()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        return key?.GetValue(EntryName) is not null;
    }

    public void Set(bool on)
    {
        var starter = _installedStarterPath
            ?? throw new InvalidOperationException("The app isn't installed, so Start with Windows can't be changed.");
        try
        {
            if (on)
            {
                using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath);
                key.SetValue(EntryName, $"\"{starter}\" {StartArguments.AtSignIn}");
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
                key?.DeleteValue(EntryName, throwOnMissingValue: false);
            }
        }
        catch (Exception error) when (error is UnauthorizedAccessException or SecurityException or IOException)
        {
            throw new InvalidOperationException("Windows refused the change to the Run key.", error);
        }
    }
}
