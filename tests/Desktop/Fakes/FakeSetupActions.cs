using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.Fakes;

public sealed class FakeSetupActions : ISetupActions
{
    public string? CopiedPublicKey { get; private set; }
    public Uri? OpenedKeyPage { get; private set; }

    public void CopyPublicKey(string publicKey) => CopiedPublicKey = publicKey;
    public void OpenKeyPage(Uri address) => OpenedKeyPage = address;
}
