namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Holds the trace of the one test running now, so a test never sees another test's moves and nothing accumulates across tests.</summary>
internal sealed class PointerTraceHolder
{
    private string? _testId;
    private PointerTrace? _trace;

    /// <summary>
    /// The trace of the test with this ID: the same one while the same ID asks again, and a new empty one when another ID asks.
    /// Only the latest ID is remembered, so asking again with an earlier ID starts empty too.
    /// </summary>
    public PointerTrace For(string testId)
    {
        ArgumentNullException.ThrowIfNull(testId);
        if (_trace is null || testId != _testId)
        {
            _testId = testId;
            _trace = new PointerTrace();
        }

        return _trace;
    }
}