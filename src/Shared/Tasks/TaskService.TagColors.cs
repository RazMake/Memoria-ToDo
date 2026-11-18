using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

internal sealed partial class TaskService
{
    public ITagColors TagColors => new TagColorsView(this);

    /// <summary>The tag colors, kept in <c>tags.json</c> and changed under the same lock as the tasks.</summary>
    private sealed class TagColorsView(TaskService owner) : ITagColors
    {
        public string? ColorOf(string tag)
        {
            ArgumentNullException.ThrowIfNull(tag);
            return owner.repository.Locked(_ => owner.tagColors.Failure is null ? owner.tagColors.Value.Find(tag)?.Color?.Value : null);
        }

        public event EventHandler? Changed
        {
            add => owner.tagColorsChanged += value;
            remove => owner.tagColorsChanged -= value;
        }

        public void SetColor(string tag, string colorName)
        {
            ArgumentNullException.ThrowIfNull(tag);
            ArgumentNullException.ThrowIfNull(colorName);
            if (!TaskText.IsStoredTag(tag))
                throw new ArgumentException("Must be one non-empty line with no spaces around it.", nameof(tag));
            if (!TaskText.IsSingleLine(colorName))
                throw new ArgumentException("Must be one non-empty line.", nameof(colorName));
            var written = false;
            owner.repository.LockedChange(clone =>
            {
                var root = clone ?? throw new NoRepositoryFolderException();
                var colors = owner.tagColors.Require();
                var existing = colors.Find(tag);
                if (existing?.Color?.Value == colorName)
                    return;
                var entry = (existing ?? new TagColorEntry(tag, null, null)) with { Tag = tag, Color = new TagStamp(colorName, owner.Now()) };
                var changed = colors.With([entry]);
                owner.tagColorsFolder.Write(root, changed);
                owner.tagColors = new FileLoad<TagColorSet>(changed, true, null);
                written = true;
            });

            if (written)
                owner.Announce(Notice.TagColors | Notice.LocalSaved);
        }
    }
}
