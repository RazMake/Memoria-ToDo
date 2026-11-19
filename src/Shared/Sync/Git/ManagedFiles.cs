using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Which repository files Sync manages: those Tasks' rules classify, and those Contacts' rules hold.</summary>
internal sealed class ManagedFiles(IRepositoryFileRules rules, IContactFileRules contacts)
{
    public bool IsManaged(string path) => rules.Classify(path) != RepositoryFileKind.Unmanaged || contacts.Holds(path);
}
