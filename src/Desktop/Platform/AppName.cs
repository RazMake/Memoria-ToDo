using System.Reflection;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The app's name as users see it, such as in the error box's title and the tray icon's tooltip.</summary>
public static class AppName
{
    /// <summary>The name. Its only home is the build's Product property in Directory.Build.props.</summary>
    /// <exception cref="InvalidOperationException">The platform assembly has no product attribute, as when the build doesn't generate assembly info.</exception>
    public static string Value => _value ??= Read();

    private static string? _value;

    private static string Read() =>
        typeof(AppName).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? throw new InvalidOperationException("The platform assembly has no product attribute: set the build's Product property in Directory.Build.props.");
}
