using System.IO;
using Grpc.Core;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Fakes;
using Serilog.Events;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>What tests read from a call's outcome.</summary>
internal static class CallOutcome
{
    /// <summary>The status a call over the pipe ends with; fails when it succeeds.</summary>
    public static async Task<RpcException> FailsAsync<T>(AsyncUnaryCall<T> call) => await FailsAsync(call.ResponseAsync);

    /// <summary>The status a stream ends with; fails when it ends without a failure.</summary>
    public static async Task<RpcException> FailsAsync(Task call)
    {
        try
        {
            await call.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (RpcException failure)
        {
            return failure;
        }

        throw new Xunit.Sdk.XunitException("The stream ended without a failure, but was expected to fail.");
    }

    /// <summary>The status the call ends with; fails when it succeeds.</summary>
    public static async Task<RpcException> FailsAsync<T>(Task<T> call)
    {
        try
        {
            await call;
        }
        catch (RpcException failure)
        {
            return failure;
        }

        throw new Xunit.Sdk.XunitException("The call succeeded, but was expected to fail.");
    }

    /// <summary>
    /// Waits for an open stream to end, as it does when the client cancels or the app stops. Ending normally, cancelled, or
    /// <c>UNAVAILABLE</c> all count; any other status, or a stream that never ends, fails.
    /// </summary>
    public static async Task EndedAsync(Task stream)
    {
        try
        {
            await stream.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (RpcException ended) when (ended.StatusCode is StatusCode.Cancelled or StatusCode.Unavailable)
        {
        }
    }

    /// <summary>Whether the entry names the status, by its .NET name (NotFound) or its gRPC name (NOT_FOUND).</summary>
    public static bool NamesStatus(this LogEvent entry, StatusCode code)
    {
        var text = entry.EverythingIn();
        var grpcName = System.Text.RegularExpressions.Regex.Replace(code.ToString(), "(?<!^)([A-Z])", "_$1").ToUpperInvariant();
        return text.Contains(code.ToString(), StringComparison.Ordinal) || text.Contains(grpcName, StringComparison.Ordinal);
    }
}

/// <summary>
/// The Tasks calls as a program makes them, with no server: each runs through the call interceptor into the service class, with a
/// fake call context, as the web server would run it. Shared test setup, not a fake of a port.
/// </summary>
internal sealed class TasksCaller(TasksService service, CallRunner runner)
{
    public Task<V1.TaskList> ListTasksAsync(V1.ListTasksRequest request, CancellationToken cancellationToken = default) =>
        Run("ListTasks", request, service.ListTasks, cancellationToken);

    public Task<V1.TaskItem> GetTaskAsync(V1.GetTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("GetTask", request, service.GetTask, cancellationToken);

    public Task<V1.TaskItem> CreateTaskAsync(V1.CreateTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("CreateTask", request, service.CreateTask, cancellationToken);

    public Task<V1.TaskItem> UpdateTaskAsync(V1.UpdateTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("UpdateTask", request, service.UpdateTask, cancellationToken);

    public Task<V1.TaskItem> MoveTaskAsync(V1.MoveTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("MoveTask", request, service.MoveTask, cancellationToken);

    public Task<V1.TaskItem> ResolveTaskAsync(V1.ResolveTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("ResolveTask", request, service.ResolveTask, cancellationToken);

    public Task<V1.TaskItem> ReopenTaskAsync(V1.ReopenTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("ReopenTask", request, service.ReopenTask, cancellationToken);

    public Task<V1.DeleteTaskResponse> DeleteTaskAsync(V1.DeleteTaskRequest request, CancellationToken cancellationToken = default) =>
        Run("DeleteTask", request, service.DeleteTask, cancellationToken);

    public Task<V1.TaskItem> AddImageAsync(V1.AddImageRequest request, CancellationToken cancellationToken = default) =>
        Run("AddImage", request, service.AddImage, cancellationToken);

    public Task<V1.TagList> ListTagsAsync(V1.ListTagsRequest request, CancellationToken cancellationToken = default) =>
        Run("ListTags", request, service.ListTags, cancellationToken);

    /// <summary>Opens WatchTasks; the task ends with the stream.</summary>
    public Task WatchAsync(RecordingStreamWriter<V1.ChangeNotice> writer, CancellationToken cancellation) =>
        runner.Stream("WatchTasks", new V1.WatchTasksRequest(), writer, service.WatchTasks, cancellation);

    private Task<TResponse> Run<TRequest, TResponse>(
        string method, TRequest request, UnaryServerMethod<TRequest, TResponse> handler, CancellationToken cancellation)
        where TRequest : class where TResponse : class =>
        runner.Unary(method, request, handler, cancellation);
}

/// <summary>
/// The Local API's parts on the shared fakes and a <see cref="FakeApiHost"/>, with no server and no pipe. Contacts is the real module
/// over an in-memory file system: seed contact files into <see cref="Files"/> under <see cref="Clone"/>, then <see cref="LoadContacts"/>.
/// </summary>
internal sealed class ApiHarness : IAsyncDisposable
{
    public const string PipeName = "Memoria-ToDo.LocalApi.test-domain.test-user";

    public FakeTaskStore Store { get; } = new();

    /// <summary>The in-memory file system under Contacts.</summary>
    public FakeFileSystem Files { get; } = new();

    /// <summary>The clone folder Contacts reads once <see cref="LoadContacts"/> runs. Nothing is real on disk.</summary>
    public string Clone { get; } = Path.Combine(Path.GetTempPath(), "memoria-localapi-tests", "clone");

    public IContacts Contacts { get; }

    public FakeSyncService Sync { get; } = new();

    /// <summary>Selects the clone, so Contacts reads the files seeded so far.</summary>
    public void LoadContacts() => Contacts.UseRepositoryFolder(Clone);

    /// <summary>The contact file at the clone-relative path, parsed.</summary>
    public ContactFiles.Parsed ParsedContactFile(string relative) => ContactFiles.Read(Files.ReadFile(TestContactStore.PathOf(Clone, relative)));

    public FakeApiHost Host { get; }

    /// <summary>The entries the Local API logged, through the logger it was given.</summary>
    public SignalLogSink Log { get; } = new();

    /// <summary>The lifetime, which is also the gate the calls check.</summary>
    public LocalApiLifetime Lifetime { get; }

    public TasksCaller Tasks { get; }

    public ContactsCaller ContactCalls { get; }

    public SyncCaller SyncCalls { get; }

    public SettingsCaller SettingsCalls { get; }

    /// <summary>Builds the parts. <paramref name="stopWait"/> is how long a stop waits for running calls, short for a test of giving up.</summary>
    public ApiHarness(FakeApiHost? host = null, TimeSpan? stopWait = null)
    {
        Contacts = TestContactStore.Create(Files).Contacts;
        Host = host ?? new FakeApiHost();
        var logger = Log.CreateLogger();
        Lifetime = new LocalApiLifetime(Host, PipeName, logger, stopWait ?? LocalApiLifetime.DefaultStopWait);
        var runner = new CallRunner(new CallInterceptor(Lifetime, logger));
        Tasks = new TasksCaller(new TasksService(Store), runner);
        ContactCalls = new ContactsCaller(new ContactsService(Contacts), runner);
        SyncCalls = new SyncCaller(new SyncApiService(Sync), runner);
        SettingsCalls = new SettingsCaller(new SettingsService(Store), runner);
    }

    public async ValueTask DisposeAsync() => await Lifetime.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));
}
