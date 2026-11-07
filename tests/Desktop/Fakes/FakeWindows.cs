using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>The shared fake of <see cref="IAddWindow"/>: counts Show calls.</summary>
public sealed class FakeAddWindow : IAddWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <inheritdoc />
    public void Show() => ShowCount++;
}

/// <summary>The shared fake of <see cref="IViewTasksWindow"/>: counts Show calls.</summary>
public sealed class FakeViewTasksWindow : IViewTasksWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <inheritdoc />
    public void Show() => ShowCount++;
}

/// <summary>The shared fake of <see cref="IConfigureWindow"/>: counts Show calls.</summary>
public sealed class FakeConfigureWindow : IConfigureWindow
{
    /// <summary>How many times Show was called.</summary>
    public int ShowCount { get; private set; }

    /// <inheritdoc />
    public void Show() => ShowCount++;
}
