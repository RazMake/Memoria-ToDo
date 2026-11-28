using System.Text.Encodings.Web;

namespace Memoria.ToDo.Shared.Platform;

/// <summary>
/// Escapes only what JSON requires, as <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> does, but keeps characters outside the
/// Basic Multilingual Plane, such as emoji, as they are.
/// </summary>
internal sealed class UnescapedJsonEncoder : JavaScriptEncoder
{
    private static readonly JavaScriptEncoder Relaxed = UnsafeRelaxedJsonEscaping;

    public static readonly UnescapedJsonEncoder Instance = new();

    public override int MaxOutputCharactersPerInputCharacter => Relaxed.MaxOutputCharactersPerInputCharacter;

    public override bool WillEncode(int unicodeScalar) => unicodeScalar <= char.MaxValue && Relaxed.WillEncode(unicodeScalar);

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        for (var i = 0; i < textLength; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < textLength && char.IsLowSurrogate(text[i + 1]))
                i++;
            else if (Relaxed.WillEncode(text[i]))
                return i;
        }

        return -1;
    }

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten) =>
        Relaxed.TryEncodeUnicodeScalar(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);
}
