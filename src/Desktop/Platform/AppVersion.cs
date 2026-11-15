using System.Reflection;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The app's version as users see it, such as 1.4.2.</summary>
public static class AppVersion
{
    /// <summary>The platform assembly's informational version, without the "+" and commit the .NET SDK adds after it.</summary>
    /// <exception cref="InvalidOperationException">The assembly has no informational version.</exception>
    public static string Value => _value ??= Read();

    private static string? _value;

    private static string Read()
    {
        var full = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException("The platform assembly has no informational version: set the build's version in Directory.Build.props.");
        var plus = full.IndexOf('+');
        return plus < 0 ? full : full[..plus];
    }
}
