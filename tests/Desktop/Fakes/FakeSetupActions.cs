using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Fakes;

public sealed class FakeSetupActions : ISetupActions
{
    private readonly List<string> calls = [];
    private readonly List<string> copiedTexts = [];

    public string? CopiedPublicKey { get; private set; }
    public Uri? OpenedKeyPage { get; private set; }

    /// <summary>The texts <see cref="CopyText"/> copied, in order; a failed copy adds none.</summary>
    public IReadOnlyList<string> CopiedTexts => copiedTexts;

    /// <summary>The actions that succeeded, in order: "CopyPublicKey", "CopyText", and "OpenKeyPage".</summary>
    public IReadOnlyList<string> Calls => calls;

    /// <summary>When set, every action throws it and record nothing.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, copying throws it and records nothing.</summary>
    public Exception? CopyFailure { get; set; }

    /// <summary>When set, opening the key page throws it and records nothing.</summary>
    public Exception? OpenFailure { get; set; }

    public void CopyPublicKey(string publicKey)
    {
        ThrowIfFailing(CopyFailure);
        CopiedPublicKey = publicKey;
        calls.Add(nameof(CopyPublicKey));
    }

    public void CopyText(string text)
    {
        ThrowIfFailing(CopyFailure);
        copiedTexts.Add(text);
        calls.Add(nameof(CopyText));
    }

    public void OpenKeyPage(Uri address)
    {
        ThrowIfFailing(OpenFailure);
        OpenedKeyPage = address;
        calls.Add(nameof(OpenKeyPage));
    }

    private void ThrowIfFailing(Exception? own)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        if (own is not null)
        {
            throw own;
        }
    }
}
