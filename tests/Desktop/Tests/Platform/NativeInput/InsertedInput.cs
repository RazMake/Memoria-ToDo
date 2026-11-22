using System.ComponentModel;

namespace Memoria.ToDo.Desktop.Platform.Tests;

internal static class InsertedInput
{
    public static void Require(uint inserted, int requested, int win32Error)
    {
        if (inserted != requested)
        {
            throw new Win32Exception(win32Error, $"Windows inserted {inserted} of {requested} input events.");
        }
    }
}
