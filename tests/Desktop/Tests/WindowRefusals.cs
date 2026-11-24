namespace Memoria.ToDo.Desktop;

/// <summary>
/// The refusals of windows that loaded in a test outside the screen collection, each under the unique ID of the test that was
/// running (null when none was). Safe to use from any thread, since WPF reports a window's load from a dispatcher thread.
/// </summary>
internal sealed class WindowRefusals
{
    /// <summary>The record the guard, the hook after each test, and the assembly fixture share.</summary>
    public static WindowRefusals Shared { get; } = new();

    private readonly object gate = new();
    private readonly List<(string? TestId, string Message)> refusals = [];

    public void Record(string? testId, string message)
    {
        lock (gate)
        {
            refusals.Add((testId, message));
        }
    }

    /// <summary>Removes and returns the messages recorded under <paramref name="testId"/>, leaving other tests' alone.</summary>
    public IReadOnlyList<string> Take(string testId)
    {
        lock (gate)
        {
            var taken = refusals.Where(refusal => refusal.TestId == testId).Select(refusal => refusal.Message).ToList();
            if (taken.Count > 0)
            {
                refusals.RemoveAll(refusal => refusal.TestId == testId);
            }

            return taken;
        }
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> with every message no test took, when there is one.</summary>
    public void ThrowUnclaimed()
    {
        string[] left;
        lock (gate)
        {
            left = [.. refusals.Select(refusal => refusal.Message)];
        }

        if (left.Length > 0)
        {
            throw new InvalidOperationException(
                "A window loaded outside the screen collection with no test left to fail for it:" + Environment.NewLine
                + string.Join(Environment.NewLine, left));
        }
    }
}
