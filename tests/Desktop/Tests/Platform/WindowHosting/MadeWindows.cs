using System.Windows;

namespace Memoria.ToDo.Desktop.Platform.Tests.WindowHosting;

/// <summary>
/// The windows a test's function made for Single window, in order, closed when the test ends. Shared test setup,
/// not a fake of a port. <paramref name="create"/> makes each window; it makes a plain 300x200 window by default.
/// </summary>
internal sealed class MadeWindows(Func<Window>? create = null) : IDisposable
{
    public List<Window> All { get; } = [];

    public Window Make()
    {
        var window = create?.Invoke() ?? new Window { Width = 300, Height = 200 };
        All.Add(window);
        return window;
    }

    public void Dispose()
    {
        foreach (var window in All)
        {
            window.Close();
        }
    }
}
