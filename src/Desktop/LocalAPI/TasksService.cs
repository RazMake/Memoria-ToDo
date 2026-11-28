using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Memoria.ToDo.Shared.Tasks;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The Tasks service: maps each call to <see cref="ITaskStore"/> and its <see cref="ITaskStore.Direct"/> changes. It lets a failure
/// escape; <see cref="CallInterceptor"/> maps it to a status.
/// </summary>
internal sealed class TasksService : V1.Tasks.TasksBase
{
    private readonly ITaskStore tasks;

    public TasksService(ITaskStore tasks) => this.tasks = tasks;

    /// <inheritdoc />
    public override Task<V1.TaskList> ListTasks(V1.ListTasksRequest request, ServerCallContext context)
    {
        RejectBoth(request.HasTag, request.Tags.Count > 0);
        var found = request.HasTag ? tasks.Search(request.Query, [request.Tag])
            : request.Tags.Count > 0 ? tasks.Search(request.Query, request.Tags)
            : tasks.Search(request.Query);
        var reply = new V1.TaskList();
        foreach (var task in found)
        {
            if (request.IncludeResolved || task.Resolved is null)
                reply.Tasks.Add(Item(task));
        }

        return Task.FromResult(reply);
    }

    /// <inheritdoc />
    public override Task<V1.TaskItem> GetTask(V1.GetTaskRequest request, ServerCallContext context) =>
        Task.FromResult(Item(tasks.All.FirstOrDefault(task => task.Id == request.Id) ?? throw new TaskNotFoundException()));

    /// <inheritdoc />
    public override Task<V1.TaskItem> CreateTask(V1.CreateTaskRequest request, ServerCallContext context)
    {
        RejectBoth(request.HasTag, request.Tags.Count > 0);
        return Task.FromResult(Item(NoticeFailures.Saved(() => tasks.Direct.Add(request.Title, request.Description, request.HasTag ? OlderTag(request.Tag) : request.Tags))));
    }

    /// <inheritdoc />
    public override Task<V1.TaskItem> UpdateTask(V1.UpdateTaskRequest request, ServerCallContext context)
    {
        if (request.BaseUpdated is null)
            throw new ArgumentException("UpdateTask needs base_updated, the task's updated time as the program read it.");

        RejectBoth(request.HasTag, request.Tags is not null);
        return Task.FromResult(Item(NoticeFailures.Saved(() => tasks.Direct.Update(
            request.Id, request.BaseUpdated.ToDateTimeOffset(),
            request.HasTitle ? request.Title : null,
            request.HasDescription ? request.Description : null,
            request.HasTag ? OlderTag(request.Tag) : request.Tags?.Names))));
    }

    /// <inheritdoc />
    public override Task<V1.TaskItem> MoveTask(V1.MoveTaskRequest request, ServerCallContext context) =>
        Task.FromResult(Item(NoticeFailures.Saved(() =>
            tasks.Move(request.Id, request.HasPreviousId ? request.PreviousId : null, request.HasNextId ? request.NextId : null))));

    /// <inheritdoc />
    public override Task<V1.TaskItem> ResolveTask(V1.ResolveTaskRequest request, ServerCallContext context) =>
        Task.FromResult(Item(NoticeFailures.Saved(() => tasks.Resolve(request.Id))));

    /// <inheritdoc />
    public override Task<V1.TaskItem> ReopenTask(V1.ReopenTaskRequest request, ServerCallContext context) =>
        Task.FromResult(Item(NoticeFailures.Saved(() => tasks.Reopen(request.Id))));

    /// <inheritdoc />
    public override Task<V1.DeleteTaskResponse> DeleteTask(V1.DeleteTaskRequest request, ServerCallContext context)
    {
        NoticeFailures.Saved(() => tasks.Delete(request.Id));
        return Task.FromResult(new V1.DeleteTaskResponse());
    }

    /// <inheritdoc />
    public override Task<V1.TaskItem> AddImage(V1.AddImageRequest request, ServerCallContext context)
    {
        // The host's receive limit leaves room for the fields around an image, so an image just over the promised size gets here.
        if (request.Image.Length > ApiLimits.MaxImageBytes)
            throw new RpcException(new Status(StatusCode.ResourceExhausted, $"An image can be at most {ApiLimits.MaxImageBytes} bytes."));

        return Task.FromResult(Item(NoticeFailures.Saved(() => tasks.Direct.AddImage(request.Id, request.Image.ToByteArray(), request.AltText))));
    }

    /// <inheritdoc />
    public override Task<V1.TagList> ListTags(V1.ListTagsRequest request, ServerCallContext context)
    {
        var reply = new V1.TagList();
        foreach (var use in tasks.TagsInUse())
            reply.Tags.Add(new V1.TagCount { Tag = use.Tag, Count = use.Count });
        return Task.FromResult(reply);
    }

    /// <inheritdoc />
    public override Task WatchTasks(V1.WatchTasksRequest request, IServerStreamWriter<V1.ChangeNotice> responseStream, ServerCallContext context) =>
        ChangeStream.SendNoticesAsync(handler => tasks.Changed += handler, handler => tasks.Changed -= handler, responseStream, context.CancellationToken);

    // A program that sets the older tag and the list learns of its mistake, since neither is told apart as the winner.
    private static void RejectBoth(bool hasTag, bool hasTags)
    {
        if (hasTag && hasTags)
            throw new ArgumentException("Set either tag or tags, not both.");
    }

    // The older tag is one tag, and only exactly "" means none; Tasks rejects a tag that is empty once normalized.
    private static string[] OlderTag(string tag) => tag.Length == 0 ? [] : [tag];

    private V1.TaskItem Item(TaskItem task)
    {
        var item = new V1.TaskItem
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Created = Timestamp.FromDateTimeOffset(task.Created),
            Updated = Timestamp.FromDateTimeOffset(task.Updated),
            Link = tasks.LinkAddress(task.Id),
        };
        item.Tags.AddRange(task.Tags);
        if (task.GroupTag is { } groupTag)
            item.Tag = groupTag;
        if (task.Resolved is { } resolved)
            item.Resolved = Timestamp.FromDateTimeOffset(resolved);
        return item;
    }
}
