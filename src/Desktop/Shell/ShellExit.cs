using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Desktop.TaskList;
using Memoria.ToDo.Desktop.Updates;

namespace Memoria.ToDo.Desktop.Shell;

/// <summary>
/// The exit Shell gives Updates so an update can end the app. Task View closes by its own rules, which may ask about a
/// changed edit; Add and Configure close with the app, and Add keeps its draft then, as it does on Exit.
/// </summary>
internal sealed class ShellExit(IAddWindow add, IViewTasksWindow viewTasks, IConfigureWindow configure, Action endApp) : IAppExit
{
    /// <inheritdoc />
    public bool AnyWindowOpen => add.IsOpen || viewTasks.IsOpen || configure.IsOpen;

    /// <inheritdoc />
    public bool CloseWindows() => viewTasks.Close();

    /// <inheritdoc />
    public void End() => endApp();
}
