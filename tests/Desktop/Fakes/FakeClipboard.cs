using Memoria.ToDo.Desktop.TaskEditor;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>The shared fake of <see cref="IClipboard"/>. A test sets what the clipboard holds.</summary>
public sealed class FakeClipboard : IClipboard
{
    /// <summary>The text the clipboard holds; null when none.</summary>
    public string? Content { get; set; }

    /// <summary>The PNG the clipboard holds; null when none.</summary>
    public byte[]? Image { get; set; }

    /// <summary>What <see cref="ImagePng"/> throws, as a busy clipboard does; null to answer normally.</summary>
    public Exception? ImageFailure { get; set; }

    /// <inheritdoc />
    public string? Text() => Content;

    /// <inheritdoc />
    public byte[]? ImagePng() => ImageFailure is null ? Image : throw ImageFailure;
}
