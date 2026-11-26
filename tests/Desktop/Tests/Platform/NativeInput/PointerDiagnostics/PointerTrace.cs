using System.Diagnostics;
using System.Text;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// One test's record of its real pointer moves: the newest <see cref="Capacity"/> timed entries, plus facts kept apart per move
/// (the readings at the send and at the failure, and the outcome) of the latest move only, which dropping old entries never erases.
/// </summary>
internal sealed class PointerTrace
{
    public const int Capacity = 200;

    /// <summary>Names of the facts kept per move.</summary>
    public const string AtSend = "at send";
    public const string AtFailure = "at failure";
    public const string Refused = "refused";
    public const string Misplaced = "misplaced";
    public const string TimedOut = "timed out";
    public const string QueueStatus = "queue status";

    private readonly Queue<(TimeSpan Elapsed, string Move, string What)> _entries = new(Capacity);
    private readonly List<(string Name, string What)> _facts = [];
    private readonly Stopwatch _clock = new();
    private string? _keptMove;
    private int _calls;

    /// <summary>The label that tells one move apart: the test's call (1 for the first) and the step, such as "away" or "target".</summary>
    public static string Label(int call, string step) => $"call {call} {step}";

    public TimeSpan Elapsed => _clock.Elapsed;

    public int BeginCall()
    {
        if (!_clock.IsRunning)
        {
            _clock.Start();
        }

        return ++_calls;
    }

    /// <summary>How many of the oldest entries were dropped.</summary>
    public int Dropped { get; private set; }

    /// <summary>Adds an entry, timed from the test's first move; the oldest entry is dropped past <see cref="Capacity"/>.</summary>
    public void Record(TimeSpan elapsed, string move, string what)
    {
        if (_entries.Count == Capacity)
        {
            _entries.Dequeue();
            Dropped++;
        }

        _entries.Enqueue((elapsed, move, what));
    }

    /// <summary>
    /// Keeps a fact of the move, never dropped. A fact of a move other than the one kept before replaces all the facts kept so far,
    /// so only the latest move's summary is kept; earlier moves' events live in the entries.
    /// </summary>
    public void Keep(string move, string name, string what)
    {
        if (move != _keptMove)
        {
            _facts.Clear();
            _keptMove = move;
        }

        var index = _facts.FindIndex(fact => fact.Name == name);
        if (index >= 0)
        {
            _facts[index] = (name, what);
        }
        else
        {
            _facts.Add((name, what));
        }
    }

    /// <summary>The text appended to a failure message: the dropped count, the kept facts, then the entries, oldest first.</summary>
    public string Describe()
    {
        var text = new StringBuilder();
        text.Append("Pointer trace of this test's real moves; ").Append(Dropped).Append(" oldest entries dropped.\n");
        if (_keptMove is not null)
        {
            text.Append("Latest move, kept apart from the entries:\n");
            foreach (var (name, what) in _facts)
            {
                text.Append("  ").Append(_keptMove).Append(' ').Append(name).Append(": ").Append(what).Append('\n');
            }
        }

        text.Append("Entries, oldest first:\n");
        foreach (var (elapsed, move, what) in _entries)
        {
            text.Append("  [+").Append((long)elapsed.TotalMilliseconds).Append(" ms] ").Append(move).Append(": ").Append(what).Append('\n');
        }

        text.Append("Not shown: mouse capture held by an unobserved thread, windows of other threads or processes that came and went between readings, ")
            .Append("and whether Windows dropped or delayed a move. A missing message is evidence, not proof, that Windows did not deliver it. ")
            .Append("A low-level hook installed later can run before ours and suppress an event; Windows can silently remove a slow low-level hook; ")
            .Append("input can still be lost after the hook chain; a timeout does not prove the hook was present throughout the wait; ")
            .Append("and the observer itself changes timing slightly. ")
            .Append("This trace names no cause.");
        return text.ToString();
    }
}