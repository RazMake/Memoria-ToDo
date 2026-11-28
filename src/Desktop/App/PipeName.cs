using System.Diagnostics;
using System.Security.Principal;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The name of the MCP server's pipe, which the app and its bridge both get here, so two sign-ins never share a pipe.</summary>
internal static class PipeName
{
    /// <summary>The app's name, the user's security ID, and the Windows session's number, joined.</summary>
    public static string For(string appName, string userSid, int sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        return $"{appName}.Mcp.{userSid}.{sessionId}";
    }

    /// <summary>The pipe name for the signed-in user's current session.</summary>
    public static string ForCurrentUser()
    {
        using var process = Process.GetCurrentProcess();
        return For(AppName.Value, WindowsIdentity.GetCurrent().User!.Value, process.SessionId);
    }
}
