namespace Memoria.ToDo.Shared.Sync.Tests;

/// <summary>
/// A remote command a test drives by hand: its execution task ends when the test calls <see cref="Finish"/>, or faults
/// with an <see cref="ObjectDisposedException"/> once the command is disposed first, as SSH.NET's does. Once the
/// command has ended, its input can no longer be opened: <see cref="CreateInputStream"/> throws, as SSH.NET's does.
/// Sending EOF (disposing the input stream) throws <see cref="EofFailure"/> when it is set.
/// </summary>
internal sealed class FakeRemoteCommand : IRemoteCommand
{
    private readonly TaskCompletionSource execution = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private (int? Status, string? Signal, string Error)? endOnEof;
    private bool ended;

    public string CommandText => "git-receive-pack 'me/tasks.git'";

    /// <summary>When set, sending EOF fails with it, after any <see cref="FinishOnEof"/> report.</summary>
    public Exception? EofFailure { get; set; }

    /// <summary>When set, reading the output fails with it.</summary>
    public Exception? ReadFailure { get; set; }

    public int? ExitStatus { get; private set; }

    public string? ExitSignal { get; private set; }

    public string Error { get; private set; } = "";

    public Stream OutputStream => output;

    private readonly Output output;

    public FakeRemoteCommand() => output = new Output(this);

    /// <summary>Makes the host's output readable: what a command prints before it ends.</summary>
    public void Print(byte[] bytes)
    {
        output.Write(bytes);
        output.Position = 0;
    }

    public Task ExecuteAsync() => execution.Task;

    /// <summary>Throws <see cref="InvalidOperationException"/> once the command has ended, as SSH.NET's does.</summary>
    public Stream CreateInputStream() =>
        ended ? throw new InvalidOperationException("The input stream can be used only during execution.") : new Input(this);

    /// <summary>The host reports how the command ended, and the execution task completes.</summary>
    public void Finish(int? status, string? signal = null, string error = "")
    {
        ended = true;
        ExitStatus = status;
        ExitSignal = signal;
        Error = error;
        execution.TrySetResult();
    }

    /// <summary>
    /// A host that ends the command once its input ends, as receive-pack does: the report is given when EOF is sent,
    /// so the input opened while the command was still running.
    /// </summary>
    public void FinishOnEof(int? status, string? signal = null, string error = "") => endOnEof = (status, signal, error);

    /// <summary>The execution task faults, as SSH.NET's does when the session drops. The input can still be opened.</summary>
    public void Fault(Exception failure) => execution.TrySetException(failure);

    public void Dispose() => execution.TrySetException(new ObjectDisposedException(nameof(FakeRemoteCommand)));

    private sealed class Input(FakeRemoteCommand owner) : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && owner.endOnEof is { } report)
            {
                owner.Finish(report.Status, report.Signal, report.Error);
            }

            if (disposing && owner.EofFailure is { } failure)
            {
                throw failure;
            }
        }
    }

    private sealed class Output(FakeRemoteCommand owner) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            owner.ReadFailure is { } failure ? throw failure : base.Read(buffer, offset, count);
    }
}
