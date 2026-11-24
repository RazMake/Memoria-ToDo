using System.Reflection;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>Finds the tools in the tool groups: every public method marked <see cref="McpServerToolAttribute"/>.</summary>
internal static class ToolCatalog
{
    /// <summary>One tool for each marked method of each group, named, described, and flagged as its attributes say.</summary>
    public static IReadOnlyList<McpServerTool> Of(IEnumerable<object> groups) =>
    [
        .. groups.SelectMany(group => group.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => McpServerTool.Create(method, group))),
    ];
}
