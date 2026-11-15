using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Tasks;

/// <summary>What reading one shared file in a clone found. <see cref="Failure"/> is set when the file is there but can't be read.</summary>
internal sealed record FileLoad<T>(T Value, bool Present, InvalidDataException? Failure)
{
    /// <summary>The value, or <see cref="InvalidDataException"/> when the file can't be read, so nothing runs on guessed values.</summary>
    public T Require() =>
        Failure is null ? Value : throw new InvalidDataException(Failure.Message, Failure);

    /// <summary>Never throws for a bad file: a missing file gives <paramref name="absent"/>, an unreadable one a <see cref="Failure"/>.</summary>
    public static FileLoad<T> Read(IFileSystem files, string path, Func<byte[], T> parse, T absent)
    {
        try
        {
            return new FileLoad<T>(parse(files.ReadFile(path)), true, null);
        }
        catch (FileNotFoundException)
        {
            return new FileLoad<T>(absent, false, null);
        }
        catch (InvalidDataException e)
        {
            return new FileLoad<T>(absent, true, e);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new FileLoad<T>(absent, true, new InvalidDataException(e.Message, e));
        }
    }
}
