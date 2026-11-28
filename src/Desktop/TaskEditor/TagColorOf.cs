using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Which color a tag has, so the badges and Custom's mark agree on it.</summary>
internal static class TagColorOf
{
    /// <summary>The tag's kept color, or its default when none is kept or the kept name isn't one this app knows.</summary>
    public static TagColor Of(ITagColors tagColors, string tag) =>
        (tagColors.ColorOf(tag) is { } name ? TagColors.Find(name) : null) ?? TagColors.DefaultFor(tag);
}
