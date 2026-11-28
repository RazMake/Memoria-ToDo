using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>The shared fake of <see cref="IDefaultBrowser"/>. It records the addresses it was asked to open and opens nothing.</summary>
public sealed class FakeDefaultBrowser : IDefaultBrowser
{
    private readonly CountSignal opens = new();
    private readonly List<string> opened = [];

    /// <summary>The addresses opened, in order. Read it on the UI thread, where the browser is used.</summary>
    public IReadOnlyList<string> Opened => opened;

    /// <summary>What <see cref="Open"/> throws, as a browser that can't open the address does; null to open normally.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc />
    public void Open(string address)
    {
        opened.Add(address);
        opens.Increment();
        if (Failure is not null)
        {
            throw Failure;
        }
    }

    /// <summary>Completes once the browser has been asked to open at least <paramref name="count"/> addresses.</summary>
    public Task WaitForOpensAsync(int count) => opens.WaitForAsync(count);
}
