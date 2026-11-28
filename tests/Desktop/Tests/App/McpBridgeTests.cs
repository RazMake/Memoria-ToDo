using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The bridge an AI assistant starts with <c>--mcp</c>, through <c>McpBridge</c>: it copies standard input and output
/// to and from the app's pipe, connects at once when the app runs, otherwise starts the app and waits at most 30 seconds
/// for the pipe, and on failure writes a message to standard error and ends with a non-zero code. The app, the pipe, the
/// streams, and the clock are fakes, so no process or pipe is started.
/// </summary>
[Unit]
public class McpBridgeTests
{
    private static readonly TimeSpan ThirtySeconds = TimeSpan.FromSeconds(30);

    private readonly FakeBridgeApp _app = new();
    private readonly FakeBridgePipe _pipe = new();

    private BridgeRun Run() => new(_app, _pipe);

    public class When_the_app_is_already_running : McpBridgeTests
    {
        [Fact]
        public async Task Connects_to_the_pipe_and_starts_nothing()
        {
            _app.Running = true;
            _pipe.Appear(out var app);
            var run = Run();

            run.Start();
            await run.Pipe.Connected.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

            Assert.Equal(0, _app.Starts);
            run.AssistantWrites.CompleteWriting();
            Assert.Equal(0, await run.ExitCodeAsync());
            app.Dispose();
        }

        [Fact]
        public async Task Starts_nothing_and_waits_for_the_pipe_while_the_app_has_none_yet_then_connects_when_it_appears()
        {
            _app.Running = true;
            var run = Run();
            run.Start();
            await run.ElapseAsync(TimeSpan.FromSeconds(5));

            _pipe.Appear(out var app);
            await run.ElapseUntilAsync(run.Pipe.Connected, ThirtySeconds - TimeSpan.FromSeconds(6));
            run.AssistantWrites.WriteText("hello\n");

            Assert.Equal("hello\n", System.Text.Encoding.UTF8.GetString(await app.ReadExactlyAsync(6)));
            Assert.Equal(0, _app.Starts);
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
        }

        [Fact]
        public async Task Fails_after_at_most_30_seconds_telling_the_user_to_turn_on_AI_assistant_access_in_Configure()
        {
            _app.Running = true;
            var run = Run();
            run.Start();
            await run.ElapseAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.Ended);

            await run.ElapseUntilEndedAsync(ThirtySeconds - TimeSpan.FromSeconds(10));

