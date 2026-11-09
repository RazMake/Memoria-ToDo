using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Wraps Configure's repository folder so Tasks reads the folder Configure saved: at once, and again after each
/// successful clone or use of an existing folder. Await it on the UI thread, as Configure does, so Tasks
/// is told on the UI thread too.
/// </summary>
internal sealed class TasksFollowRepositoryFolder : IRepositoryFolder
{
    private readonly IRepositoryFolder _inner;
    private readonly ITasks _tasks;

    public TasksFollowRepositoryFolder(IRepositoryFolder inner, ITasks tasks)
    {
        _inner = inner;
        _tasks = tasks;
        _tasks.UseRepositoryFolder(_inner.Current());
    }

    public string? Current() => _inner.Current();

    public Task<string?> RemoteAsync(CancellationToken cancellationToken) => _inner.RemoteAsync(cancellationToken);

    public Task<string?> IdentityAsync(CancellationToken cancellationToken) => _inner.IdentityAsync(cancellationToken);

    public async Task CloneAsync(string url, string folder, CancellationToken cancellationToken)
    {
        await _inner.CloneAsync(url, folder, cancellationToken);
        _tasks.UseRepositoryFolder(_inner.Current());
    }

    public void UseExisting(string folder)
    {
        _inner.UseExisting(folder);
        _tasks.UseRepositoryFolder(_inner.Current());
    }
}
