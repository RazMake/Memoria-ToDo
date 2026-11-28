using System.Xml.Linq;
using System.Text.Json;

namespace Memoria.ToDo.Architecture;

/// <summary>One project under src/, read from its .csproj file.</summary>
internal sealed record SourceProject(
    string Name,
    string Part,
    string FolderName,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences);

internal static class Repository
{
    public static string Root { get; } = FindRoot();

    public static string ReadModulesMarkdown() =>
        File.ReadAllText(Path.Combine(Root, "docs", "architecture", "modules.md"));

    public static IReadOnlyList<string> ReleaseRoots(string platform)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, ".agents", "release-platforms.json")));
        return document.RootElement.GetProperty("platforms").GetProperty(platform).GetProperty("roots")
            .EnumerateArray().Select(root => Path.GetFileNameWithoutExtension(root.GetString()!)).ToArray();
    }

    /// <summary>The release roots and the source projects they use, without reading unrelated project files.</summary>
    public static IReadOnlyList<SourceProject> ReadSourceProjects(IEnumerable<string> roots)
    {
        var src = Path.Combine(Root, "src");
        var paths = Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .GroupBy(Path.GetFileNameWithoutExtension).ToDictionary(group => group.Key!, group => group.ToArray());
        var pending = new Queue<string>(roots);
        var found = new Dictionary<string, SourceProject>();
        while (pending.TryDequeue(out var name))
        {
            if (found.ContainsKey(name)) continue;
            var path = paths[name].Single();
            var folders = Path.GetRelativePath(src, Path.GetDirectoryName(path)!).Split(Path.DirectorySeparatorChar);
            var project = XDocument.Load(path).Root!;
            var source = new SourceProject(name, folders[0], folders[^1],
                Includes(project, "ProjectReference").Select(i => Path.GetFileNameWithoutExtension(i.Replace('\\', '/'))).ToArray(),
                Includes(project, "PackageReference").ToArray());
            found.Add(name, source);
            foreach (var reference in source.ProjectReferences) pending.Enqueue(reference);
        }
        return found.Values.OrderBy(project => project.Name, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> Includes(XElement project, string item) =>
        project.Descendants(item).Select(e => (string?)e.Attribute("Include")).OfType<string>();

    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Memoria.ToDo.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("Memoria.ToDo.slnx wasn't found above " + AppContext.BaseDirectory);
    }
}
