using System.IO.Pipes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The bridge's real pipe client, through <c>IBridgePipe</c>, against real named pipes. Every test uses its own pipe name, never the app's.</summary>
[Integration]
public class NamedPipeBridgePipeTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static string UniqueName() => $"Memoria.ToDo.Tests.{Guid.NewGuid():N}";

    private static NamedPipeServerStream Serve(string name) => new(
        name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    [Fact]
    public async Task Finds_no_pipe_when_none_has_the_name_and_does_not_wait_for_one()
    {
        IBridgePipe pipe = new NamedPipeBridgePipe(UniqueName());

        var stream = await pipe.TryConnectAsync(TestContext.Current.CancellationToken).WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.Null(stream);
    }

    [Fact]
    public async Task Connects_to_a_pipe_this_user_made_and_carries_bytes_both_ways()
    {
        var name = UniqueName();
        await using var server = Serve(name);
        var accepted = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        IBridgePipe pipe = new NamedPipeBridgePipe(name);

        await using var stream = await pipe.TryConnectAsync(TestContext.Current.CancellationToken).WaitAsync(Limit, TestContext.Current.CancellationToken);
        await accepted.WaitAsync(Limit, TestContext.Current.CancellationToken);

        Assert.NotNull(stream);
        // A pipe's flush waits until the other end has read, so each read starts before the write it waits for.
        var received = new byte[5];
        var receiving = server.ReadExactlyAsync(received, TestContext.Current.CancellationToken).AsTask();
        await stream.WriteAsync("ping\n"u8.ToArray(), TestContext.Current.CancellationToken);
        await stream.FlushAsync(TestContext.Current.CancellationToken).WaitAsync(Limit, TestContext.Current.CancellationToken);
        await receiving.WaitAsync(Limit, TestContext.Current.CancellationToken);
        Assert.Equal("ping\n"u8.ToArray(), received);

        var answer = new byte[5];
        var answering = stream.ReadExactlyAsync(answer, TestContext.Current.CancellationToken).AsTask();
        await server.WriteAsync("pong\n"u8.ToArray(), TestContext.Current.CancellationToken);
        await server.FlushAsync(TestContext.Current.CancellationToken).WaitAsync(Limit, TestContext.Current.CancellationToken);
        await answering.WaitAsync(Limit, TestContext.Current.CancellationToken);
        Assert.Equal("pong\n"u8.ToArray(), answer);
    }

    [Fact]
    public async Task Sees_the_end_of_the_stream_when_the_app_closes_the_pipe()
    {
        var name = UniqueName();
        var server = Serve(name);
        var accepted = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        IBridgePipe pipe = new NamedPipeBridgePipe(name);
        await using var stream = await pipe.TryConnectAsync(TestContext.Current.CancellationToken).WaitAsync(Limit, TestContext.Current.CancellationToken);
        await accepted.WaitAsync(Limit, TestContext.Current.CancellationToken);

        await server.DisposeAsync();

        var read = await stream!.ReadAsync(new byte[1], TestContext.Current.CancellationToken).AsTask().WaitAsync(Limit, TestContext.Current.CancellationToken);
        Assert.Equal(0, read);
    }
}
