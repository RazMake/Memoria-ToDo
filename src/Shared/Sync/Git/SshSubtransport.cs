using LibGit2Sharp;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Registers Sync's SSH transport with libgit2, once for the process.</summary>
internal static class SshRegistration
{
    private static readonly Lazy<bool> Registered = new(static () =>
    {
        GlobalSettings.RegisterSmartSubtransport<SshSubtransport>("ssh");
        return true;
    });

    public static void Ensure() => _ = Registered.Value;
}

/// <summary>
/// libgit2's SSH transport over SSH.NET: one connection, one command per service. Everything it needs comes from the
/// <see cref="OperationScope"/> of the worker's running native call.
/// </summary>
internal sealed class SshSubtransport : SmartSubtransport
{
    private OperationScope? scope;
    private SshLink? link;
    private SshStream? stream;

    protected override SmartSubtransportStream Action(string url, GitSmartSubtransportAction action)
    {
        scope ??= OperationScope.Current;
        try
        {
            return action switch
            {
                GitSmartSubtransportAction.UploadPackList => Start(url, "git-upload-pack"),
                GitSmartSubtransportAction.ReceivePackList => Start(url, "git-receive-pack"),
                GitSmartSubtransportAction.UploadPack or GitSmartSubtransportAction.ReceivePack =>
                    stream ?? throw new InvalidOperationException("git asked for a pack exchange before listing the refs."),
                _ => throw new NotSupportedException($"git asked for an unknown SSH action: {action}."),
            };
        }
        catch (Exception ex)
        {
            scope.Record(ex);
            throw;
        }
    }

    protected override void Close()
    {
        // libgit2 frees the stream first, which sent EOF; the connection ends after its commands, awaited by Sync.
        stream = null;
        if (link is not null)
        {
            scope!.Connection.Cleanup.Add(link.CloseAsync());
            link = null;
        }

        base.Close();
    }

    private SshStream Start(string url, string service)
    {
        var address = AddressParser.Parse(url);
        link ??= SshLink.Open(address, scope!);
        return stream = new SshStream(this, link.Start(service, address.Path, scope!.Token));
    }

    private sealed class SshStream(SshSubtransport owner, SshCommandIo io) : SmartSubtransportStream(owner)
    {
        private readonly byte[] readBuffer = new byte[64 * 1024];
        private readonly byte[] writeBuffer = new byte[64 * 1024];

        public override int Read(Stream dataStream, long length, out long bytesRead)
        {
            try
            {
                var read = io.Read(readBuffer, (int)Math.Min(length, readBuffer.Length));
                dataStream.Write(readBuffer, 0, read);
                bytesRead = read;
                return 0;
            }
            catch (Exception ex)
            {
                OperationScope.Current.Record(ex);
                throw;
            }
        }

        public override int Write(Stream dataStream, long length)
        {
            try
            {
                for (var left = length; left > 0;)
                {
                    var read = dataStream.Read(writeBuffer, 0, (int)Math.Min(left, writeBuffer.Length));
                    if (read == 0)
                    {
                        throw new EndOfStreamException("libgit2 gave fewer bytes than it asked to write.");
                    }

                    io.Write(writeBuffer, read);
                    left -= read;
                }

                return 0;
            }
            catch (Exception ex)
            {
                OperationScope.Current.Record(ex);
                throw;
            }
        }

        protected override void Free()
        {
            try
            {
                io.SendEof();
            }
            finally
            {
                base.Free();
            }
        }
    }
}
