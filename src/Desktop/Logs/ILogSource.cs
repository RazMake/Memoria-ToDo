namespace Memoria.ToDo.Desktop.Logs;

/// <summary>Where the window reads the log. The composition root implements it beside its log writer.</summary>
public interface ILogSource
{
    /// <summary>
    /// Reads every kept entry, in no set order, without blocking the caller's thread. A file deleted while it's read is left out.
    /// Throws only when the log folder can't be listed.
    /// </summary>
    Task<LogRead> ReadAsync(CancellationToken cancellationToken);
}
