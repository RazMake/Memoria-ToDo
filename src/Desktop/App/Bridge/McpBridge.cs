using System.IO;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The bridge an AI assistant starts with <c>--mcp</c>: it copies the assistant's standard input to the app's pipe and the
/// pipe to standard output, byte for byte. It connects at once when the pipe exists. Otherwise it starts the app, unless the
/// app already runs, and waits at most 30 seconds for the pipe. A failure goes to standard error, with exit code 1.
/// </summary>
internal sealed class McpBridge(IBridgeApp app, IBridgePipe pipe, TimeProvider time)
{
    private static readonly TimeSpan PipeWait = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(250);
    private const int BufferSize = 16 * 1024;

    /// <summary>
    /// Runs until the assistant closes <paramref name="input"/> or the app closes the pipe, and returns 0; or until a
    /// failure, which it writes to <paramref name="error"/>, and returns 1.
    /// </summary>
    public async Task<int> RunAsync(Stream input, Stream output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            await using var connection = await ConnectAsync(cancellationToken);
            await CopyBothWaysAsync(input, output, connection, cancellationToken);
            return 0;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            await error.WriteLineAsync($"Memoria ToDo's MCP bridge failed: {Describe(failure)}");
            return 1;
        }
    }

    private static string Describe(Exception failure) => failure is UnauthorizedAccessException
        ? $"{failure.Message} The pipe belongs to another user, or this program runs as administrator while the app doesn't."
        : failure.Message;

    private async Task<Stream> ConnectAsync(CancellationToken cancellationToken)
    {
        if (await pipe.TryConnectAsync(cancellationToken) is { } connected)
        {
            return connected;
        }

        var running = app.IsRunning();
        if (!running)
        {
            app.Start();
        }

        var started = time.GetTimestamp();
        while (true)
        {
            await Task.Delay(PollEvery, time, cancellationToken);
            if (await pipe.TryConnectAsync(cancellationToken) is { } stream)
            {
                return stream;
            }

            if (time.GetElapsedTime(started) >= PipeWait)
            {
                throw new TimeoutException(running
                    ? "The app is running but its pipe isn't there. Turn on AI assistant access in Memoria ToDo's Configure window, or read there why it's off."
                    : "The app was started but its pipe didn't appear within 30 seconds.");
            }
        }
    }

    private static async Task CopyBothWaysAsync(Stream input, Stream output, Stream pipe, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var toApp = CopyAsync(input, pipe, stop.Token);
        var toAssistant = CopyAsync(pipe, output, stop.Token);
        var first = await Task.WhenAny(toApp, toAssistant);
        // One side ended, so the other is stopped: a read on the real standard input can't be cancelled, and the process ends instead.
        await stop.CancelAsync();
        await first;
    }

    private static async Task CopyAsync(Stream from, Stream to, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        int count;
        while ((count = await from.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await to.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            await to.FlushAsync(cancellationToken);
        }
    }
}