            Assert.NotEqual(0, await run.ExitCodeAsync());
            Assert.Contains("Configure", run.Error);
            Assert.Equal(0, _app.Starts);
        }
    }

    public class When_the_pipe_exists : McpBridgeTests
    {
        [Fact]
        public async Task Connects_directly_and_starts_nothing_even_when_the_one_instance_signal_is_not_seen()
        {
            _app.Running = false;
            _pipe.Appear(out var app);
            var run = Run();

            run.Start();
            await run.Pipe.Connected.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

            Assert.Equal(0, _app.Starts);
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
            app.Dispose();
        }

        [Fact]
        public async Task Copies_what_the_assistant_writes_to_the_pipe_unchanged()
        {
            _pipe.Appear(out var app);
            var run = Run();
            run.Start();
            const string line = "{\"jsonrpc\":\"2.0\",\"method\":\"ping\",\"note\":\"é ✓\"}\r\n";

            run.AssistantWrites.WriteText(line);

            var expected = System.Text.Encoding.UTF8.GetBytes(line);
            Assert.Equal(expected, await app.ReadExactlyAsync(expected.Length));
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
        }

        [Fact]
        public async Task Copies_what_the_app_writes_to_its_standard_output_unchanged()
        {
            _pipe.Appear(out var app);
            var run = Run();
            run.Start();
            byte[] reply = [0x7B, 0xC3, 0xA9, 0xFF, 0x00, 0x0D, 0x0A, 0x7D];

            app.Write(reply, 0, reply.Length);

            Assert.Equal(reply, await run.AssistantReads.ReadExactlyAsync(reply.Length));
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
        }

        [Fact]
        public async Task Copies_both_ways_at_once()
        {
            _pipe.Appear(out var app);
            var run = Run();
            run.Start();

            run.AssistantWrites.WriteText("request\n");
            app.WriteText("response\n");

            Assert.Equal("request\n", System.Text.Encoding.UTF8.GetString(await app.ReadExactlyAsync(8)));
            Assert.Equal("response\n", System.Text.Encoding.UTF8.GetString(await run.AssistantReads.ReadExactlyAsync(9)));
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
        }

        [Fact]
        public async Task Ends_with_zero_and_closes_the_pipe_when_the_assistant_closes_its_input()
        {
            _pipe.Appear(out var app);
            var run = Run();
            run.Start();

            run.AssistantWrites.CompleteWriting();

            Assert.Equal(0, await run.ExitCodeAsync());
            Assert.Equal(string.Empty, await app.ReadToEndAsync());
            Assert.Equal(string.Empty, run.Error);
        }

        [Fact]
        public async Task Ends_with_zero_when_the_app_closes_the_pipe_though_the_assistants_input_stays_open()
        {
            _pipe.Appear(out var app);
            var run = Run();
            run.Start();
            app.WriteText("last\n");
            await run.AssistantReads.ReadExactlyAsync(5);

            app.Dispose();

            Assert.Equal(0, await run.ExitCodeAsync());
            Assert.Equal(string.Empty, run.Error);
        }
    }

    public class When_there_is_no_pipe_and_the_app_does_not_run : McpBridgeTests
    {
        [Fact]
        public async Task Starts_the_app_once_and_connects_when_its_pipe_appears()
        {
            MemoryDuplex? appEnd = null;
            _app.OnStart = () => _pipe.Appear(out appEnd);
            var run = Run();

            run.Start();
            await run.ElapseUntilAsync(run.Pipe.Connected, TimeSpan.FromSeconds(29));
            run.AssistantWrites.WriteText("hi\n");

            Assert.Equal("hi\n", System.Text.Encoding.UTF8.GetString(await appEnd!.ReadExactlyAsync(3)));
            Assert.Equal(1, _app.Starts);
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
        }

        [Fact]
        public async Task Waits_for_a_pipe_that_appears_some_seconds_after_the_start()
        {
            var run = Run();
            run.Start();
            await run.ElapseAsync(TimeSpan.FromSeconds(12));
            Assert.False(run.Ended);

            _pipe.Appear(out var app);
            await run.ElapseUntilAsync(run.Pipe.Connected, TimeSpan.FromSeconds(17));
            app.WriteText("up\n");

            Assert.Equal("up\n", System.Text.Encoding.UTF8.GetString(await run.AssistantReads.ReadExactlyAsync(3)));
            Assert.Equal(1, _app.Starts);
            run.AssistantWrites.CompleteWriting();
            await run.ExitCodeAsync();
        }

        [Fact]
        public async Task Fails_after_at_most_30_seconds_with_a_message_on_standard_error_and_a_non_zero_code()
        {
            var run = Run();
            run.Start();
            await run.ElapseAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.Ended);

            await run.ElapseUntilEndedAsync(ThirtySeconds - TimeSpan.FromSeconds(10));

            Assert.NotEqual(0, await run.ExitCodeAsync());
            Assert.False(string.IsNullOrWhiteSpace(run.Error));
            Assert.Equal(1, _app.Starts);
        }

        [Fact]
        public async Task Fails_at_once_without_waiting_when_the_app_cannot_be_started_and_says_why()
        {
            _app.StartFails = new InvalidOperationException("The starter is missing.");
            var run = Run();

            run.Start();

            Assert.NotEqual(0, await run.ExitCodeAsync());
            Assert.Contains("The starter is missing.", run.Error);
            Assert.Equal(1, _app.Starts);
        }
    }

    public class When_the_pipe_cannot_be_used : McpBridgeTests
    {
        [Fact]
        public async Task Fails_at_once_with_the_reason_on_standard_error_and_a_non_zero_code_and_starts_nothing()
        {
            _pipe.Fails = new UnauthorizedAccessException("The pipe belongs to another user.");
            var run = Run();

            run.Start();

            Assert.NotEqual(0, await run.ExitCodeAsync());
            Assert.Contains("The pipe belongs to another user.", run.Error);
            Assert.Equal(0, _app.Starts);
        }
    }
}
