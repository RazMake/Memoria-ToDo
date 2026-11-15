using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The <c>tags.json</c> file in a clone.</summary>
internal sealed class TagColorsFolder(IFileSystem files)
{
    /// <summary>Never throws for a bad file: a missing file gives no entries, an unreadable one a <see cref="FileLoad{T}.Failure"/>.</summary>
    public FileLoad<TagColorSet> Read(string clone) =>
        FileLoad<TagColorSet>.Read(files, RepositoryLayout.TagColorsFile(clone), TagColorsFileFormat.Read, TagColorSet.Empty);

    public void Write(string clone, TagColorSet colors) =>
        files.WriteFile(RepositoryLayout.TagColorsFile(clone), TagColorsFileFormat.Write(colors));
}
