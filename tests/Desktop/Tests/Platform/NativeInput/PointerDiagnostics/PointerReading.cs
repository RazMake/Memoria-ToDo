using System.ComponentModel;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Writes one diagnostic reading, so a reading Windows refuses shows as unavailable instead of replacing a move's failure.</summary>
internal static class PointerReading
{
    /// <summary>
    /// The name with the value <paramref name="read"/> returns. When it throws <see cref="System.ComponentModel.Win32Exception"/>,
    /// the name with "unavailable" and the Win32 error code instead; the exception is not rethrown.
    /// </summary>
    public static string Of(string name, Func<string> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            return $"{name} {read()}";
        }
        catch (Win32Exception refused)
        {
            return $"{name} unavailable (error {refused.NativeErrorCode})";
        }
    }
}