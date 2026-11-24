using System.ComponentModel;
using Memoria.ToDo.Shared.Tasks;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.McpServer.Tools;

/// <summary>The tools that read tasks.</summary>
internal sealed class TaskReadTools(ToolRunner run, CloneCheck clone, ITasks tasks)
{
    [McpServerTool(ReadOnly = true), Description("Lists every task, open and resolved, in the order the user sees them. Descriptions are left out; get_task has them.")]
    public Task<string> ListTasks() => run.Run(() =>
    {
        clone.Require();
        return TaskView.ListOf(tasks.All, withDescription: false);
    });

    [McpServerTool(ReadOnly = true), Description("Gets one task with its description, by ID.")]
    public Task<string> GetTask([Description("The task's ID.")] string id) => run.Run(() =>
    {
        clone.Require();
        return TaskView.Of(tasks.All.FirstOrDefault(task => task.Id == id) ?? throw new TaskNotFoundException(), withDescription: true);
    }, [id]);

    [McpServerTool(ReadOnly = true), Description("Finds the tasks whose title, description, or tags hold every word of the text, ignoring case, in the order the user sees them. Descriptions are left out; get_task has them.")]
    public Task<string> SearchTasks(
        [Description("The words to find.")] string text,
        [Description("Only tasks with this tag, ignoring case.")] string? tag = null) => run.Run(() =>
    {
        clone.Require();
        return TaskView.ListOf(string.IsNullOrEmpty(tag) ? tasks.Search(text) : tasks.Search(text, tag), withDescription: false);
    });

    [McpServerTool(ReadOnly = true), Description("Gets the address that links to a task, to put in another task's description as a Markdown link, such as [text](address).")]
    public Task<string> GetTaskLink([Description("The task's ID.")] string id) => run.Run(() =>
    {
        clone.Require();
        _ = tasks.All.FirstOrDefault(task => task.Id == id) ?? throw new TaskNotFoundException();
        return new { address = tasks.LinkAddress(id) };
    }, [id]);
}
