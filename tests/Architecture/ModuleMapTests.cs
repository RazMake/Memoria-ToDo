using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Architecture;

[Integration]
public sealed class The_module_map_and_the_projects
{
    private static readonly string[] Parts = ["Shared", "Desktop", "Android"];

    private readonly ModuleMap _map = ModuleMap.Read(Repository.ReadModulesMarkdown());
    private readonly IReadOnlyList<SourceProject> _projects = Repository.ReadSourceProjects();

    private static string PlatformOf(string part) => $"Memoria.ToDo.{part}.Platform";
    private static string AppOf(string part) => $"Memoria.ToDo.{part}.App";

    private static bool IsApp(SourceProject p) => p.Name == AppOf(p.Part);
    private static bool IsPlatform(SourceProject p) => p.Name == PlatformOf(p.Part);

    /// <summary>What a project may reference, by the naming rules and the map. Null when the project isn't one the rules know.</summary>
    private HashSet<string>? AllowedReferences(SourceProject project)
    {
        if (IsApp(project))
        {
            return _projects.Where(p => p.Part == project.Part || p.Part == "Shared").Select(p => p.Name).ToHashSet();
        }

        if (IsPlatform(project))
        {
            return project.Part == "Shared" ? [] : [PlatformOf("Shared")];
        }

        var module = _map.Modules.FirstOrDefault(m => m.ProjectName == project.Name);
        if (module is null)
        {
            return null;
        }

        var allowed = module.MayUse.Select(use => _map.Resolve(module.Part, use).ProjectName).ToHashSet();
        allowed.Add(PlatformOf(module.Part));
        allowed.Add(PlatformOf("Shared"));
        return allowed;
    }

    [Fact]
    public void Map_lists_the_three_parts_with_modules_that_each_name_only_listed_modules()
    {
        Assert.Equal(Parts, _map.Modules.Select(m => m.Part).Distinct());
        Assert.All(_map.Modules, m => Assert.All(m.MayUse, use => _map.Resolve(m.Part, use)));
    }

    [Fact]
    public void Map_lists_the_four_shared_modules_and_four_modules_in_each_app()
    {
        Assert.Equal(["Tasks", "Sync", "Editing", "Rendering"], _map.Modules.Where(m => m.Part == "Shared").Select(m => m.Name));
        foreach (var app in new[] { "Desktop", "Android" })
        {
            Assert.Equal(["Shell", "Task editor", "Task list", "Configure"], _map.Modules.Where(m => m.Part == app).Select(m => m.Name));
        }
    }

    [Fact]
    public void Every_project_under_src_is_a_platform_a_composition_root_or_a_module_in_the_map_named_by_the_rules()
    {
        var unknown = _projects
            .Where(p => !IsApp(p) && !IsPlatform(p) && !_map.Modules.Any(m => m.ProjectName == p.Name))
            .Select(p => p.Name);

        Assert.Empty(unknown);
    }

    [Fact]
    public void Every_project_sits_in_the_folder_of_its_part_and_is_named_after_its_own_folder()
    {
        var misplaced = _projects
            .Where(p => !p.Name.StartsWith($"Memoria.ToDo.{p.Part}.", StringComparison.Ordinal)
                        || !p.Name.EndsWith("." + p.FolderName, StringComparison.Ordinal))
            .Select(p => p.Name);

        Assert.Empty(misplaced);
    }

    [Fact]
    public void Every_project_references_only_the_projects_the_map_and_the_naming_rules_allow()
    {
        var violations = new List<string>();
        foreach (var project in _projects)
        {
            var allowed = AllowedReferences(project);
            if (allowed is null)
            {
                continue; // reported by the project-naming test
            }

            violations.AddRange(project.ProjectReferences
                .Where(reference => !allowed.Contains(reference))
                .Select(reference => $"{project.Name} references {reference}"));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void A_platform_references_no_module()
    {
        var moduleNames = _map.Modules.Select(m => m.ProjectName).ToHashSet();

        var violations = _projects
            .Where(IsPlatform)
            .SelectMany(p => p.ProjectReferences.Where(moduleNames.Contains).Select(r => $"{p.Name} references {r}"));

        Assert.Empty(violations);
    }

    [Fact]
    public void No_project_references_a_project_of_the_other_app()
    {
        var violations = _projects
            .Where(p => p.Part != "Shared")
            .SelectMany(p => p.ProjectReferences
                .Select(r => (Reference: r, Target: _projects.FirstOrDefault(t => t.Name == r)))
                .Where(x => x.Target is { } target && target.Part != "Shared" && target.Part != p.Part)
                .Select(x => $"{p.Name} references {x.Reference}"));

        Assert.Empty(violations);
    }

    [Fact]
    public void No_project_reference_forms_a_cycle()
    {
        var references = _projects.ToDictionary(p => p.Name, p => p.ProjectReferences);
        var finished = new HashSet<string>();
        var path = new List<string>();

        string? FindCycle(string name)
        {
            if (path.Contains(name))
            {
                return string.Join(" -> ", path.Skip(path.IndexOf(name)).Append(name));
            }

            if (!references.TryGetValue(name, out var next) || !finished.Add(name))
            {
                return null;
            }

            path.Add(name);
            foreach (var target in next)
            {
                if (FindCycle(target) is { } cycle)
                {
                    return cycle;
                }
            }

            path.RemoveAt(path.Count - 1);
            return null;
        }

        Assert.Null(_projects.Select(p => FindCycle(p.Name)).FirstOrDefault(c => c is not null));
    }

    [Fact]
    public void Only_the_desktop_app_references_Velopack_or_H_NotifyIcon_so_Shell_does_not()
    {
        var violations = _projects
            .Where(p => p.Name != AppOf("Desktop"))
            .SelectMany(p => p.PackageReferences
                .Where(id => id.StartsWith("Velopack", StringComparison.OrdinalIgnoreCase)
                             || id.StartsWith("H.NotifyIcon", StringComparison.OrdinalIgnoreCase))
                .Select(id => $"{p.Name} references {id}"));

        Assert.Empty(violations);
    }
}
