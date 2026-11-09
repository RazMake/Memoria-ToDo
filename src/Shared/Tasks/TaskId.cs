using System.Text.RegularExpressions;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Task and image IDs: lowercase UUIDs with hyphens, made as version 7.</summary>
internal static partial class TaskId
{
    public const string LinkPrefix = "task:";

    public static string New(TimeProvider time) => Guid.CreateVersion7(time.GetUtcNow()).ToString();

    public static bool IsValid(ReadOnlySpan<char> id) => Valid().IsMatch(id);

    /// <summary>Refuses an ID that isn't valid, so it can never be a path that leaves its folder.</summary>
    public static void ThrowIfInvalid(string id, string paramName)
    {
        ArgumentNullException.ThrowIfNull(id, paramName);
        if (!IsValid(id))
            throw new ArgumentException("Not a task ID.", paramName);
    }

    /// <summary>For an ID read from a file: refuses one that isn't valid.</summary>
    public static void ThrowIfInvalidData(string id)
    {
        if (!IsValid(id))
            throw new InvalidDataException("Not a task ID.");
    }

    [GeneratedRegex(@"\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z")]
    private static partial Regex Valid();
}
