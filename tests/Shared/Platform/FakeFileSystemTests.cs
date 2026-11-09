using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>The in-memory fake follows the file system contract, so modules' tests can rely on it.</summary>
[Unit]
public sealed class FakeFileSystemTests : FileSystemContract
{
    protected override IFileSystem Files { get; } = new FakeFileSystem();

    protected override string Root { get; } = Path.GetFullPath("/fake-root");
}
