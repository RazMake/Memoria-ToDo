using System.Diagnostics;
using System.IO;
using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The repository folder as a clone made by the installed git, with the user's own git credentials. The folder's
/// path is kept in one text file on this device.
/// </summary>
internal sealed class GitRepositoryFolder : IRepositoryFolder
{
    private readonly string _settingFile;

    /// <param name="settingFile">The file that keeps the folder's path; made when first needed.</param>
    public GitRepositoryFolder(string settingFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingFile);
        _settingFile = settingFile;
    }

    public string? Current()
    {
        if (!File.Exists(_settingFile))
        {
            return null;
        }

        var folder = File.ReadAllText(_settingFile).Trim();
        return folder.Length == 0 ? null : folder;
    }

    public async Task CloneAsync(string url, string folder, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var fullFolder = Path.GetFullPath(folder);
        if (Directory.Exists(fullFolder) && Directory.EnumerateFileSystemEntries(fullFolder).Any())
        {
            throw new InvalidOperationException($"{fullFolder} isn't empty. Choose an empty or new folder.");
        }

        await RunGitAsync(["clone", "--", url, fullFolder], "couldn't clone the repository", cancellationToken);
        Save(fullFolder);
    }

    public Task<string?> RemoteAsync(CancellationToken cancellationToken) =>
        ConfigValueAsync("remote.origin.url", cancellationToken);

    public async Task<string?> IdentityAsync(CancellationToken cancellationToken)
    {
        var name = await ConfigValueAsync("user.name", cancellationToken);
        var email = await ConfigValueAsync("user.email", cancellationToken);
        return name is null || email is null ? null : $"{name} <{email}>";
    }

    // The value git uses for the key in the current clone, its own settings first, then the user's; null when unset.
    private async Task<string?> ConfigValueAsync(string key, CancellationToken cancellationToken)
    {
        var folder = Current();
        if (folder is null)
        {
            return null;
        }

        // git config exits with 1 when the key isn't set.
        var (exitCode, output) = await RunGitAsync(
            ["-C", folder, "config", "--get", key], $"couldn't read {key}", cancellationToken, allowedExitCode: 1);
        return exitCode == 0 ? output.Trim() : null;
    }

    private static async Task<(int ExitCode, string Output)> RunGitAsync(
        string[] arguments, string failure, CancellationToken cancellationToken, int? allowedExitCode = null)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Process git;
        try
        {
            git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start.");
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new InvalidOperationException("git isn't installed, or isn't on the PATH.", error);
        }

        using (git)
        {
            var errors = git.StandardError.ReadToEndAsync(cancellationToken);
            var output = await git.StandardOutput.ReadToEndAsync(cancellationToken);
            await git.WaitForExitAsync(cancellationToken);
            if (git.ExitCode != 0 && git.ExitCode != allowedExitCode)
            {
                throw new InvalidOperationException($"git {failure}: {(await errors).Trim()}");
            }

            return (git.ExitCode, output);
        }
    }

    public void UseExisting(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var fullFolder = Path.GetFullPath(folder);
        var gitEntry = Path.Combine(fullFolder, ".git");
        if (!Directory.Exists(gitEntry) && !File.Exists(gitEntry))
        {
            throw new InvalidOperationException($"{fullFolder} isn't a git clone: it has no .git folder.");
        }

        Save(fullFolder);
    }

    private void Save(string folder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingFile)!);
        File.WriteAllText(_settingFile, folder);
    }
}
