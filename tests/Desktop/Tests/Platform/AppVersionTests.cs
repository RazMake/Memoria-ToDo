using System.Reflection;
using System.Text.RegularExpressions;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>The app's version, read from the platform assembly's informational version.</summary>
[Unit]
public partial class AppVersionTests
{
    [Fact]
    public void The_version_holds_no_plus_or_commit()
    {
        Assert.DoesNotContain('+', AppVersion.Value);
    }

    [Fact]
    public void The_version_is_the_assemblys_informational_version_before_its_plus()
    {
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.Equal(informational.Split('+')[0], AppVersion.Value);
    }

    [Fact]
    public void The_version_is_numbers_with_dots()
    {
        Assert.Matches(Numbers(), AppVersion.Value);
    }

    [GeneratedRegex(@"^\d+(\.\d+)+")]
    private static partial Regex Numbers();
}
