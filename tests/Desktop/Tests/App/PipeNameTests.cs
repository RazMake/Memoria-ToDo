using System.Diagnostics;
using System.Security.Principal;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The name of the MCP server's pipe, which the app and its bridge both get from <c>PipeName</c>: the app's name, the
/// signed-in user's security ID, and the Windows session's number, so two sign-ins never share a pipe.
/// </summary>
[Unit]
public class PipeNameTests
{
    private const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    [Fact]
    public void Is_the_same_for_the_same_app_user_and_session()
    {
        Assert.Equal(PipeName.For("Memoria ToDo", Sid, 2), PipeName.For("Memoria ToDo", Sid, 2));
    }

    [Fact]
    public void Differs_for_another_user()
    {
        Assert.NotEqual(
            PipeName.For("Memoria ToDo", Sid, 2),
            PipeName.For("Memoria ToDo", "S-1-5-21-1111111111-2222222222-3333333333-1002", 2));
    }

    [Fact]
    public void Differs_for_another_session_of_the_same_user()
    {
        Assert.NotEqual(PipeName.For("Memoria ToDo", Sid, 2), PipeName.For("Memoria ToDo", Sid, 3));
    }

    [Fact]
    public void Differs_for_another_app_name()
    {
        Assert.NotEqual(PipeName.For("Memoria ToDo", Sid, 2), PipeName.For("Other App", Sid, 2));
    }

    [Fact]
    public void Joins_the_app_name_the_user_and_the_session()
    {
        var name = PipeName.For("Memoria ToDo", Sid, 2417);

        Assert.Contains("Memoria ToDo", name);
        Assert.Contains(Sid, name);
        Assert.Contains("2417", name);
    }

    [Fact]
    public void Is_a_name_Windows_accepts_for_a_pipe_without_a_path()
    {
        var name = PipeName.For("Memoria ToDo", Sid, 2);

        Assert.DoesNotContain('\\', name);
        Assert.DoesNotContain('/', name);
        Assert.InRange(name.Length, 1, 200);
    }

    [Fact]
    public void Of_the_current_user_joins_the_apps_name_this_users_ID_and_this_sessions_number()
    {
        using var process = Process.GetCurrentProcess();
        var expected = PipeName.For(AppName.Value, WindowsIdentity.GetCurrent().User!.Value, process.SessionId);

        Assert.Equal(expected, PipeName.ForCurrentUser());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Refuses_a_blank_app_name_or_user(string blank)
    {
        Assert.Throws<ArgumentException>(() => PipeName.For(blank, Sid, 2));
        Assert.Throws<ArgumentException>(() => PipeName.For("Memoria ToDo", blank, 2));
    }

    [Fact]
    public void Refuses_a_missing_app_name_or_user()
    {
        Assert.Throws<ArgumentNullException>(() => PipeName.For(null!, Sid, 2));
        Assert.Throws<ArgumentNullException>(() => PipeName.For("Memoria ToDo", null!, 2));
    }
}
