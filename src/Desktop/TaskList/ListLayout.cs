namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>Where a shown row sits in the list, from its top to its bottom, in the list's own coordinates.</summary>
internal sealed record RowSpan(string TaskId, double Top, double Bottom);

/// <summary>
/// Where a tag group sits: its heading and its shown rows as one item. <see cref="Tag"/> is null for the "No tag" group, and
/// <see cref="Rows"/> is empty while the group is collapsed.
/// </summary>
internal sealed record GroupSpan(string? Tag, double Top, double Bottom, IReadOnlyList<RowSpan> Rows);

/// <summary>
/// Where the list's items sit, top to bottom: the tag groups in the order shown with "No tag" last, then the resolved tasks.
/// <see cref="ResolvedTop"/> is where the "N resolved" button starts, or null when there are no resolved tasks; the list
/// <see cref="ResolvedRows"/> is empty while they are hidden.
/// </summary>
internal sealed record ListLayout(IReadOnlyList<GroupSpan> Groups, double? ResolvedTop, IReadOnlyList<RowSpan> ResolvedRows);

/// <summary>
/// The place a dragged task lands: before the row at <see cref="Index"/> among the rows of the group named by
/// <see cref="Tag"/> (null for "No tag"), or among the resolved rows when <see cref="Resolved"/>. An index equal to the row count means the end.
/// </summary>
internal sealed record TaskGap(string? Tag, bool Resolved, int Index);
