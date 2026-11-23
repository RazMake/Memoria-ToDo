using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>
/// How the Add window looks and how its editor control behaves when hosted: styled Markdown and the theme, the update bar,
/// the logged failures, and the control's own requests, height and put-back. A test sees styling only as pixels
/// (<see cref="RenderedField"/>); which spans a text has is Rendering's, tested there. What the keys and the control's
/// state do is also in the unit tests of the editor save and the add draft.
/// </summary>
[UX("AddWindow")]
[Collection(ScreenTests.Collection)]
public class AddWindowAppearanceTests
{
    private const string LightRed = "#d1242f";
    private const string DarkRed = "#ff7b72";
    private static readonly string LongText = string.Join("\n\n", Enumerable.Range(1, 200).Select(i => $"line {i}"));

    [WpfFact]
    public async Task Path_10_styled_text_the_update_bar_the_logged_failures_and_the_hosted_editor_control()
    {
        // Styled text paints in the theme's colors and follows the theme.
        using (var screen = new AddScreen())
        {
            var red = await Look(screen, EditorFields.Description, ":red[mmmmmmmmmmmm]");
            Assert.True(red.ShowsColor(LightRed));
            Assert.False(red.ShowsColor(DarkRed));

            screen.Services.Theme.Switch(true);
            await RenderedField.Settled();
            var dark = RenderedField.Of(screen.Window, EditorFields.Description);
            Assert.True(dark.ShowsColor(DarkRed));
            Assert.False(dark.ShowsColor(LightRed));

            var styled = await Look(screen, EditorFields.Description, "**mmmmmmmmmmmmmmmmmmmm**");
            var plain = await Look(screen, EditorFields.Description, "** mmmmmmmmmmmmmmmmmmmm**");
            Assert.True(styled.TotalInk() > plain.TotalInk());

            var one = await Look(screen, EditorFields.Description, "# mmmmmmmmmm");
            var two = await Look(screen, EditorFields.Description, "## mmmmmmmmmm");
            var four = await Look(screen, EditorFields.Description, "#### mmmmmmmmmm");
            var none = await Look(screen, EditorFields.Description, "####mmmmmmmmmm");
            Assert.True(one.InkHeight() > two.InkHeight());
            Assert.True(two.InkHeight() > four.InkHeight());
            Assert.True(four.InkHeight() > none.InkHeight());

            var all = await Look(screen, EditorFields.Description, "# mmmmmmmm\n[mmmmmmmm](https://example.com)\n`mmmmmmmm`\n_mmmmmmmm_");
            Assert.True(all.ShowsColor(TextLooks.Dark.Heading1));
            Assert.True(all.ShowsColor(TextLooks.Dark.Link));
            Assert.True(all.ShowsColor(TextLooks.Dark.Code));
            Assert.True(all.ShowsColor(TextLooks.Dark.Italic));
            Assert.False(all.ShowsColor(TextLooks.Light.Heading1));

            var hash = await Look(screen, EditorFields.Title, "# mmmmmmmmmm");
            var plainTitle = await Look(screen, EditorFields.Title, "#mmmmmmmmmm");
            Assert.Equal(plainTitle.InkHeight(), hash.InkHeight());

            screen.Show();
            screen.Focus(EditorFields.Title);
            EditorFields.Type(screen.Window, EditorFields.Description, ":red[mmmmmmmmmmmm]");
            Assert.Equal(":red[mmmmmmmmmmmm]", EditorFields.TextOf(screen.Window, EditorFields.Description));
        }

        // The update bar shows only while a version is available or updating, below Save, and grows the window.
        using (var screen = new AddScreen())
        {
            screen.Services.Updates.State = new UpdateState(UpdateStage.NoUpdate, "2.1.0", null, null);
            screen.Show();
            screen.UseTheme(dark: false);
            Assert.False(screen.ShowsUpdateBar);
            Assert.False(screen.ShowsText("Update"));
            var plain = screen.WindowHeight;

            screen.PublishUpdate(UpdateStage.Available, "2.2.0");
            Assert.True(screen.ShowsUpdateBar);
            Assert.Equal("Update available: 2.2.0", string.Concat(screen.UpdateBarTexts));
            var barHeight = screen.UpdateBar!.ActualHeight;
            Assert.True(barHeight > 0, "The bar has no height.");
            Assert.Equal(plain + barHeight, screen.WindowHeight, 1.0);
            var (bar, save, contentHeight) = screen.BarPlace;
            Assert.True(bar.Top >= save.Bottom - 0.5, $"The bar starts at {bar.Top}, above Save's bottom {save.Bottom}.");
            Assert.InRange(bar.Bottom, contentHeight - 1, contentHeight + 1);

            OpenWindows.Click(screen.UpdateTextButton);
            Assert.Same(screen.Window, Assert.Single(screen.Services.Updates.WhatIsNewOwners));
            Assert.Equal(0, screen.Services.Updates.UpdateNowCount);

            screen.PublishUpdate(UpdateStage.Updating, "2.2.0");
            Assert.Equal("Updating to 2.2.0", string.Concat(screen.UpdateBarTexts));

            screen.PublishUpdate(UpdateStage.NoUpdate, null);
            Assert.False(screen.ShowsUpdateBar);
            Assert.Equal(plain, screen.WindowHeight, 1.0);
        }

        // Failures the window shows are also logged with the exception, and a working window logs no problem.
        using (var screen = new AddScreen())
        {
            screen.Services.KeepUnreadableDraft();
            screen.Show();
            Assert.Contains(screen.Services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.Exception is not null);
        }

        using (var screen = new AddScreen())
        {
            screen.Show();
            screen.Fill("Buy milk");
            screen.Services.Files.FailWritesUnder(screen.Services.TaskFolder);
            screen.Save();
            Assert.Contains(screen.Services.Log.Events, entry => entry.Level == LogEventLevel.Error && entry.Exception is not null);
        }

        using (var screen = new AddScreen())
        {
            screen.Show();
            screen.Fill("Buy milk");
            screen.Save();
            screen.Services.Log.Events.AssertNoProblems();
        }

        // The editor control, hosted in a window of its own: its requests, keys, height and focus.
        using (var host = new EditorHost())
        {
            var draft = host.Services.Tasks.StartDraft();
            host.Editor.Value = draft with { Title = "Title", Description = "Text", Tags = ["a"] };
            Assert.Equal("Title", host.Editor.Value.Title);
            Assert.Equal(["a"], host.Editor.Value.Tags);
            Assert.Equal(draft.TaskId, host.Editor.Value.TaskId);
            Assert.False(host.Editor.HasChanges);
            host.Type(EditorFields.Title, "Buy milk");
            Assert.True(host.Editor.HasChanges);

            host.Press(Key.LeftCtrl, Key.Enter);
            Assert.Equal((1, 0, 0), host.Counts);
            host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);
            Assert.Equal((1, 1, 0), host.Counts);
            host.Press(Key.Escape);
            Assert.Equal((1, 1, 1), host.Counts);
            Assert.Empty(host.Services.Tasks.All);

            host.Services.SaveKeys.Set(ModifierKeys.Control, Key.S);
            host.Press(Key.LeftCtrl, Key.Enter);
            Assert.Equal((1, 1, 1), host.Counts);
            host.Press(Key.LeftCtrl, Key.S);
            Assert.Equal((2, 1, 1), host.Counts);
            host.Services.SaveKeys.Set(ModifierKeys.Control | ModifierKeys.Shift, Key.Enter);
            host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);
            Assert.Equal((3, 1, 1), host.Counts);

