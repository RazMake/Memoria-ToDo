using Memoria.ToDo.Android.TaskList;

namespace Memoria.ToDo.Android.Shell;

/// <summary>What opening the app does. The entry activity calls it, then closes.</summary>
internal sealed class AppOpening(ITaskViewScreen taskView, Serilog.ILogger logger)
{
    private readonly ITaskViewScreen _taskView = taskView;
    private readonly Serilog.ILogger _logger = logger;

    /// <summary>Opens Task View.</summary>
    public void Open()
    {
        _logger.Information("Opening the app");
        _logger.Debug("Opening the app leads to Task View");
        _taskView.Show();
    }
}
