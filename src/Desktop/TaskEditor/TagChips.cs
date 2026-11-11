using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The five most-used tags as buttons.</summary>
public sealed class TagChips : UserControl
{
    private const int ChipCount = 5;

    private readonly ITasks _tasks;
    private readonly ItemsControl _chips;

    public TagChips(ITasks tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        _tasks = tasks;
        _chips = ClickableItems.Create(ClickableItems.ItemButton(), tag => TagChosen?.Invoke(this, tag));
        AutomationProperties.SetAutomationId(this, "TagChips");
        Content = _chips;
        Refresh();
        WhileLoaded.Run(this, () =>
        {
            _tasks.Changed += OnTasksChanged;
            Refresh();
        }, () => _tasks.Changed -= OnTasksChanged);
    }

    /// <summary>Raised with the tag of the chip the user clicked.</summary>
    public event EventHandler<string>? TagChosen;

    private void OnTasksChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh() => _chips.ItemsSource = _tasks.TagsInUse().Take(ChipCount).Select(use => use.Tag).ToList();
}
