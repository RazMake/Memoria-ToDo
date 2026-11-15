using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>A group's heading on the left side: the tag as the Task editor's badge, or "No tag" for the untagged tasks.</summary>
internal sealed class GroupHeading : Border
{
    public const string NoTagName = "No tag";

    public GroupHeading(string? tag, ITagColors tagColors, ITheme theme)
    {
        Margin = new Thickness(0, 12, 0, 4);
        AutomationProperties.SetAutomationId(this, "GroupHeading");
        AutomationProperties.SetName(this, tag ?? NoTagName);
        Child = tag is null
            ? new TextBlock { Text = NoTagName, FontWeight = FontWeights.SemiBold }
            : new TagBadges(tagColors, theme) { Tags = [tag], HorizontalAlignment = HorizontalAlignment.Left };
    }
}
