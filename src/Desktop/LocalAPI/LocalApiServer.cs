using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>Builds the Local API's lifetime over its real web server.</summary>
public static class LocalApiServer
{
    /// <summary>Throws ArgumentNullException for a null argument, and ArgumentException for an empty pipe name.</summary>
    public static ILocalApiLifetime Create(
        ITaskStore tasks, IContacts contacts, ISyncService sync, string pipeName, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(logger);

        // The host needs the lifetime as its call gate, and the lifetime needs the host, so the host asks for the gate when it starts.
        LocalApiLifetime? lifetime = null;
        var host = new KestrelApiHost(pipeName, tasks, contacts, sync, () => lifetime!, logger);
        lifetime = new LocalApiLifetime(host, pipeName, logger, LocalApiLifetime.DefaultStopWait);
        return lifetime;
    }
}
