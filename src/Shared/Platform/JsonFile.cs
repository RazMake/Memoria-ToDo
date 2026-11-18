using System.Buffers;
using System.Text.Json;
using System.Text.Unicode;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>The JSON style of every store file the apps write, and the rules for reading one.</summary>
public static class JsonFile
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = UnescapedJsonEncoder.Instance,
    };

    /// <summary>
    /// The bytes of the one JSON value <paramref name="write"/> writes: two-space indents, <c>\n</c> line breaks, characters outside
    /// ASCII as they are, UTF-8 with no byte-order mark, and no line break at the end.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="write"/> writes no value, or leaves it unfinished.</exception>
    public static byte[] Write(Action<Utf8JsonWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            write(writer);
            if (writer.CurrentDepth != 0)
                throw new InvalidOperationException("The JSON value was left unfinished.");
            writer.Flush();
        }

        return buffer.WrittenCount == 0
            ? throw new InvalidOperationException("No JSON value was written.")
            : buffer.WrittenSpan.ToArray();
    }

    /// <summary>The parsed JSON value, of any kind.</summary>
    /// <exception cref="InvalidDataException">
    /// The bytes aren't valid JSON, or a string value or property name in it doesn't decode to valid text.
    /// </exception>
    public static JsonDocument Parse(byte[] bytes)
    {
        if (!Utf8.IsValid(bytes))
            throw new InvalidDataException("Not valid JSON.");
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Not valid JSON.", e);
        }

        try
        {
            RequireDecodableStrings(document.RootElement);
        }
        catch
        {
            document.Dispose();
            throw;
        }

        return document;
    }

    // The bytes are valid UTF-8, so only a lone escaped surrogate (such as \ud800) can fail to decode.
    private static void RequireDecodableStrings(JsonElement element)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    element.GetString();
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        RequireDecodableStrings(item);
                    break;
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        _ = property.Name;
                        RequireDecodableStrings(property.Value);
                    }

                    break;
            }
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidDataException("A string doesn't decode to valid text.", e);
        }
    }

    /// <summary>The object's <c>version</c>: a whole number from 1 to <paramref name="newest"/>.</summary>
    /// <exception cref="InvalidDataException">
    /// <paramref name="file"/> isn't an object, or its version is missing, not a whole number, or below 1; and when the version is above
    /// <paramref name="newest"/>, which <see cref="SkippedFile.Of"/> then reports as needing a newer app.
    /// </exception>
    public static int RequireVersion(JsonElement file, int newest)
    {
        if (file.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Not a JSON object.");
        if (!file.TryGetProperty("version", out var value) || value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException("Missing or invalid 'version'.");
        if (!value.TryGetInt32(out var version) || version < 1)
            throw new InvalidDataException("Unsupported version.");
        if (version > newest)
            throw new InvalidDataException("The file needs a newer version of the app.", new NewerVersionException());
        return version;
    }
}
