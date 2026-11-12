using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Shared.Fakes;

public sealed record FakeSshHostKey(
    string Host, int Port, string Algorithm, string Fingerprint, bool CanAccept) : ISshHostKey;
