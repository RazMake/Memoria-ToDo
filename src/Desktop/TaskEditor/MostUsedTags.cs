using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The most-used tags, which the top tags and the + menu both list, so the count has one home.</summary>
internal static class MostUsedTags
{
    public const int Count = 8;

    /// <summary>The <see cref="Count"/> most-used tags in use, most-used first, leaving out <paramref name="except"/> (ignoring case) before counting.</summary>
    public static IReadOnlyList<string> Of(ITasks tasks, IReadOnlyList<string> except) =>
    [
        .. tasks.TagsInUse()
            .Select(use => use.Tag)
            .Where(tag => !except.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Take(Count),
    ];
}
