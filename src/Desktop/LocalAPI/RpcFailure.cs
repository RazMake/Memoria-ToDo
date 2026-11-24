using Grpc.Core;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// A failure a service raises itself, with the status the contract gives it. A service uses it at the one call that throws, where the
/// <see cref="StatusMap"/> by type would give another status. <see cref="CallInterceptor"/> logs it like any other failure.
/// </summary>
internal static class RpcFailure
{
    public static RpcException Of(StatusCode code, string message) => new(new Status(code, message));
}
