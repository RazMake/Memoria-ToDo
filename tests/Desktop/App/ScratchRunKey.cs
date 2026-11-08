using Microsoft.Win32;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// A scratch registry key under the current user's own test branch, standing in for the real <c>Run</c> key.
/// It starts empty and is deleted with everything in it on Dispose, so no test touches the real key. The empty
/// branch stays, since tests run in parallel and another test may be using it.
/// </summary>
internal sealed class ScratchRunKey : IDisposable
{
    private const string Branch = @"Software\Memoria-ToDo-Tests";

    public ScratchRunKey()
    {
        Path = $@"{Branch}\{Guid.NewGuid():N}";
        Registry.CurrentUser.CreateSubKey(Path).Dispose();
    }

    /// <summary>The key's path under HKEY_CURRENT_USER, which an adapter takes through its constructor.</summary>
    public string Path { get; }

    /// <summary>The names of the values in the key.</summary>
    public IReadOnlyList<string> ValueNames()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path)!;
        return key.GetValueNames();
    }

    /// <summary>The text of the key's only value.</summary>
    public string OnlyValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path)!;
        var name = Assert.Single(key.GetValueNames());
        return (string)key.GetValue(name)!;
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(Path, throwOnMissingSubKey: false);
}
