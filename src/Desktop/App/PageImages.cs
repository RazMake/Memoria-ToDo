using System.IO;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.App;

/// <summary>What the clone's address answers to one request: an HTTP status, and for an image its content type and bytes.</summary>
internal readonly record struct PageImage(int Status, string? ContentType, byte[]? Content);

/// <summary>
/// Decides what the clone's address serves: an image of the clone, else an image waiting for an open edit, read-only.
/// Anything else is "404 Not Found", and a failed read "500 Internal Server Error".
/// </summary>
internal sealed class PageImages
{
    /// <param name="files">Reads the images.</param>
    /// <param name="rules">Tasks' file rules, which say which paths are images.</param>
    /// <param name="cloneFolder">The clone, looked in first, or null when there is no clone yet.</param>
    /// <param name="editImagesFolder">The folder of the images that wait for an open edit.</param>
    /// <param name="logger">Gets a Warning for each failed read.</param>
    public PageImages(IFileSystem files, IRepositoryFileRules rules, string? cloneFolder, string editImagesFolder, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(editImagesFolder);
        ArgumentNullException.ThrowIfNull(logger);
        _files = files;
        _rules = rules;
        _logger = logger;
        _folders = cloneFolder is null ? [editImagesFolder] : [cloneFolder, editImagesFolder];
    }

    private static readonly PageImage NotFound = new(404, null, null);
    private static readonly PageImage ServerError = new(500, null, null);

    private readonly IFileSystem _files;
    private readonly IRepositoryFileRules _rules;
    private readonly ILogger _logger;
    private readonly string[] _folders;

    /// <summary>The answer to a request with this method for this path, such as <c>/images/&lt;task-id&gt;/&lt;file&gt;.png</c>, as the request's address spells it.</summary>
    public PageImage Find(string method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        if (method != "GET" || !path.StartsWith('/') || ContentTypeOfImage(path[1..]) is not { } contentType)
        {
            return NotFound;
        }

        var relative = path[1..].Replace('/', Path.DirectorySeparatorChar);
        foreach (var folder in _folders)
        {
            try
            {
                return new PageImage(200, contentType, _files.ReadFile(Path.Combine(folder, relative)));
            }
            catch (FileNotFoundException)
            {
                // Not in this folder: the next one, then not found.
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // The link is images/<task id>/<file>, as the image rules checked.
                var parts = path[1..].Split('/');
                _logger.Warning(failure, "Reading the image {File} of task {TaskId} failed", parts[^1], parts[^2]);
                return ServerError;
            }
        }

        return NotFound;
    }

    // The content type when the link is a task's image: one Tasks' rules classify as an image, whose file name holds
    // only ASCII letters, digits, and hyphens, and ends in .png or .jpg. The name rule leaves out every character that
    // could climb out of a folder, or be read as another file once the system takes the path. Null for any other link.
    private string? ContentTypeOfImage(string link)
    {
        try
        {
            if (_rules.Classify(link) != RepositoryFileKind.Image)
            {
                return null;
            }
        }
        catch (ArgumentException)
        {
            // The rules reject a rooted path and a '.' or '..' segment.
            return null;
        }

        var file = link.AsSpan(link.LastIndexOf('/') + 1);
        var dot = file.LastIndexOf('.');
        if (dot <= 0 || file[..dot].ContainsAnyExcept(NameCharacters))
        {
            return null;
        }

        return ContentTypes.GetValueOrDefault(file[dot..].ToString());
    }

    private static readonly System.Buffers.SearchValues<char> NameCharacters =
        System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-");

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
    };
}
