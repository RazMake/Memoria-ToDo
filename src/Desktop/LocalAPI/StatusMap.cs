using System.IO;
using Grpc.Core;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The error table of docs/architecture/local-api-contract.md: the gRPC status of each failure, by its type. A new case is a new row,
/// above any row for a type it derives from. A change saved before a notice failed isn't a failure here: the call ends OK
/// (see <see cref="NoticeFailures"/>), and a store's own failure with notice failures arrives as an aggregate, which is
/// <c>INTERNAL</c> like any failure inside the app no row names.
/// </summary>
internal static class StatusMap
{
    // Most specific type first: the first row the failure is an instance of wins.
    private static readonly (Type Failure, StatusCode Code)[] Table =
    [
        (typeof(OperationCanceledException), StatusCode.Cancelled),
        (typeof(TaskNotFoundException), StatusCode.NotFound),
        (typeof(StaleTaskException), StatusCode.Aborted),
        (typeof(ImageTooLargeException), StatusCode.InvalidArgument),
        (typeof(InvalidDataException), StatusCode.InvalidArgument),
        (typeof(ArgumentException), StatusCode.InvalidArgument),
        (typeof(NoRepositoryFolderException), StatusCode.FailedPrecondition),
        (typeof(SkippedFileException), StatusCode.FailedPrecondition),
        (typeof(SyncNotReadyException), StatusCode.FailedPrecondition),
    ];

    /// <summary>The status a failure ends its call with, carrying the failure's own message; <c>INTERNAL</c> for a type no row names.</summary>
    public static Status Of(Exception failure)
    {
        foreach (var (type, code) in Table)
        {
            if (type.IsInstanceOfType(failure))
                return new Status(code, failure.Message);
        }

        return new Status(StatusCode.Internal, failure.Message);
    }
}
