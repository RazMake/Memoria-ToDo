namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>Says whether the API still takes calls. A call that arrives once it doesn't gets <c>UNAVAILABLE</c>.</summary>
internal interface ICallGate
{
    /// <summary>False from the moment a stop begins.</summary>
    bool AcceptsCalls { get; }

    /// <summary>Cancelled from the moment a stop begins; an open stream ends with it.</summary>
    CancellationToken Stopping { get; }
}
