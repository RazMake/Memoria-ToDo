using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Shared.Platform.Tests;

/// <summary>Runs the file system contract on the fake, so the fake can't drift from the real thing.</summary>
[Unit]
public sealed class FakeFileSystemContractTests : FileSystemContract
{
    protected override IFileSystem FileSystem { get; } = new FakeFileSystem();

    protected override string Root { get; } = Path.Combine(Path.GetTempPath(), "fake-file-system", Guid.NewGuid().ToString("N"));
}
