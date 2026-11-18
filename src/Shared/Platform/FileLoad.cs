namespace Memoria.ToDo.Shared.Platform;

/// <summary>What reading one file found. <see cref="Failure"/> is set when the file is there but can't be read.</summary>
public sealed record FileLoad<T>(T Value, bool Present, InvalidDataException? Failure)
{
    /// <summary>The value, or <see cref="InvalidDataException"/> around <see cref="Failure"/> when the file can't be read, so nothing runs on guessed values.</summary>
    public T Require() =>
        Failure is null ? Value : throw new InvalidDataException(Failure.Message, Failure);

    /// <summary>
    /// Reads and parses the file, and never throws for a bad file: a missing file gives <paramref name="absent"/>, not <see cref="Present"/>.
    /// A failure <see cref="SkippedFile.Skips"/> gives <paramref name="absent"/>, <see cref="Present"/>, and the <see cref="Failure"/>:
    /// <paramref name="parse"/>'s <see cref="InvalidDataException"/> as it is, or one with the same message around any other.
    /// Other exceptions pass through.
    /// </summary>
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
        catch (Exception e) when (SkippedFile.Skips(e))
        {
            return new FileLoad<T>(absent, true, new InvalidDataException(e.Message, e));
        }
    }
}
