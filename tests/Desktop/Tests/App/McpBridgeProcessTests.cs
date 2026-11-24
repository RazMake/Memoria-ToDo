using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The app's program started with <c>--mcp</c>, as an assistant starts it, against a real pipe. The app's own pipe
/// name is the only one the bridge knows, so these tests make a pipe of that name and need the real app not to run;
/// they fail with a message that says so when it does. Starting the app, when no pipe exists, isn't tried here: it
/// would start the user's real app, so the unit tests cover it behind fakes.
/// </summary>
[Integration]
[Collection(BridgeProcessCollection.Name)]
public class McpBridgeProcessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static readonly string OneInstanceName = $"{AppName.Value}.OneInstance";

    private static string ProgramPath() => Path.Combine(AppContext.BaseDirectory, "Memoria.ToDo.Desktop.App.exe");

    private static NamedPipeServerStream ServeTheAppsPipe()
    {
        Assert.False(
            NamedSignalOneInstance.IsClaimed(OneInstanceName),
            "The real Memoria ToDo app is running. Quit it, then run this test again.");
        try
        {
            return new NamedPipeServerStream(
                PipeName.ForCurrentUser(), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }
        catch (IOException failure)
        {
            throw new Xunit.Sdk.XunitException($"Something already holds the app's pipe name. Quit the real Memoria ToDo app, then run this test again. {failure.Message}");
        }
    }

    private static Process StartBridge() => Process.Start(new ProcessStartInfo(ProgramPath(), "--mcp")
    {
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardInputEncoding = new UTF8Encoding(false),
        StandardOutputEncoding = new UTF8Encoding(false),
    })!;

    private static async Task<string> ReadLineFromAsync(Stream stream)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one, TestContext.Current.CancellationToken).AsTask().WaitAsync(Limit);
            Assert.Equal(1, read);
            bytes.Add(one[0]);
            if (one[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
        }
    }

    private static async Task ExitsAsync(Process bridge, int expectedCode)
    {
        await bridge.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(Limit);
        Assert.Equal(expectedCode, bridge.ExitCode);
    }

    private static void Stop(Process bridge)
    {
        if (!bridge.HasExited)
        {
            bridge.Kill(entireProcessTree: true);
        }

        bridge.Dispose();
    }

    private static string[] LogFiles() => Directory.Exists(AppLog.Folder)
        ? Directory.GetFiles(AppLog.Folder)
            .Select(file => $"{file}|{new FileInfo(file).Length}|{File.GetLastWriteTimeUtc(file):O}")
            .Order()
            .ToArray()
        : [];

    [Fact]
    public async Task Copies_a_request_to_the_apps_pipe_and_the_reply_to_standard_output_unchanged_and_ends_with_zero_when_input_closes()
    {
        await using var server = ServeTheAppsPipe();
        var accepted = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        var bridge = StartBridge();
        try
        {
            await accepted.WaitAsync(Limit, TestContext.Current.CancellationToken);

            await bridge.StandardInput.WriteAsync("{\"id\":1,\"text\":\"é ✓\"}\n");
            await bridge.StandardInput.FlushAsync(TestContext.Current.CancellationToken);
            Assert.Equal("{\"id\":1,\"text\":\"é ✓\"}\n", await ReadLineFromAsync(server));

            await server.WriteAsync(Encoding.UTF8.GetBytes("{\"id\":1,\"result\":\"ok ✓\"}\n"), TestContext.Current.CancellationToken);
            await server.FlushAsync(TestContext.Current.CancellationToken);
            Assert.Equal("{\"id\":1,\"result\":\"ok ✓\"}\n", await ReadLineFromAsync(bridge.StandardOutput.BaseStream));

            bridge.StandardInput.Close();

            await ExitsAsync(bridge, 0);
        }
        finally
        {
            Stop(bridge);
        }
    }

    [Fact]
    public async Task Ends_with_zero_when_the_app_closes_the_pipe()
    {
        var server = ServeTheAppsPipe();
        var accepted = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        var bridge = StartBridge();
        try
        {
            await accepted.WaitAsync(Limit, TestContext.Current.CancellationToken);

            await server.DisposeAsync();

            await ExitsAsync(bridge, 0);
        }
        finally
        {
            Stop(bridge);
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Does_not_claim_the_one_instance_while_it_runs()
    {
        await using var server = ServeTheAppsPipe();
        var accepted = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        var bridge = StartBridge();
        try
        {
            await accepted.WaitAsync(Limit, TestContext.Current.CancellationToken);

            Assert.False(NamedSignalOneInstance.IsClaimed(OneInstanceName));
        }
        finally
        {
            Stop(bridge);
        }
    }

    [Fact]
    public async Task Writes_no_log_files()
    {
        await using var server = ServeTheAppsPipe();
        var before = LogFiles();
        var accepted = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        var bridge = StartBridge();
        try
        {
            await accepted.WaitAsync(Limit, TestContext.Current.CancellationToken);
            bridge.StandardInput.Close();
            await ExitsAsync(bridge, 0);

            Assert.Equal(before, LogFiles());
        }
        finally
        {
            Stop(bridge);
        }
    }
}

/// <summary>The bridge tests share the app's one pipe name, so they run one after another.</summary>
[CollectionDefinition(Name)]
public sealed class BridgeProcessCollection
{
    public const string Name = "Bridge process";
}
