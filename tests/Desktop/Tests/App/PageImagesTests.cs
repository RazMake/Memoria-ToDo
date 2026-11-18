using System.IO;
using System.Text;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// What the clone's address serves, through <c>PageImages</c>: an image of the clone first, then an image waiting for an
/// open edit, read-only. Everything else is "404 Not Found", and a failed read "500 Internal Server Error".
/// Files are in the shared fake file system and the paths are judged by Tasks' real file rules.
/// </summary>
[Unit]
public class PageImagesTests
{
    private const string TaskId = "0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private static readonly byte[] CloneBytes = Encoding.ASCII.GetBytes("from the clone");
    private static readonly byte[] WaitingBytes = Encoding.ASCII.GetBytes("from the edit");

    private readonly FakeFileSystem _files = new();
    private readonly string _clone = Path.GetFullPath("clone");
    private readonly string _waiting = Path.GetFullPath("edit-images");
    private readonly IRepositoryFileRules _rules;

    public PageImagesTests()
    {
        var store = TestTaskStore.Create(_files, new FakeImageEncoder(), new FakeTimeProvider(), Path.GetFullPath("device"));
        _rules = store.Synchronization.Files;
    }

    private PageImages Images(string? clone = "") => new(_files, _rules, clone == "" ? _clone : clone, _waiting);

    private void InClone(string link, byte[]? bytes = null) => _files.WriteFile(Path.Combine(_clone, link.Replace('/', Path.DirectorySeparatorChar)), bytes ?? CloneBytes);

    private void Waiting(string link, byte[]? bytes = null) => _files.WriteFile(Path.Combine(_waiting, link.Replace('/', Path.DirectorySeparatorChar)), bytes ?? WaitingBytes);

    private static string Link(string file) => $"images/{TaskId}/{file}";

    private static void AssertNotFound(PageImage answer)
    {
        Assert.Equal(404, answer.Status);
        Assert.Null(answer.Content);
    }

    [Fact]
    public void Serves_a_png_of_the_clone_with_its_bytes_and_the_png_content_type()
    {
        InClone(Link("a1-b2.png"));

        var answer = Images().Find("GET", "/" + Link("a1-b2.png"));

        Assert.Equal(200, answer.Status);
        Assert.Equal("image/png", answer.ContentType);
        Assert.Equal(CloneBytes, answer.Content);
    }

    [Fact]
    public void Serves_a_jpg_of_the_clone_with_the_jpeg_content_type()
    {
        InClone(Link("photo.jpg"));

        var answer = Images().Find("GET", "/" + Link("photo.jpg"));

        Assert.Equal(200, answer.Status);
        Assert.Equal("image/jpeg", answer.ContentType);
    }

    [Fact]
    public void Serves_an_image_that_is_only_among_the_waiting_edit_images()
    {
        Waiting(Link("new.png"));

        var answer = Images().Find("GET", "/" + Link("new.png"));

        Assert.Equal(200, answer.Status);
        Assert.Equal("image/png", answer.ContentType);
        Assert.Equal(WaitingBytes, answer.Content);
    }

    [Fact]
    public void Serves_the_clones_image_when_a_waiting_image_has_the_same_path()
    {
        InClone(Link("same.png"));
        Waiting(Link("same.png"));

        Assert.Equal(CloneBytes, Images().Find("GET", "/" + Link("same.png")).Content);
    }

    [Fact]
    public void Serves_the_waiting_images_when_there_is_no_clone_yet()
    {
        Waiting(Link("new.png"));

        Assert.Equal(WaitingBytes, Images(clone: null).Find("GET", "/" + Link("new.png")).Content);
    }

    [Fact]
    public void An_image_in_neither_folder_is_not_found()
    {
        AssertNotFound(Images().Find("GET", "/" + Link("missing.png")));
    }

    [Fact]
    public void A_request_other_than_a_get_is_not_found_even_for_an_image_that_exists()
    {
        InClone(Link("a.png"));

        foreach (var method in new[] { "POST", "PUT", "DELETE", "HEAD" })
        {
            AssertNotFound(Images().Find(method, "/" + Link("a.png")));
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("")]
    [InlineData("/tasks/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b.json")]
    [InlineData("/settings.json")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/")]
    [InlineData("/images/not-a-task-id/a.png")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/folder/a.png")]
    public void A_path_that_is_not_an_image_of_a_task_is_not_found_though_the_clone_holds_a_file_there(string path)
    {
        InClone($"tasks/{TaskId}.json");
        InClone("settings.json");
        InClone("images/not-a-task-id/a.png");
        InClone($"images/{TaskId}/folder/a.png");

        AssertNotFound(Images().Find("GET", path));
    }

    [Theory]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/../0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/a.png")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/../../secret.png")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/./a.png")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/..%2F..%2Fsecret.png")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/%2e%2e/secret.png")]
    [InlineData("/images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/..\\..\\secret.png")]
    [InlineData("/../secret.png")]
    [InlineData("//images/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b/a.png")]
    public void A_path_that_climbs_out_of_the_image_folder_is_not_found_and_reads_nothing_outside_both_folders(string path)
    {
        InClone(Link("a.png"));
        Waiting(Link("a.png"));
        _files.WriteFile(Path.Combine(_clone, "secret.png"), CloneBytes);
        _files.WriteFile(Path.GetFullPath("secret.png"), CloneBytes);
        _files.WriteFile(Path.Combine(_waiting, "secret.png"), WaitingBytes);

        AssertNotFound(Images().Find("GET", path));
    }

    [Theory]
    [InlineData("a.gif")]
    [InlineData("a.PNG")]
    [InlineData("a.jpeg")]
    [InlineData("a.png.exe")]
    [InlineData("a.json")]
    [InlineData("a")]
    [InlineData(".png")]
    [InlineData("a_b.png")]
    [InlineData("a b.png")]
    [InlineData("a.b.png")]
    [InlineData("a%20b.png")]
    [InlineData("a%2Eb.png")]
    [InlineData("%61.png")]
    [InlineData("é.png")]
    public void A_file_name_other_than_ascii_letters_digits_and_hyphens_ending_in_png_or_jpg_is_not_found(string file)
    {
        InClone(Link(file));
        Waiting(Link(file));

        AssertNotFound(Images().Find("GET", "/" + Link(file)));
    }

    [Fact]
    public void A_read_that_fails_for_another_reason_than_a_missing_file_is_a_server_error_with_no_content()
    {
        InClone(Link("a.png"));
        _files.FailReadsUnder(_clone);

        var answer = Images().Find("GET", "/" + Link("a.png"));

        Assert.Equal(500, answer.Status);
        Assert.Null(answer.Content);
    }

    [Fact]
    public void A_waiting_image_that_cannot_be_read_is_a_server_error()
    {
        Waiting(Link("a.png"));
        _files.FailReadsUnder(_waiting);

        Assert.Equal(500, Images().Find("GET", "/" + Link("a.png")).Status);
    }
}
