using System.IO;
using Grpc.Core;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The Settings service: maps each call to the task store's shared settings, tag colors, and tag order, and Rendering's 30 tag colors.
/// A settings or tags file that can't be read is <c>FAILED_PRECONDITION</c> here, where the <see cref="StatusMap"/> by type says
/// <c>INVALID_ARGUMENT</c>, since no argument caused it; a skipped one is <c>FAILED_PRECONDITION</c> through the map.
/// </summary>
internal sealed class SettingsService : V1.Settings.SettingsBase
{
    private readonly ITaskStore tasks;

    public SettingsService(ITaskStore tasks) => this.tasks = tasks;

    /// <inheritdoc />
    public override Task<V1.SharedSettings> GetSettings(V1.GetSettingsRequest request, ServerCallContext context)
    {
        tasks.RequireReadableSettings();
        return Task.FromResult(Settings());
    }

    private int DeletionDelayDays()
    {
        try
        {
            return tasks.Settings.DeletionDelayDays;
        }
        catch (InvalidDataException unreadable)
        {
            throw Unreadable(unreadable);
        }
    }

    /// <inheritdoc />
    public override Task<V1.SharedSettings> SetDeletionDelay(V1.SetDeletionDelayRequest request, ServerCallContext context)
    {
        tasks.RequireReadableSettings();
        try
        {
            NoticeFailures.Saved(() => tasks.Settings.SetDeletionDelayDays(request.DeletionDelayDays));
        }
        catch (InvalidDataException unreadable)
        {
            throw Unreadable(unreadable);
        }

        return Task.FromResult(Settings());
    }

    /// <inheritdoc />
    public override Task<V1.SharedSettings> SetTagColor(V1.SetTagColorRequest request, ServerCallContext context)
    {
        if (TagColors.Find(request.Color) is null)
            throw new ArgumentException($"'{request.Color}' isn't one of the {TagColors.All.Count} tag color names.");

        tasks.RequireReadableSettings();
        try
        {
            NoticeFailures.Saved(() => tasks.TagColors.SetColor(request.Tag, request.Color));
        }
        catch (InvalidDataException unreadable)
        {
            throw Unreadable(unreadable);
        }

        return Task.FromResult(Settings());
    }

    /// <inheritdoc />
    public override Task<V1.SharedSettings> MoveTag(V1.MoveTagRequest request, ServerCallContext context)
    {
        tasks.RequireReadableSettings();
        try
        {
            NoticeFailures.Saved(() => tasks.TagOrder.Move(request.Tag, request.HasPreviousTag ? request.PreviousTag : null, request.HasNextTag ? request.NextTag : null));
        }
        catch (InvalidDataException unreadable)
        {
            throw Unreadable(unreadable);
        }

        return Task.FromResult(Settings());
    }

    private static RpcException Unreadable(InvalidDataException unreadable) => RpcFailure.Of(StatusCode.FailedPrecondition, unreadable.Message);

    private V1.SharedSettings Settings()
    {
        var days = DeletionDelayDays();

        var reply = new V1.SharedSettings { DeletionDelayDays = days };
        var inUse = tasks.TagsInUse();
        var names = new string[inUse.Count];
        for (var i = 0; i < names.Length; i++)
            names[i] = inUse[i].Tag;

        foreach (var tag in tasks.TagOrder.Sort(names))
        {
            var kept = tasks.TagColors.ColorOf(tag);
            reply.Tags.Add(new V1.TagLook { Tag = tag, Color = kept ?? TagColors.DefaultFor(tag).Name, ColorIsDefault = kept is null });
        }

        foreach (var color in TagColors.All)
            reply.Colors.Add(color.Name);
        return reply;
    }
}
