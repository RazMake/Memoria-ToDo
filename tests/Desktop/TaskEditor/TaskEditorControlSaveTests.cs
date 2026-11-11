using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The editor's own save, which Add and the Task list's edit in place both use. The editor runs the function its user
/// passes, so these tests pass fake functions and read what the editor shows and returns.
/// </summary>
[UX]
public class TaskEditorControlSaveTests
{
    [WpfFact]
    public void Saving_calls_the_function_with_the_editors_value_and_returns_true()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");
        var expected = host.Editor.Value;
        var received = new List<TaskDraft>();

        var saved = host.Editor.Save(received.Add);

        Assert.True(saved);
        var draft = Assert.Single(received);
        Assert.Equal(expected.TaskId, draft.TaskId);
        Assert.Equal("Buy milk", draft.Title);
        Assert.Equal("Two litres", draft.Description);
        Assert.Equal(["home"], draft.Tags);
    }

    [WpfFact]
    public void Saving_adds_the_tag_text_typed_and_not_yet_added_before_calling_the_function()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "", "home");
        EditorFields.Type(host.Editor, EditorFields.Tags, "errand");
        var received = new List<TaskDraft>();

        host.Editor.Save(received.Add);

        Assert.Equal(["home", "errand"], Assert.Single(received).Tags);
        Assert.Equal(["home", "errand"], host.Editor.Value.Tags);
        Assert.Equal("", EditorFields.TextOf(host.Editor, EditorFields.Tags));
    }

    [WpfFact]
    public void Saving_with_an_empty_Title_still_adds_the_tag_text_typed_so_no_typing_is_lost()
    {
        using var host = new EditorHost();
        host.Fill("", "Keep me");
        EditorFields.Type(host.Editor, EditorFields.Tags, "errand");

        host.Editor.Save(_ => { });

        Assert.Equal(["errand"], host.Editor.Value.Tags);
    }

    [WpfTheory]
    [InlineData("")]
    [InlineData("   ")]
    public void Saving_with_an_empty_Title_returns_false_and_does_not_call_the_function(string title)
    {
        using var host = new EditorHost();
        host.Fill(title, "A description", "home");
        var calls = 0;

        var saved = host.Editor.Save(_ => calls++);

        Assert.False(saved);
        Assert.Equal(0, calls);
        Assert.Equal("A description", host.Editor.Value.Description);
        Assert.Equal(["home"], host.Editor.Value.Tags);
    }

    [WpfFact]
    public void Saving_with_an_empty_Title_marks_the_Title()
    {
        using var host = new EditorHost();
        host.Fill("");
        Assert.False(host.TitleIsMarked);

        host.Editor.Save(_ => { });

        Assert.True(host.TitleIsMarked);
    }

    [WpfFact]
    public void Saving_with_an_empty_Title_puts_the_cursor_in_the_Title()
    {
        using var host = new EditorHost();
        host.Fill("");
        host.Focus(EditorFields.Description);

        host.Editor.Save(_ => { });

        Assert.Equal(EditorFields.Title, host.FocusedField);
    }

    [WpfFact]
    public void Saving_does_not_mark_a_Title_that_has_text()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk");

        host.Editor.Save(_ => { });

        Assert.False(host.TitleIsMarked);
    }

    [WpfFact]
    public void Saving_without_a_repository_folder_returns_false_keeps_the_text_and_says_to_set_it_in_Configure()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");

        var saved = host.Editor.Save(draft => host.Services.Tasks.Add(draft));

        Assert.False(saved);
        Assert.True(host.ShowsText("Configure"));
        Assert.True(host.ShowsText("repository folder"));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_when_the_repository_folder_is_not_set_reports_it_whichever_function_throws()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");

        var saved = host.Editor.Save(_ => throw new NoRepositoryFolderException());

        Assert.False(saved);
        Assert.True(host.ShowsText("Configure"));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_when_the_files_cannot_be_written_returns_false_keeps_the_text_and_says_why()
    {
        using var host = new EditorHost();
        host.Services.UseRepository();
        host.Services.Files.FailWritesUnder(host.Services.TaskFolder);
        host.Fill("Buy milk", "Two litres", "home");

        var saved = host.Editor.Save(draft => host.Services.Tasks.Add(draft));

        Assert.False(saved);
        Assert.True(host.ShowsText("wasn't saved"));
        Assert.True(host.ShowsText("Writing is not allowed here"));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_a_task_that_was_deleted_meanwhile_returns_false_keeps_the_text_and_says_so()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");

        var saved = host.Editor.Save(_ => throw new TaskNotFoundException());

        Assert.False(saved);
        Assert.True(host.ShowsText("This task no longer exists. Copy your changes before leaving the editor."));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_a_task_whose_add_could_not_be_undone_says_it_was_incomplete_and_keeps_the_text()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");

        var saved = host.Editor.Save(_ => throw new AggregateException("partly added", new IOException("undo failed")));

        Assert.False(saved);
        Assert.True(host.ShowsText("incomplete"));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_an_image_that_cannot_be_shrunk_to_1_MB_says_why_and_keeps_the_text()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");

        var saved = host.Editor.Save(_ => throw new ImageTooLargeException());

        Assert.False(saved);
        Assert.True(host.ShowsText("wasn't saved"));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_a_task_whose_image_cannot_be_kept_with_the_task_says_why_and_keeps_the_text()
    {
        using var host = new EditorHost();
        host.Services.UseRepository();
        host.Fill("Buy milk", "", "home");
        host.Services.Clipboard.Image = Memoria.ToDo.Shared.Fakes.FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        host.Focus(EditorFields.Description);
        host.Press(System.Windows.Input.Key.LeftCtrl, System.Windows.Input.Key.V);
        var description = EditorFields.TextOf(host.Editor, EditorFields.Description);
        host.Services.Files.FailWritesUnder(Path.Combine(host.Services.Clone, "images"));

        var saved = host.Editor.Save(draft => host.Services.Tasks.Add(draft));

        Assert.False(saved);
        Assert.True(host.ShowsText("wasn't saved"));
        Assert.Equal(description, host.Editor.Value.Description);
        Assert.Empty(host.Services.Tasks.All);
    }

    [WpfTheory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(ArgumentException))]
    public void Saving_says_the_reason_of_each_other_known_failure_and_keeps_the_text(Type failure)
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");
        var exception = (Exception)Activator.CreateInstance(failure, "the reason given")!;

        var saved = host.Editor.Save(_ => throw exception);

        Assert.False(saved);
        Assert.True(host.ShowsText("wasn't saved: the reason given"));
        AssertTextKept(host);
    }

    [WpfFact]
    public void Saving_does_not_swallow_a_failure_it_does_not_know()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk");

        Assert.Throws<InvalidCastException>(() => host.Editor.Save(_ => throw new InvalidCastException()));
    }

    [WpfFact]
    public void Saving_after_a_failure_can_succeed_and_return_true()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk", "Two litres", "home");
        host.Editor.Save(_ => throw new IOException("disk full"));

        var saved = host.Editor.Save(_ => { });

        Assert.True(saved);
    }

    [WpfFact]
    public void The_message_line_is_part_of_the_editor_below_the_Tags_line()
    {
        using var host = new EditorHost();
        host.Fill("Buy milk");

        host.Editor.Save(_ => throw new IOException("disk full"));

        var message = Assert.Single(Memoria.ToDo.Desktop.Fakes.OpenWindows.Descendants(host.Editor)
            .OfType<System.Windows.Controls.TextBlock>(), block => Memoria.ToDo.Desktop.Fakes.OpenWindows.TextOf(block).Contains("disk full"));
        host.Editor.UpdateLayout();
        var tagsBottom = Top(EditorFields.Field(host.Editor, EditorFields.Tags), host.Editor)
            + EditorFields.Field(host.Editor, EditorFields.Tags).ActualHeight;
        Assert.True(Top(message, host.Editor) >= tagsBottom, "The message line isn't under the Tags line.");
    }

    [WpfFact]
    public void Putting_the_cursor_in_the_Title_moves_the_focus_from_another_field()
    {
        using var host = new EditorHost();
        host.Focus(EditorFields.Tags);

        host.Editor.FocusTitle();

        Assert.Equal(EditorFields.Title, host.FocusedField);
    }

    private static double Top(System.Windows.FrameworkElement element, System.Windows.UIElement ancestor) =>
        element.TransformToAncestor(ancestor).Transform(new System.Windows.Point(0, 0)).Y;

    private static void AssertTextKept(EditorHost host)
    {
        Assert.Equal("Buy milk", host.Editor.Value.Title);
        Assert.Equal("Two litres", host.Editor.Value.Description);
        Assert.Equal(["home"], host.Editor.Value.Tags);
    }
}
