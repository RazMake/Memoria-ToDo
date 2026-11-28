using Xunit;

namespace Memoria.ToDo.Architecture.Desktop;

public sealed class The_desktop_module_map_and_the_projects : ModuleMapChecks
{
    private protected override string Platform => "desktop";

    [Fact]
    public void The_desktop_app_references_every_package_that_writes_and_reads_the_log_files()
    {
        var app = _projects.Single(p => p.Name == AppOf("Desktop"));
        Assert.All(LogFilePackages, package => Assert.Contains(package, app.PackageReferences));
    }

    [Fact]
    public void Map_lists_Logs_as_a_desktop_module_that_uses_no_module_and_Shell_may_use_it()
    {
        var logs = Assert.Single(_map.Modules, m => m.Part == "Desktop" && m.Name == "Logs");
        var shell = Assert.Single(_map.Modules, m => m.Part == "Desktop" && m.Name == "Shell");
        Assert.Empty(logs.MayUse);
        Assert.Contains("Logs", shell.MayUse);
    }

    [Fact]
    public void Only_Shell_and_the_MCP_server_may_use_Logs()
    {
        var users = _map.Modules.Where(m => m.MayUse.Contains("Logs")).Select(m => $"{m.Part} {m.Name}");
        Assert.Equal(["Desktop MCP server", "Desktop Shell"], users.Order());
    }

    [Fact]
    public void Only_Shell_and_Configure_may_use_the_MCP_server()
    {
        var users = _map.Modules.Where(m => m.MayUse.Contains("MCP server")).Select(m => $"{m.Part} {m.Name}");
        Assert.Equal(["Desktop Configure", "Desktop Shell"], users.Order());
    }

    [Fact]
    public void The_MCP_server_may_use_no_window_module_so_no_loop_forms()
    {
        var server = Assert.Single(_map.Modules, m => m.Part == "Desktop" && m.Name == "MCP server");
        Assert.Equal(["Contacts", "Logs", "Sync", "Tasks", "Updates"], server.MayUse.Order());
    }

    [Fact]
    public void The_MCP_server_project_sits_in_its_own_folder_of_the_desktop_part()
    {
        var server = Assert.Single(_projects, p => p.Name == "Memoria.ToDo.Desktop.MCPServer");
        Assert.Equal("Desktop", server.Part);
        Assert.Equal("MCPServer", server.FolderName);
    }

    [Fact]
    public void Only_the_MCP_server_project_references_the_ModelContextProtocol_Core_package()
    {
        var users = _projects.Where(p => p.PackageReferences.Contains("ModelContextProtocol.Core", StringComparer.OrdinalIgnoreCase)).Select(p => p.Name);
        Assert.Equal(["Memoria.ToDo.Desktop.MCPServer"], users);
    }

    [Fact]
    public void The_Logs_project_sits_in_its_own_folder_and_references_only_the_two_platforms_and_Serilog()
    {
        var logs = Assert.Single(_projects, p => p.Name == "Memoria.ToDo.Desktop.Logs");
        Assert.Equal("Desktop", logs.Part);
        Assert.Equal("Logs", logs.FolderName);
        Assert.All(logs.ProjectReferences, reference => Assert.Contains(reference, new[] { PlatformOf("Desktop"), PlatformOf("Shared") }));
        Assert.Contains(PlatformOf("Desktop"), logs.ProjectReferences);
        Assert.Equal(["Serilog"], logs.PackageReferences);
    }
}
