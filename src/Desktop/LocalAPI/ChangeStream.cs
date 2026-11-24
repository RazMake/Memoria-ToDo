using System.Threading.Channels;
using Grpc.Core;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// Runs a watch stream: sends one message at once, then one after each change the event raises. Changes that come while a write is
/// still running make a single pending send, never a queue, so a slow reader costs no memory. The event can fire on any thread; its
/// handler never waits.
/// </summary>
internal static class ChangeStream
{
    private static readonly V1.ChangeNotice Notice = new();

    /// <summary>An empty notice at once, then one after each change.</summary>
    public static Task SendNoticesAsync(
        Action<EventHandler> subscribe, Action<EventHandler> unsubscribe, IServerStreamWriter<V1.ChangeNotice> stream, CancellationToken cancellation) =>
        SendAsync(subscribe, unsubscribe, stream, static () => Notice, cancellation);

    /// <summary>
    /// What <paramref name="current"/> gives at once, then again after each change, read when the send starts, so a reader that
    /// falls behind gets only the newest. Ends by throwing <see cref="OperationCanceledException"/> when <paramref name="callCancelled"/>
    /// is cancelled or the app stops.
    /// </summary>
    public static async Task SendAsync<T>(
        Action<EventHandler> subscribe, Action<EventHandler> unsubscribe, IServerStreamWriter<T> stream, Func<T> current,
        CancellationToken callCancelled)
    {
        using var ends = CancellationTokenSource.CreateLinkedTokenSource(callCancelled, AppStop.Token);
        var cancellation = ends.Token;
        var pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });
        EventHandler onChange = (_, _) => pending.Writer.TryWrite(true);

        // Listening starts before the first send, so a change that comes meanwhile isn't lost.
        subscribe(onChange);
        try
        {
            await stream.WriteAsync(current());
            while (await pending.Reader.WaitToReadAsync(cancellation))
            {
                // Taken before the send, so a change during the send makes the next one.
                pending.Reader.TryRead(out _);
                await stream.WriteAsync(current());
            }
        }
        finally
        {
            unsubscribe(onChange);
        }
    }
}
