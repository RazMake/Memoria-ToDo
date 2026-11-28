using System.Reflection;
using System.Windows;

namespace Memoria.ToDo.Desktop;

/// <summary>Chooses the assemblies the WPF warm-up covers.</summary>
internal static class WpfWarmUpScope
{
    // The runtime's and Windows desktop libraries' signing keys. They carry no themes of their own beyond WPF's four assemblies, and
    // their references include optional ones that aren't installed, so the walk neither loads nor follows them.
    private static readonly HashSet<string> FrameworkKeyTokens = new(StringComparer.Ordinal)
    {
        "B77A5C561934E089", "B03F5F7F11D50A3A", "31BF3856AD364E35", "CC7B13FFCD2DDD51", "7CEC85D7BEA7798E",
    };

    /// <summary>
    /// WPF's four assemblies, and every assembly <paramref name="testAssembly"/> references, directly or through other
    /// references, that carries <c>System.Windows.ThemeInfoAttribute</c>. Assemblies signed with the .NET runtime's own keys are not followed.
    /// </summary>
    internal static IReadOnlyCollection<Assembly> Assemblies(Assembly testAssembly)
    {
        var scope = new HashSet<Assembly>
        {
            typeof(DependencyObject).Assembly, // WindowsBase
            typeof(UIElement).Assembly, // PresentationCore
            typeof(FrameworkElement).Assembly, // PresentationFramework
            typeof(System.Xaml.XamlReader).Assembly, // System.Xaml
        };

        var visited = new HashSet<string>(StringComparer.Ordinal) { testAssembly.FullName! };
        var pending = new Stack<Assembly>();
        pending.Push(testAssembly);
        while (pending.Count > 0)
        {
            foreach (var reference in pending.Pop().GetReferencedAssemblies())
            {
                if (!visited.Add(reference.FullName) || FrameworkKeyTokens.Contains(Convert.ToHexString(reference.GetPublicKeyToken() ?? [])))
                {
                    continue;
                }

                var assembly = Assembly.Load(reference);
                if (assembly.IsDefined(typeof(ThemeInfoAttribute)))
                {
                    scope.Add(assembly);
                }

                pending.Push(assembly);
            }
        }

        return scope;
    }
}
