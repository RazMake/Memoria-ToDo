using System.Windows;
using System.Windows.Documents;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.FormattedView.Tests;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

// How a test types "@" in the search box and finds the card of a mention in a row's title.
internal sealed partial class TaskListScreen
{
    /// <summary>Gives the search box the focus and types the text into it one character at a time, as the keyboard does; "@" opens the contact list.</summary>
    public void TypeInSearch(string text)
    {
        FocusSearchBox();
        TaskEditor.Tests.EditorFields.TypeOnFocused(text);
        Refresh();
    }

    /// <summary>The contacts the search box's open "@" list offers, in order; empty while it is closed.</summary>
    public IReadOnlyList<string> ShownAliases => TaskEditor.Tests.EditorFields.ShownPopupMentions();

    /// <summary>The row's title, which shows its mentions and makes their cards.</summary>
    public FormattedTitle TitleOf(string rowTitle, string? group = null) =>
        Visuals(Row(rowTitle, group)).OfType<FormattedTitle>().Single();

    /// <summary>
    /// The card the row's title makes for the middle of the mention's chip when the text is a mention, or else for the point just inside
    /// the start of the title's own text; null when it makes none.
    /// </summary>
    public async Task<FrameworkElement?> CardAt(string rowTitle, string text, string? group = null)
    {
        Refresh();
        var title = TitleOf(rowTitle, group);
        Point point;
        if (title.Inlines.OfType<Run>().FirstOrDefault(r => r.Text.Contains(text)) is { } run)
        {
            var rect = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            point = new Point(rect.X + 2, rect.Y + rect.Height / 2);
        }
        else
        {
            var chip = title.ChipBox(text);
            point = new Point(chip.Left + (chip.Width / 2), chip.Top + (chip.Height / 2));
        }

        var part = await title.PartAt(point);
        return part?.Card;
    }

    /// <summary>
    /// The card the shown description makes for a mention, when its web view finds the mention with the number under the pointer, or null
    /// when it makes none. The description shows the selected task's, or the edit's Preview.
    /// </summary>
    public async Task<FrameworkElement?> DescriptionCardAt(int mentionNumber)
    {
        Refresh();
        Web.MentionUnderPointer = new MentionAt(mentionNumber, new Rect(10, 10, 40, 16));
        var part = await Visuals(Details).OfType<FormattedDescription>().Single().PartAt(new Point(20, 18));
        return part?.Card;
    }
}
