using System.ComponentModel;
using Memoria.ToDo.Shared.Tasks;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>The tools that change tasks.</summary>
internal sealed class TaskChangeTools(ToolRunner run, CloneCheck clone, ITasks tasks, IDirectTaskChanges direct)
{
    private const int MaxImageBytes = 20 * 1024 * 1024;

    [McpServerTool(Destructive = false), Description("Adds a task at the top of the list and answers it. Use the same tag spelling the user already uses; list_tags shows them.")]
    public Task<string> AddTask(
        [Description("The title: one line, not empty.")] string title,
        [Description("The description, in Markdown. Empty when left out.")] string? description = null,
        [Description("One tag.")] string? tag = null)
    {
        string? addedId = null;
        return run.Run(() =>
        {
            var added = direct.Add(title, description ?? "", tag);
            addedId = added.Id;
            return TaskView.Of(added, withDescription: true);
        }, resultId: _ => addedId);
    }

    [McpServerTool(Destructive = false), Description("Changes a task's title, description, or tag, and answers it. What you leave out stays as it is; an empty tag removes every tag. If the task changed since this call read it, it fails and you should read the task again and retry.")]
    public Task<string> EditTask(
        [Description("The task's ID.")] string id,
        [Description("The new title.")] string? title = null,
        [Description("The new description, in Markdown.")] string? description = null,
        [Description("The new tag; empty removes it.")] string? tag = null) => run.Run(() =>
    {
        clone.Require();
        var started = tasks.All.FirstOrDefault(task => task.Id == id) ?? throw new TaskNotFoundException();
        return TaskView.Of(direct.Update(id, started.Updated, title, description, tag), withDescription: true);
    }, [id]);

    [McpServerTool(Destructive = false), Description("Saves an image with a task, appends the Markdown that shows it to the end of the task's description, and answers the task. The image is shrunk to at most 1 MB.")]
    public Task<string> AddImage(
        [Description("The task's ID.")] string id,
        [Description("The image file's bytes, in base64, at most 20 MB.")] string image,
        [Description("What the image shows, for people who can't see it.")] string altText) => run.Run(() =>
    {
        clone.Require();
        if (DecodedSize(image) > MaxImageBytes)
        {
            throw new ArgumentException("The image is larger than 20 MB.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(image);
        }
        catch (FormatException)
        {
            throw new ArgumentException("The image isn't valid base64.");
        }

        return TaskView.Of(direct.AddImage(Existing(id), bytes, altText), withDescription: true);
    }, [id]);

    [McpServerTool(Destructive = false), Description("Moves a task to show after previousId and before nextId, which must be neighbors. Leave out previousId to move it to the top, nextId to move it to the bottom. Answers the task.")]
    public Task<string> MoveTask(
        [Description("The task's ID.")] string id,
        [Description("The ID of the task it shows after.")] string? previousId = null,
        [Description("The ID of the task it shows before.")] string? nextId = null) => run.Run(() =>
    {
        clone.Require();
        return TaskView.Of(tasks.Move(Existing(id), ExistingOrNull(previousId), ExistingOrNull(nextId)), withDescription: true);
    }, [id, previousId, nextId]);

    [McpServerTool(Destructive = false), Description("Marks a task resolved, as the user does; it is deleted after the deletion delay. reopen_task undoes it. Answers the task.")]
    public Task<string> ResolveTask([Description("The task's ID.")] string id) => run.Run(() =>
    {
        clone.Require();
        return TaskView.Of(tasks.Resolve(id), withDescription: true);
    }, [id]);

    [McpServerTool(Destructive = false), Description("Makes a resolved task open again. Answers the task.")]
    public Task<string> ReopenTask([Description("The task's ID.")] string id) => run.Run(() =>
    {
        clone.Require();
        return TaskView.Of(tasks.Reopen(id), withDescription: true);
    }, [id]);

    [McpServerTool(Destructive = true), Description("Deletes a task and its images for good; this can't be undone. To finish a task, use resolve_task, which can be undone with reopen_task.")]
    public Task<string> DeleteTask([Description("The task's ID.")] string id) => run.Run(() =>
    {
        clone.Require();
        tasks.Delete(Existing(id));
        return new { deleted = id };
    }, [id]);

    // Tasks refuses a malformed ID without saying it is unknown, so an ID that names no task is refused here as not found.
    private string Existing(string id) =>
        tasks.All.Any(task => task.Id == id) ? id : throw new TaskNotFoundException();

    private string? ExistingOrNull(string? id) => id is null ? null : Existing(id);

    // The size the base64 text decodes to, worked out from its length so a huge image is refused before it is decoded.
    private static long DecodedSize(string base64)
    {
        var padding = base64.EndsWith("==", StringComparison.Ordinal) ? 2 : base64.EndsWith('=') ? 1 : 0;
        return (long)base64.Length / 4 * 3 - padding;
    }
}
