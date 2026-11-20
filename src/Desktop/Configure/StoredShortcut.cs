using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Reads a row's shortcut from its stored text, and checks a pick against the other rows' shortcuts.</summary>
internal static class StoredShortcut
{
    /// <summary>
    /// The stored shortcut, the default when nothing is stored, or null with the reason in <paramref name="problem"/>
    /// when the text can't be read or isn't a valid shortcut.
    /// </summary>
    public static Shortcut? Read(
        string label, Shortcut defaultShortcut, Func<string?> readStored, Func<string?, Shortcut?> parse,
        ILogger logger, out string? problem)
    {
        problem = null;
        try
        {
            var text = readStored();
            if (text is null)
            {
                return defaultShortcut;
            }

            var shortcut = parse(text);
            problem = shortcut is null ? $"The saved {label} shortcut \"{text}\" isn't a valid shortcut." : null;
            return shortcut;
        }
        catch (Exception error)
        {
            logger.Error(error, "The saved {Label} shortcut couldn't be read", label);
            problem = $"The saved {label} shortcut couldn't be read: {error.Message}";
            return null;
        }
    }

    /// <summary>The message naming the first row that already uses the shortcut, or null when none does.</summary>
    public static string? UsedBy(Shortcut shortcut, IUsedShortcut[] others)
    {
        var other = Array.Find(others, row => row.Active == shortcut);
        return other is null
            ? null
            : $"{other.Label} already uses {shortcut.ToDisplayText()}. Press a different shortcut.";
    }
}
