using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

internal sealed partial class TaskService
{
    public ITagOrder TagOrder => new TagOrderView(this);

    /// <summary>The tag places, kept in <c>tags.json</c> and changed under the same lock as the tasks.</summary>
    private sealed class TagOrderView(TaskService owner) : ITagOrder
    {
        public IReadOnlyList<string> Sort(IReadOnlyCollection<string> tags)
        {
            ArgumentNullException.ThrowIfNull(tags);
            var normalized = TagList.Normalized(tags, nameof(tags));
            return owner.repository.Locked(_ => owner.tagColors.Value.Sort(normalized));
        }

        public event EventHandler? Changed
        {
            add => owner.tagOrderChanged += value;
            remove => owner.tagOrderChanged -= value;
        }

        public void Move(string tag, string? previousTag, string? nextTag)
        {
            tag = RequireTag(tag, nameof(tag));
            if (previousTag is not null)
                previousTag = RequireNeighbor(previousTag, tag, nameof(previousTag));
            if (nextTag is not null)
                nextTag = RequireNeighbor(nextTag, tag, nameof(nextTag));
            var written = false;
            owner.repository.LockedChange(clone =>
            {
                var root = clone ?? throw new NoRepositoryFolderException();
                var tags = owner.tagColors.Require();
                var changed = TagMove.Apply(tags, owner.set.Tags.InUse.Select(use => use.Tag), tag, previousTag, nextTag, owner.Now());
                if (changed is null)
                    return;
                owner.tagColorsFolder.Write(root, changed);
                owner.tagColors = new FileLoad<TagColorSet>(changed, true, null);
                written = true;
            });

            if (written)
            {
                owner.logger.Information("Moved tag {Tag}", tag);
                owner.AnnounceSaved(Notice.TagOrder | Notice.LocalSaved, null);
            }
        }

        /// <summary>The normalized tag. Throws <see cref="ArgumentException"/> when it's empty once normalized.</summary>
        private static string RequireTag(string tag, string paramName) =>
            TagList.Normalized([tag], paramName)[0];

        private static string RequireNeighbor(string neighbor, string tag, string paramName)
        {
            var normalized = RequireTag(neighbor, paramName);
            return normalized == tag ? throw new ArgumentException("A tag can't be its own neighbor.", paramName) : normalized;
        }
    }
}
