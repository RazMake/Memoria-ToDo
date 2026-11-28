using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="IOneInstance"/>. It is the first instance unless a test says another one
/// runs, and a test can start a later instance, which runs the action the claim was given.
/// </summary>
public sealed class FakeOneInstance : IOneInstance
{
    private Action? laterStart;

    /// <summary>When true, Claim answers false, as when another instance runs.</summary>
    public bool AnotherInstanceRuns { get; set; }

    /// <summary>Whether this instance holds the claim now.</summary>
    public bool IsClaimed { get; private set; }

    /// <summary>How many times Claim was called.</summary>
    public int ClaimCount { get; private set; }

    /// <summary>Starts a later instance, which runs the action of the claim.</summary>
    /// <exception cref="InvalidOperationException">This instance holds no claim.</exception>
    public void StartLaterInstance() =>
        (laterStart ?? throw new InvalidOperationException("No claim is held, so a later start isn't handed over."))();

    /// <inheritdoc />
    public bool Claim(Action laterStart)
    {
        ClaimCount++;
        if (AnotherInstanceRuns)
        {
            return false;
        }

        this.laterStart = laterStart;
        IsClaimed = true;
        return true;
    }

    /// <inheritdoc />
    public void Release()
    {
        laterStart = null;
        IsClaimed = false;
    }
}
