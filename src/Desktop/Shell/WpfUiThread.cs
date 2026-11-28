using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>The WPF dispatcher of the thread that created it.</summary>
internal sealed class WpfUiThread : IUiThread
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public bool IsCurrent => _dispatcher.CheckAccess();

    public void Post(Action work) => _dispatcher.BeginInvoke(work);
}
