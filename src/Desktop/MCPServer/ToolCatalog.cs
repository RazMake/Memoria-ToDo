using System.Collections.Concurrent;
using System.Reflection;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>Shares tool definitions while resolving each call's tool group from its own server.</summary>
internal sealed class ToolCatalog : IServiceProvider
{
    private static readonly ConcurrentDictionary<Type, Lazy<IReadOnlyList<McpServerTool>>> Definitions = new();

    private readonly Dictionary<Type, object> _groups;

    /// <summary>Takes one instance of each tool-group type for this server.</summary>
    public ToolCatalog(IEnumerable<object> groups)
    {
        _groups = groups.ToDictionary(group => group.GetType());
        Tools =
        [
            .. _groups.Keys.SelectMany(type => Definitions.GetOrAdd(type,
                static key => new Lazy<IReadOnlyList<McpServerTool>>(
                    () => DefineTools(key), LazyThreadSafetyMode.ExecutionAndPublication)).Value),
        ];
    }

    /// <summary>One tool for each marked method of each group, named, described, and flagged as its attributes say.</summary>
    public IReadOnlyList<McpServerTool> Tools { get; }

    /// <inheritdoc />
    public object? GetService(Type serviceType) => _groups.GetValueOrDefault(serviceType);

    private static IReadOnlyList<McpServerTool> DefineTools(Type type) =>
    [
        .. type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => McpServerTool.Create(method, context =>
                context.Services?.GetService(type)
                ?? throw new InvalidOperationException($"The server has no tool group {type.Name}."))),
    ];
}
