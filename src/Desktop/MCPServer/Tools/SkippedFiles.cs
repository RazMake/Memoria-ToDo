using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>The files a store skipped, as the assistant sees them.</summary>
internal static class SkippedFiles
{
    public static IReadOnlyList<object> Of(IEnumerable<ISkippedRepositoryFile> files) =>
        [.. files.Select(file => new { path = file.RelativePath, reason = file.Reason })];
}
