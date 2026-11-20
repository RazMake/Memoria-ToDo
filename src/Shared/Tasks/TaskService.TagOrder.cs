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
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                RequireTag(tag, nameof(tags));
                if (!seen.Add(tag))
                    throw new ArgumentException($"The tag '{tag}' is there twice.", nameof(tags));
            }

            return owner.repository.Locked(_ => owner.tagColors.Value.Sort(tags));
        }

        public event EventHandler? Changed
        {
            add => owner.tagOrderChanged += value;
            remove => owner.tagOrderChanged -= value;
        }

        public void Move(string tag, string? previousTag, string? nextTag)
        {
            RequireTag(tag, nameof(tag));
            if (previousTag is not null)
                RequireNeighbor(previousTag, tag, nameof(previousTag));
            if (nextTag is not null)
                RequireNeighbor(nextTag, tag, nameof(nextTag));
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
                owner.Announce(Notice.TagOrder | Notice.LocalSaved);
            }
        }

        private static void RequireTag(string tag, string paramName)
        {
            ArgumentNullException.ThrowIfNull(tag, paramName);
            if (!TaskText.IsStoredTag(tag))
                throw new ArgumentException("Must be one non-empty line with no spaces around it.", paramName);
        }

        private static void RequireNeighbor(string neighbor, string tag, string paramName)
        {
            RequireTag(neighbor, paramName);
            if (string.Equals(neighbor, tag, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A tag can't be its own neighbor.", paramName);
        }
    }
}
