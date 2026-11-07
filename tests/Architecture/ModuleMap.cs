using System.Text.RegularExpressions;

namespace Memoria.ToDo.Architecture;

/// <summary>One row of a module-map table in docs/architecture/modules.md.</summary>
internal sealed record MapModule(string Part, string Name, IReadOnlyList<string> MayUse)
{
    /// <summary>The project name the naming rules give the module, such as <c>Memoria.ToDo.Desktop.TaskEditor</c>.</summary>
    public string ProjectName => ModuleMap.ProjectName(Part, Name);
}

/// <summary>The module map, read from the tables in docs/architecture/modules.md.</summary>
internal sealed partial class ModuleMap
{
    private static readonly string[] ExpectedHeader = ["Module", "What it does", "Design file", "May use"];

    public ModuleMap(IReadOnlyList<MapModule> modules) => Modules = modules;

    public IReadOnlyList<MapModule> Modules { get; }

    public static string ProjectName(string part, string name) => $"Memoria.ToDo.{part}.{PascalCase(name)}";

    /// <summary>The words joined without spaces, each starting with an upper-case letter: "Task editor" is "TaskEditor".</summary>
    private static string PascalCase(string words) =>
        string.Create(words.Length - words.Count(' '), words, static (result, source) =>
        {
            var next = 0;
            var startOfWord = true;
            foreach (var ch in source)
            {
                if (ch == ' ')
                {
                    startOfWord = true;
                    continue;
                }

                result[next++] = startOfWord ? char.ToUpperInvariant(ch) : ch;
                startOfWord = false;
            }
        });

    /// <summary>The module a "May use" name means for a module of <paramref name="part"/>: the part's own, else the shared library's.</summary>
    public MapModule Resolve(string part, string name) =>
        Modules.FirstOrDefault(m => m.Part == part && m.Name == name)
        ?? Modules.FirstOrDefault(m => m.Part == "Shared" && m.Name == name)
        ?? throw new InvalidOperationException($"The module map names '{name}' as used by a {part} module, but lists no such module.");

    public static ModuleMap Read(string modulesMarkdown)
    {
        var modules = new List<MapModule>();
        string? part = null;
        var inMap = false;
        var headerSeen = false;

        foreach (var raw in modulesMarkdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#'))
            {
                inMap = line == "### Module map";
                part = null;
                continue;
            }

            if (!inMap)
            {
                continue;
            }

            var lead = PartLeadIn().Match(line);
            if (lead.Success)
            {
                part = lead.Groups[1].Value switch
                {
                    "shared library" => "Shared",
                    "desktop app" => "Desktop",
                    _ => "Android",
                };
                headerSeen = false;
                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            if (part is null)
            {
                throw new InvalidOperationException($"A module-map table row has no 'The ... has N modules' sentence before it: {line}");
            }

            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (!headerSeen)
            {
                if (!cells.SequenceEqual(ExpectedHeader))
                {
                    throw new InvalidOperationException($"The module-map table's columns changed: {line}");
                }

                headerSeen = true;
                continue;
            }

            if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':')))
            {
                continue;
            }

            var mayUse = cells[3] == "None" ? [] : cells[3].Split(',').Select(n => n.Trim()).ToArray();
            modules.Add(new MapModule(part, cells[0], mayUse));
        }

        return new ModuleMap(modules);
    }

    [GeneratedRegex(@"^The (shared library|desktop app|Android app) has ")]
    private static partial Regex PartLeadIn();
}
