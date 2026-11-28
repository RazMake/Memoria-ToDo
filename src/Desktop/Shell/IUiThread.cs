namespace Memoria.ToDo.Desktop.Shell;

/// <summary>The UI thread, as the Shell's startup navigation needs it.</summary>
internal interface IUiThread
{
    /// <summary>True when the caller already runs on the UI thread.</summary>
    bool IsCurrent { get; }

    /// <summary>Queues <paramref name="work"/> to run on the UI thread.</summary>
    void Post(Action work);
}
