using System.ComponentModel;
using Memoria.ToDo.Shared.Tasks;
using ModelContextProtocol.Server;

namespace Memoria.ToDo.Desktop.MCPServer.Tools;

/// <summary>The tools for tags: which are in use, their colors, and their order.</summary>
internal sealed class TagTools(ToolRunner run, CloneCheck clone, ITasks tasks, ITagColors colors, ITagOrder order)
{
    [McpServerTool(ReadOnly = true), Description("Lists the tags in use, in the order the user chose, with how many tasks have each and its color, if it has one.")]
    public Task<string> ListTags() => run.Run(() =>
    {
        clone.Require();
        return TagsInOrder();
    });

    [McpServerTool(Destructive = false), Description("Sets a tag's color, by color name. Refuses a name that isn't a color.")]
    public Task<string> SetTagColor(
        [Description("The tag.")] string tag,
        [Description("The color's name.")] string color) => run.Run(() =>
        ToolRunner.Saved(() => colors.SetColor(tag, color), () => new { tag, color }));

    [McpServerTool(Destructive = false), Description("Moves a tag to sit after previousTag and before nextTag, which must be neighbors; leave out previousTag for the start, nextTag for the end. Answers the tags in their new order.")]
    public Task<string> MoveTag(
        [Description("The tag to move.")] string tag,
        [Description("The tag it sits after.")] string? previousTag = null,
        [Description("The tag it sits before.")] string? nextTag = null) => run.Run(() =>
        ToolRunner.Saved(() => order.Move(tag, previousTag, nextTag), TagsInOrder));

    private object TagsInOrder()
    {
        var counts = tasks.TagsInUse().ToDictionary(use => use.Tag, use => use.Count);
        return new
        {
            tags = order.Sort(counts.Keys).Select(tag => new { tag, count = counts[tag], color = colors.ColorOf(tag) }).ToList(),
        };
    }
}
