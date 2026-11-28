using Memoria.ToDo.Desktop;
using Xunit;

[assembly: AssemblyFixture(typeof(WarmUpWpfFirstUses))]

namespace Memoria.ToDo.Desktop;

/// <summary>
/// Before any test runs, runs the class constructors of WPF and of every themed library the tests load on one thread, so
/// parallel desktop tests can't deadlock in WPF's first-use setup (dotnet/wpf#6609).
/// </summary>
public sealed class WarmUpWpfFirstUses
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    public WarmUpWpfFirstUses() =>
        ClassConstructorWarmUp.Run(WpfWarmUpScope.Assemblies(typeof(WarmUpWpfFirstUses).Assembly).SelectMany(a => a.GetTypes()).Append(typeof(ScreenCollectionGuard)).ToList(), Bound, TimeProvider.System);
}
