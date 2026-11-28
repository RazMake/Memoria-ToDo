using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;

namespace Memoria.ToDo.Desktop.MCPServer;

/// <summary>Tells a tool, before it touches a store, that no clone is selected, so the assistant is told what to do about it.</summary>
internal sealed class CloneCheck(ISyncService sync)
{
    /// <exception cref="NoRepositoryFolderException">No repository folder is selected.</exception>
    public void Require()
    {
        if (sync.Setup.State.RepositoryFolder is null)
        {
            throw new NoRepositoryFolderException();
        }
    }
}
