using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Fakes;

public sealed class FakeSetupActions : ISetupActions
{
    public string? CopiedPublicKey { get; private set; }
    public Uri? OpenedKeyPage { get; private set; }

    /// <summary>When set, both actions throw it and record nothing.</summary>
    public Exception? Failure { get; set; }

    public void CopyPublicKey(string publicKey)
    {
        ThrowIfFailing();
        CopiedPublicKey = publicKey;
    }

    public void OpenKeyPage(Uri address)
    {
        ThrowIfFailing();
        OpenedKeyPage = address;
    }

    private void ThrowIfFailing()
    {
        if (Failure is not null)
        {
            throw Failure;
        }
    }
}
