using System.IO;
using System.Runtime.InteropServices;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class SaveFailuresTests
{
    [Fact]
    public void A_missing_repository_folder_says_to_choose_one_in_Configure()
    {
        Assert.Equal(
            "There is no repository folder yet. Open Configure, choose a repository folder, then save again.",
            SaveFailures.MessageOf(new NoRepositoryFolderException()));
    }

    [Fact]
    public void A_task_that_no_longer_exists_says_to_copy_the_changes()
    {
        Assert.Equal(
            "This task no longer exists. Copy your changes before leaving the editor.",
            SaveFailures.MessageOf(new TaskNotFoundException()));
    }

    [Fact]
    public void A_partly_added_task_says_the_add_was_incomplete_and_the_text_is_kept()
    {
        Assert.Equal(
            "The add was incomplete: the task was only partly added and couldn't be undone. Your text is kept; check the repository folder, then save again.",
            SaveFailures.MessageOf(new AggregateException(new IOException("x"))));
    }

    [Fact]
    public void File_and_input_failures_say_the_task_wasnt_saved_and_why()
    {
        Assert.Equal("The task wasn't saved: disk full", SaveFailures.MessageOf(new IOException("disk full")));
        Assert.Equal("The task wasn't saved: denied", SaveFailures.MessageOf(new UnauthorizedAccessException("denied")));
        Assert.Equal("The task wasn't saved: bad", SaveFailures.MessageOf(new InvalidDataException("bad")));
        Assert.StartsWith("The task wasn't saved: ", SaveFailures.MessageOf(new ArgumentException("odd")));
    }

    [Fact]
    public void An_image_that_cannot_be_shrunk_says_the_task_wasnt_saved_and_why()
    {
        var failure = new ImageTooLargeException();

        Assert.Equal($"The task wasn't saved: {failure.Message}", SaveFailures.MessageOf(failure));
    }

    [Fact]
    public void A_failure_the_editor_does_not_know_has_no_message()
    {
        Assert.Null(SaveFailures.MessageOf(new InvalidOperationException()));
        Assert.Null(SaveFailures.MessageOf(new ExternalException()));
    }
}
