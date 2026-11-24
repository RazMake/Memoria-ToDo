using Memoria.ToDo.Desktop;
using Xunit;

[assembly: AssemblyFixture(typeof(FailRunOnUnclaimedWindowRefusals))]

namespace Memoria.ToDo.Desktop;

/// <summary>At the end of the run, fails it for every refused window that no test took: one that loaded with no test running or after its test's hook.</summary>
public sealed class FailRunOnUnclaimedWindowRefusals : IDisposable
{
    public void Dispose() => WindowRefusals.Shared.ThrowUnclaimed();
}
