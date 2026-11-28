using Memoria.ToDo.Desktop.KeepAwake;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IDisplayRequest"/>: it remembers whether the request is held, remembers every
/// <see cref="Set"/>, and can be told to refuse, as Windows does. A refused call leaves the request as it was.
/// </summary>
public sealed class FakeDisplayRequest : IDisplayRequest
{
    private readonly List<bool> setCalls = [];

    /// <summary>Whether the request is held now. Starts false.</summary>
    public bool IsSet { get; private set; }

    /// <summary>The values <see cref="Set"/> was called with, in order, including those Windows refused.</summary>
    public IReadOnlyList<bool> SetCalls => setCalls;

    /// <summary>When set, <see cref="Set"/> throws it and the request stays as it was. Null ends the refusal.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc />
    public void Set(bool on)
    {
        setCalls.Add(on);
        if (Failure is not null)
        {
            throw Failure;
        }

        IsSet = on;
    }
}
