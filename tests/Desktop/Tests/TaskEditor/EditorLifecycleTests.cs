using System.Runtime.CompilerServices;
using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The top tags and the editor follow Tasks and the theme only while they are on screen. The Task list takes an editor
/// out of its window and puts it back, so a control that is not shown must not keep itself alive through Tasks or the
/// theme, and one that is shown again must catch up with everything it missed.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class EditorLifecycleTests
{
    private const string LightRed = "#d1242f";
    private const string DarkRed = "#ff7b72";

    [WpfFact]
    public void Top_tags_taken_out_of_their_window_stop_following_Tasks()
    {
        using var host = new TopTagsWindow(("home", 1));
        host.Remove();

        host.Services.UseTag("work", 2);

        Assert.Equal(["home"], TagControls.BadgeTags(host.Top));
    }

    [WpfFact]
    public void Top_tags_put_back_in_a_window_show_the_tags_as_they_are_now()
    {
        using var host = new TopTagsWindow(("home", 1));
        host.Remove();
        host.Services.UseTag("work", 2);

        host.Put();

        Assert.Equal(["work", "home"], TagControls.BadgeTags(host.Top));
    }

    [WpfFact]
    public void Top_tags_put_back_in_a_window_follow_Tasks_again()
    {
        using var host = new TopTagsWindow(("home", 1));
        host.Remove();
        host.Put();

        host.Services.UseTag("work", 2);

        Assert.Equal(["work", "home"], TagControls.BadgeTags(host.Top));
    }

    [WpfFact]
    public async Task A_change_that_reaches_the_top_tags_from_a_worker_thread_after_they_left_their_window_is_ignored()
    {
        using var host = new TopTagsWindow(("home", 1));
        host.Remove();
        using var inChanged = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        host.Services.Tasks.Changed += (_, _) =>
        {
            inChanged.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        };
        host.Put();
        var change = host.Services.UseTagOnWorkerThread("work", 2);
        Assert.True(inChanged.Wait(TimeSpan.FromSeconds(10)), "Tasks never raised Changed.");
        host.Remove();

        release.Set();
        await change;
        await RenderedField.Settled();

        Assert.Equal(["home"], TagControls.BadgeTags(host.Top));
    }

    [WpfFact]
    public void An_editor_put_back_in_a_window_suggests_the_tags_in_use_now()
    {
        using var host = new EditorWindow();
        host.Services.UseTag("home");
        host.OpenCustom();
        EditorFields.Type(host.Editor, EditorFields.Tags, "ho");
        host.Remove();
        host.Services.UseTag("hobby", 2);
        host.Put();

        Assert.Equal(["hobby", "home"], EditorFields.ShownSuggestions(host.Editor));
    }

    [WpfFact]
    public async Task An_editor_put_back_in_a_window_shows_colors_for_the_theme_as_it_is_now()
    {
        using var host = new EditorWindow();
        EditorFields.Field(host.Editor, EditorFields.Title).Focus();
        host.Editor.Value = host.Editor.Value with { Description = ":red[mmmmmmmmmmmm]" };
        host.Remove();
        host.Services.Theme.Switch(true);

        host.Put();
        await RenderedField.Settled();
        var look = RenderedField.Of(host.Window, EditorFields.Description);

        Assert.True(look.ShowsColor(DarkRed));
        Assert.False(look.ShowsColor(LightRed));
    }

    [WpfFact]
    public void An_editor_put_back_in_a_window_keeps_the_text_it_held()
    {
        using var host = new EditorWindow();
        host.Editor.Value = host.Editor.Value with { Title = "Buy milk", Tags = ["home"] };
        host.OpenCustom();
        EditorFields.Type(host.Editor, EditorFields.Tags, "err");
        host.Remove();

        host.Put();

        Assert.Equal("Buy milk", host.Editor.Value.Title);
        Assert.Equal(["home"], host.Editor.Value.Tags);
        Assert.Equal("err", EditorFields.TextOf(host.Editor, EditorFields.Tags));
        Assert.True(host.Editor.HasChanges);
    }

    [WpfFact]
    public void A_tag_editor_put_back_in_a_window_shows_badge_colors_for_the_tag_colors_and_theme_as_they_are_now()
    {
        using var host = new TagEditorWindow("home");
        host.Remove();
        host.Services.TagColors.Seed("home", "blue-1");
        host.Services.Theme.Switch(true);

        host.Put();

        Assert.Equal(TagControls.Expected(TagColors.Find("blue-1")!, dark: true), TagControls.FillOf(host.Editor, "home"));
    }

    [WpfFact]
    public void A_tag_editor_taken_out_of_its_window_stops_following_the_theme_and_tag_colors()
    {
        using var host = new TagEditorWindow("home");
        var before = TagControls.FillOf(host.Editor, "home");
        host.Remove();

        host.Services.TagColors.Seed("home", "blue-1");
        host.Services.TagColors.RaiseChanged();
        host.Services.Theme.Switch(true);

        Assert.Equal(before, TagControls.FillOf(host.Editor, "home"));
    }

    [WpfFact]
    public void Tag_badges_put_back_in_a_window_show_the_theme_as_it_is_now()
    {
        var services = new EditorServices();
        var badges = services.CreateTagBadges();
        badges.Tags = ["home"];
        var window = new Window { Content = badges, Width = 480, Height = 200 };
        window.Show();
        try
        {
            LoadSync.Remove(window, badges);
            services.Theme.Switch(true);

            LoadSync.Put(window, badges);

            Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: true), TagControls.FillOf(badges, "home"));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public async Task Top_tags_taken_out_of_their_window_can_be_collected_while_Tasks_lives_on()
    {
        var services = new EditorServices();
        services.UseRepository();
        var reference = ShowAndRemoveTopTags(services);

        await RenderedField.Settled();

        Assert.True(IsCollected(reference));
        GC.KeepAlive(services);
    }

    [WpfFact]
    public async Task An_editor_taken_out_of_its_window_can_be_collected_while_Tasks_and_the_theme_live_on()
    {
        var services = new EditorServices();
        services.UseRepository();
        var reference = ShowAndRemoveEditor(services);

        await RenderedField.Settled();

        Assert.True(IsCollected(reference));
        GC.KeepAlive(services);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ShowAndRemoveTopTags(EditorServices services)
    {
        var chips = new TopTags(services.Tasks, services.TagColors, services.Theme);
        var window = new Window { Content = chips, Width = 480, Height = 200 };
        window.Show();
        LoadSync.Remove(window, chips);
        window.Close();
        return new WeakReference(chips);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ShowAndRemoveEditor(EditorServices services)
    {
        var editor = services.CreateEditor();
        var window = new Window { Content = editor, Width = 480, Height = 300 };
        window.Show();
        LoadSync.Remove(window, editor);
        window.Close();
        return new WeakReference(editor);
    }

    private static bool IsCollected(WeakReference reference)
    {
        for (var attempt = 0; attempt < 5 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !reference.IsAlive;
    }
}

/// <summary>Shows a <see cref="TopTags"/> in a window, which a test can take it out of and put it back in. Shared test setup.</summary>
internal sealed class TopTagsWindow : IDisposable
{
    private readonly Window window;

    public TopTagsWindow(params (string Tag, int Count)[] uses)
    {
        Services = new EditorServices();
        Services.UseRepository();
        foreach (var (tag, count) in uses)
        {
            Services.UseTag(tag, count);
        }

        Top = new TopTags(Services.Tasks, Services.TagColors, Services.Theme);
        window = new Window { Content = Top, Width = 480, Height = 200 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TopTags Top { get; }

    public void Remove() => LoadSync.Remove(window, Top);

    public void Put() => LoadSync.Put(window, Top);

    public void Dispose() => window.Close();
}

/// <summary>Shows a <see cref="TaskEditorControl"/> in a window, which a test can take it out of and put it back in. Shared test setup.</summary>
internal sealed class EditorWindow : IDisposable
{
    public EditorWindow()
    {
        Services = new EditorServices();
        Services.UseRepository();
        Editor = Services.CreateEditor();
        Window = new Window { Content = Editor, Width = 480, Height = 300 };
        Window.Show();
    }

    public EditorServices Services { get; }

    public TaskEditorControl Editor { get; }

    public Window Window { get; }

    public void Remove() => LoadSync.Remove(Window, Editor);

    public void Put() => LoadSync.Put(Window, Editor);

    /// <summary>Clicks + and then Custom, which opens the tag box.</summary>
    public void OpenCustom()
    {
        OpenWindows.Click(TagControls.Plus(Editor));
        OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    public void Dispose() => Window.Close();
}
