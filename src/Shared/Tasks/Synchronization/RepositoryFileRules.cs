namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Tasks' repository layout and merge rules, for Sync. Merging reads only the bytes it's given.</summary>
internal sealed class RepositoryFileRules : IRepositoryFileRules
{
    public static readonly RepositoryFileRules Instance = new();

    public string TasksFolderName => RepositoryLayout.TasksFolderName;

    public string ImagesFolderName => RepositoryLayout.ImagesFolderName;

    public string SettingsFileName => RepositoryLayout.SettingsFileName;

    public RepositoryFileKind Classify(string relativePath)
    {
        var parts = Split(relativePath);
        return parts switch
        {
            [RepositoryLayout.SettingsFileName] => RepositoryFileKind.Settings,
            [RepositoryLayout.TasksFolderName, var file] when TaskIdOfFile(file) is not null => RepositoryFileKind.Task,
            [RepositoryLayout.ImagesFolderName, var id, _] when TaskId.IsValid(id) => RepositoryFileKind.Image,
            _ => RepositoryFileKind.Unmanaged,
        };
    }

    public string? TaskIdInPath(string relativePath) => Split(relativePath) switch
    {
        [RepositoryLayout.TasksFolderName, var file] => TaskIdOfFile(file),
        [RepositoryLayout.ImagesFolderName, var id, _] when TaskId.IsValid(id) => id,
        _ => null,
    };

    public byte[]? MergeTask(string expectedTaskId, byte[]? basis, byte[]? local, byte[]? remote) => TaskMerge.Merge(expectedTaskId, basis, local, remote);

    public byte[]? MergeSettings(byte[]? basis, byte[]? local, byte[]? remote) => SettingsMerge.Merge(basis, local, remote);

    private static string? TaskIdOfFile(string file) =>
        file.EndsWith(".json", StringComparison.Ordinal) && TaskId.IsValid(file.AsSpan(0, file.Length - ".json".Length))
            ? file[..^".json".Length]
            : null;

    private static string[] Split(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\') || Path.IsPathRooted(relativePath) || relativePath.Length > 1 && relativePath[1] == ':')
            throw new ArgumentException("Must be relative to the repository root.", nameof(relativePath));
        var parts = relativePath.Split('/');
        if (parts.Any(part => part is "." or ".."))
            throw new ArgumentException("Must not contain '.' or '..' segments.", nameof(relativePath));
        return parts;
    }
}
