using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
public class TagChipsTests
{
    [WpfFact]
    public void Shows_no_chips_while_no_tag_is_in_use()
    {
        using var host = new ChipsHost();

        Assert.Empty(host.Tags);
    }

    [WpfFact]
    public void Shows_every_tag_in_use_when_there_are_fewer_than_five()
    {
        using var host = new ChipsHost(("home", 1), ("work", 2));

        Assert.Equal(["work", "home"], host.Tags);
    }

    [WpfFact]
    public void Shows_only_the_first_five_tags_in_use_most_used_first_and_ties_alphabetical()
    {
        using var host = new ChipsHost(("alpha", 3), ("beta", 2), ("gamma", 5), ("delta", 1), ("echo", 4), ("zulu", 1));

        Assert.Equal(["gamma", "echo", "alpha", "beta", "delta"], host.Tags);
    }

    [WpfFact]
    public void Clicking_a_chip_raises_TagChosen_with_its_tag()
    {
        using var host = new ChipsHost(("home", 2), ("work", 1));

        OpenWindows.Click(EditorFields.Chip(host.Chips, "work"));

        Assert.Equal(["work"], host.Chosen);
    }

    [WpfFact]
    public void Clicking_a_chip_twice_raises_TagChosen_twice()
    {
        using var host = new ChipsHost(("home", 1));

        OpenWindows.Click(EditorFields.Chip(host.Chips, "home"));
        OpenWindows.Click(EditorFields.Chip(host.Chips, "home"));

        Assert.Equal(["home", "home"], host.Chosen);
    }

    [WpfFact]
    public void Chips_are_buttons_the_keyboard_reaches_with_Tab()
    {
        using var host = new ChipsHost(("home", 1));

        var chip = EditorFields.Chip(host.Chips, "home");

        Assert.True(chip.IsTabStop);
        Assert.True(chip.Focusable);
        Assert.True(chip.IsEnabled);
    }

    [WpfFact]
    public void Refreshes_when_Tasks_changes()
    {
        using var host = new ChipsHost(("home", 1));

        host.Services.UseTag("work", 2);

        Assert.Equal(["work", "home"], host.Tags);
    }

    [WpfFact]
    public async Task Refreshes_when_Tasks_changes_on_a_worker_thread()
    {
        using var host = new ChipsHost(("home", 1));

        await host.Services.UseTagOnWorkerThread("work", 2);
        await RenderedField.Settled();

        Assert.Equal(["work", "home"], host.Tags);
    }

    [WpfFact]
    public async Task Shows_no_chips_after_the_repository_folder_is_cleared_on_a_worker_thread()
    {
        using var host = new ChipsHost(("home", 1));

        await Task.Run(() => host.Services.Tasks.UseRepositoryFolder(null));
        await RenderedField.Settled();

        Assert.Empty(host.Tags);
    }

    [WpfFact]
    public void Shows_no_chips_after_the_repository_folder_is_cleared()
    {
        using var host = new ChipsHost(("home", 1));

        host.Services.Tasks.UseRepositoryFolder(null);

        Assert.Empty(host.Tags);
    }
}

/// <summary>Hosts a <see cref="TagChips"/> in a showing window over a real Tasks with the given tag counts. Shared test setup.</summary>
internal sealed class ChipsHost : IDisposable
{
    private readonly Window window;
    private readonly List<string> chosen = [];

    public ChipsHost(params (string Tag, int Count)[] uses)
    {
        Services = new EditorServices();
        Services.UseRepository();
        foreach (var (tag, count) in uses)
        {
            Services.UseTag(tag, count);
        }

        Chips = new TagChips(Services.Tasks);
        Chips.TagChosen += (_, tag) => chosen.Add(tag);
        window = new Window { Content = Chips, Width = 480, Height = 200 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagChips Chips { get; }

    public IReadOnlyList<string> Tags => EditorFields.ChipTags(Chips);

    public IReadOnlyList<string> Chosen => chosen;

    public void Dispose() => window.Close();
}
