using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>
/// The direct changes of the server, with something that happens just before each update, such as the user saving the task between
/// the assistant's read and its update. Everything else goes straight to the real changes.
/// </summary>
internal sealed class DirectBeforeUpdate(IDirectTaskChanges inner, Action beforeUpdate) : IDirectTaskChanges
{
    public TaskItem Add(string title, string description, string? tag) => inner.Add(title, description, tag);

    public TaskItem Update(string taskId, DateTimeOffset baseUpdated, string? title, string? description, string? tag)
    {
        beforeUpdate();
        return inner.Update(taskId, baseUpdated, title, description, tag);
    }

    public TaskItem AddImage(string taskId, byte[] image, string altText) => inner.AddImage(taskId, image, altText);
}
