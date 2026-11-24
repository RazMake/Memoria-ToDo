using Grpc.Core;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>The context of a call the test makes directly on a service class, with no server. Shared test setup, not a fake of a port.</summary>
internal sealed class FakeServerCallContext(string method, CancellationToken cancellation) : ServerCallContext
{
    /// <summary>The method name a real call carries, such as <c>/memoria.todo.v1.Tasks/ListTasks</c>.</summary>
    protected override string MethodCore => "/memoria.todo.v1.Tasks/" + method;

    protected override string HostCore => "localhost";

    protected override string PeerCore => "pipe:test";

    protected override DateTime DeadlineCore => DateTime.MaxValue;

    protected override Metadata RequestHeadersCore { get; } = [];

    protected override CancellationToken CancellationTokenCore => cancellation;

    protected override Metadata ResponseTrailersCore { get; } = [];

    protected override Status StatusCore { get; set; }

    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore { get; } = new(null, new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
