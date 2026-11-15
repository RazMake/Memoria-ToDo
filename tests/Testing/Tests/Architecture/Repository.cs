using System.Xml.Linq;

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

    /// <summary>Every project under src/, with the project and package references its .csproj lists.</summary>
    public static IReadOnlyList<SourceProject> ReadSourceProjects()
    {
        var src = Path.Combine(Root, "src");
        return Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Select(path =>
            {
                var folders = Path.GetRelativePath(src, Path.GetDirectoryName(path)!).Split(Path.DirectorySeparatorChar);
                var project = XDocument.Load(path).Root!;
                return new SourceProject(
                    Path.GetFileNameWithoutExtension(path),
                    folders[0],
                    folders[^1],
                    Includes(project, "ProjectReference").Select(i => Path.GetFileNameWithoutExtension(i.Replace('\\', '/'))).ToArray(),
                    Includes(project, "PackageReference").ToArray());
            })
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();
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
