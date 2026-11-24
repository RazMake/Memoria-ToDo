using Grpc.Core;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>
/// Runs a call as the web server would, with no server: through the call interceptor into the service method, with a fake call
/// context. Shared test setup, not a fake of a port.
/// </summary>
internal sealed class CallRunner(CallInterceptor interceptor)
{
    public Task<TResponse> Unary<TRequest, TResponse>(
        string method, TRequest request, UnaryServerMethod<TRequest, TResponse> handler, CancellationToken cancellation)
        where TRequest : class where TResponse : class =>
        interceptor.UnaryServerHandler(request, new FakeServerCallContext(method, cancellation), handler);

    /// <summary>Opens the stream; the task ends when the stream does, and the stream ends when <paramref name="cancellation"/> is cancelled.</summary>
    public Task Stream<TRequest, TResponse>(
        string method, TRequest request, RecordingStreamWriter<TResponse> writer, ServerStreamingServerMethod<TRequest, TResponse> handler,
        CancellationToken cancellation)
        where TRequest : class where TResponse : class =>
        interceptor.ServerStreamingServerHandler(request, writer, new FakeServerCallContext(method, cancellation), handler);
}

/// <summary>The Contacts calls as a program makes them, with no server.</summary>
internal sealed class ContactsCaller(ContactsService service, CallRunner runner)
{
    public Task<V1.GroupList> ListGroupsAsync(CancellationToken cancellation = default) =>
        runner.Unary("ListGroups", new V1.ListGroupsRequest(), service.ListGroups, cancellation);

    public Task<V1.Contact> GetMeAsync() => runner.Unary("GetMe", new V1.GetMeRequest(), service.GetMe, default);

    public Task<V1.ContactList> SearchContactsAsync(string text) =>
        runner.Unary("SearchContacts", new V1.SearchContactsRequest { Text = text }, service.SearchContacts, default);

    public Task<V1.Contact> GetContactAsync(string name) =>
        runner.Unary("GetContact", new V1.GetContactRequest { Name = name }, service.GetContact, default);

    public Task<V1.Contact> SaveContactAsync(V1.SaveContactRequest request) =>
        runner.Unary("SaveContact", request, service.SaveContact, default);

    public Task<V1.DeleteContactResponse> DeleteContactAsync(string group, string id) =>
        runner.Unary("DeleteContact", new V1.DeleteContactRequest { Group = group, Id = id }, service.DeleteContact, default);

    public Task<V1.GroupList> MoveGroupAsync(V1.MoveGroupRequest request) =>
        runner.Unary("MoveGroup", request, service.MoveGroup, default);

    public Task<V1.Mention> MentionOfAsync(string contactId, string template) =>
        runner.Unary("MentionOf", new V1.MentionOfRequest { ContactId = contactId, Template = template }, service.MentionOf, default);

    /// <summary>Opens WatchContacts; the task ends with the stream.</summary>
    public Task WatchAsync(RecordingStreamWriter<V1.ChangeNotice> writer, CancellationToken cancellation) =>
        runner.Stream("WatchContacts", new V1.WatchContactsRequest(), writer, service.WatchContacts, cancellation);
}

/// <summary>The Sync calls as a program makes them, with no server.</summary>
internal sealed class SyncCaller(SyncApiService service, CallRunner runner)
{
    public Task<V1.SyncStatus> GetStatusAsync() => runner.Unary("GetStatus", new V1.GetStatusRequest(), service.GetStatus, default);

    public Task<V1.SyncStatus> SyncNowAsync(CancellationToken cancellation = default) =>
        runner.Unary("SyncNow", new V1.SyncNowRequest(), service.SyncNow, cancellation);

    /// <summary>Opens WatchStatus; the task ends with the stream.</summary>
    public Task WatchAsync(RecordingStreamWriter<V1.SyncStatus> writer, CancellationToken cancellation) =>
        runner.Stream("WatchStatus", new V1.WatchStatusRequest(), writer, service.WatchStatus, cancellation);
}

/// <summary>The Settings calls as a program makes them, with no server.</summary>
internal sealed class SettingsCaller(SettingsService service, CallRunner runner)
{
    public Task<V1.SharedSettings> GetSettingsAsync() =>
        runner.Unary("GetSettings", new V1.GetSettingsRequest(), service.GetSettings, default);

    public Task<V1.SharedSettings> SetDeletionDelayAsync(int days) =>
        runner.Unary("SetDeletionDelay", new V1.SetDeletionDelayRequest { DeletionDelayDays = days }, service.SetDeletionDelay, default);

    public Task<V1.SharedSettings> SetTagColorAsync(string tag, string color) =>
        runner.Unary("SetTagColor", new V1.SetTagColorRequest { Tag = tag, Color = color }, service.SetTagColor, default);

    public Task<V1.SharedSettings> MoveTagAsync(V1.MoveTagRequest request) =>
        runner.Unary("MoveTag", request, service.MoveTag, default);
}
