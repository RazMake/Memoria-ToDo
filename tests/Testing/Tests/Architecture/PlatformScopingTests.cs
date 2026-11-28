using Memoria.ToDo.Architecture;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Testing.Architecture;

[Unit]
public sealed class PlatformScopingTests
{
    [Fact]
    public void Androids_map_does_not_parse_a_broken_desktop_table()
    {
        const string map = """
            ### Module map
            The desktop app has 1 module.
            | A broken header |
            The Android app has 1 module.
            | Module | What it does | Design file | May use |
            | --- | --- | --- | --- |
            | Configure | Shows settings | configure.md | None |
            """;

        var result = ModuleMap.Read(map, new HashSet<string> { "Android" });

        Assert.Equal("Memoria.ToDo.Android.Configure", Assert.Single(result.Modules).ProjectName);
    }

    [Fact]
    public void Androids_map_resolves_shared_dependencies_without_selecting_shared_or_desktop_checks()
    {
        const string map = """
            ### Module map
            The desktop app has 1 module.
            | A broken header |
            The shared library has 1 module.
            | Module | What it does | Design file | May use |
            | --- | --- | --- | --- |
            | Tasks | Holds tasks | tasks.md | None |
            The Android app has 1 module.
            | Module | What it does | Design file | May use |
            | --- | --- | --- | --- |
            | Configure | Shows settings | configure.md | Tasks |
            """;

        var result = ModuleMap.Read(map, new HashSet<string> { "Android" });

        Assert.Equal("Memoria.ToDo.Android.Configure", Assert.Single(result.Modules).ProjectName);
        Assert.Equal("Memoria.ToDo.Shared.Tasks", result.Resolve("Android", "Tasks").ProjectName);
    }
}
