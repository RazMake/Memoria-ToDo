namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>The message sizes the contract promises (docs/architecture/local-api-contract.md): one home for the server and the Tasks service.</summary>
internal static class ApiLimits
{
    private const int Mebibyte = 1024 * 1024;

    /// <summary>The largest image <c>AddImage</c> takes, before shrinking.</summary>
    public const int MaxImageBytes = 32 * Mebibyte;

    /// <summary>The largest request the server reads: an image of <see cref="MaxImageBytes"/> and the few bytes of the fields around it.</summary>
    public const int MaxReceiveMessageBytes = MaxImageBytes + Mebibyte;

    /// <summary>The largest reply the server sends.</summary>
    public const int MaxSendMessageBytes = 64 * Mebibyte;
}