            host.Editor.Value = draft with { Title = "Buy milk", Description = "first line\nsecond line" };
            host.Editor.FocusDescription();
            Assert.Equal(EditorFields.Description, host.FocusedField);
            var length = EditorFields.TextOf(host.Editor, EditorFields.Description).Length;
            Assert.Equal((length, 0), EditorFields.SelectionOf(host.Editor, EditorFields.Description));
        }

        using (var host = new EditorHost(height: double.NaN))
        {
            Assert.Equal(140, DescriptionHeight(host), 0.5);
            host.Editor.Value = host.Editor.Value with { Description = LongText };
            Assert.Equal(400, DescriptionHeight(host), 0.5);
        }

        using (var host = new EditorHost())
        {
            host.Services.UseRepository();
            host.Services.UseTag("home");
            host.OpenCustom();
            host.Focus(EditorFields.Tags);
            host.Type(EditorFields.Tags, "ho");
            OpenWindows.Click(TagControls.PillButton(host.Editor));
            Assert.True(TagControls.MenuIsOpen(host.Editor));

            host.Press(Key.Escape);
            Assert.False(TagControls.MenuIsOpen(host.Editor));
            Assert.True(TagControls.CustomIsOpen(host.Editor));
            Assert.Equal((0, 0, 0), host.Counts);
            host.Press(Key.Escape);
            Assert.False(TagControls.CustomIsOpen(host.Editor));
            Assert.Equal((0, 0, 0), host.Counts);
            host.Press(Key.Escape);
            Assert.Equal((0, 0, 1), host.Counts);
        }

        // The editor taken out of its window and put back catches up with the tags in use, and can be collected.
        using (var host = new EditorWindow())
        {
            host.Editor.Value = host.Editor.Value with { Title = "Buy milk", Tags = ["home"] };
            host.Services.UseTag("home");
            host.Remove();
            host.Services.UseTag("hobby", 2);
            host.Put();

            Assert.Equal("Buy milk", host.Editor.Value.Title);
            Assert.Equal(["home"], host.Editor.Value.Tags);
            Assert.True(host.Editor.HasChanges);
            OpenWindows.Click(TagControls.PillButton(host.Editor));
            Assert.Equal(["hobby", "home"], TagControls.MenuTags(host.Editor));
        }

        var services = new EditorServices();
        services.UseRepository();
        var reference = ShowAndRemoveEditor(services);
        await RenderedField.Settled();
        Assert.True(IsCollected(reference));
        GC.KeepAlive(services);
    }

    private static double DescriptionHeight(EditorHost host) =>
        host.PlaceOf(EditorFields.Field(host.Editor, EditorFields.Description)).Place.Height;

    /// <summary>Shows Add, puts the text in the field, lets styling follow, and takes a picture of the field with no caret in it.</summary>
    private static async Task<RenderedField> Look(AddScreen screen, string field, string text)
    {
        screen.Show();
        screen.Focus(field == EditorFields.Title ? EditorFields.Description : EditorFields.Title);
        screen.Editor.Value = screen.Editor.Value with
        {
            Title = field == EditorFields.Title ? text : "",
            Description = field == EditorFields.Description ? text : "",
        };
        await RenderedField.Settled();
        return RenderedField.Of(screen.Window, field);
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
