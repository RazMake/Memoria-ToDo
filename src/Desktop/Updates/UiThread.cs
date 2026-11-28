using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>Runs work on the UI thread that created it: at once when already there, otherwise waiting for the thread to run it.</summary>
internal sealed class UiThread
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public void Run(Action work) => Run(() =>
    {
        work();
        return true;
    });

    public T Run<T>(Func<T> work) => _dispatcher.CheckAccess() ? work() : _dispatcher.Invoke(work);
}
