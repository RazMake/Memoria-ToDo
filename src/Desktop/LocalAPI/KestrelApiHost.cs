using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace Memoria.ToDo.Desktop.LocalAPI;

/// <summary>
/// The real <see cref="IApiHost"/>: Kestrel on one named pipe, HTTP/2 only, serving the gRPC services through the
/// /// <see cref="CallInterceptor"/>. The pipe's access list allows only the current user and denies network logons. Only integration tests start it.
/// </summary>
internal sealed class KestrelApiHost : IApiHost
{
    private readonly string pipeName;
    private readonly ITaskStore tasks;
    private readonly IContacts contacts;
    private readonly ISyncService sync;
    private readonly Func<ICallGate> gate;
    private readonly ILogger logger;
    private WebApplication? app;

    public KestrelApiHost(string pipeName, ITaskStore tasks, IContacts contacts, ISyncService sync, Func<ICallGate> gate, ILogger logger)
    {
        this.pipeName = pipeName;
        this.tasks = tasks;
        this.contacts = contacts;
        this.sync = sync;
        this.gate = gate;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();

        // Nothing from the working folder or the environment may add an endpoint: the pipe is the only one.
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenNamedPipe(pipeName, endpoint => endpoint.Protocols = HttpProtocols.Http2);
            kestrel.Limits.MaxRequestBodySize = ApiLimits.MaxReceiveMessageBytes;
        });
        // .NET refuses an access list together with CurrentUserOnly; the list below already admits only this account.
        builder.WebHost.UseNamedPipes(pipes =>
        {
            pipes.CurrentUserOnly = false;
            pipes.PipeSecurity = OwnerOnlyPipeSecurity();
        });
        builder.Services.AddSingleton(tasks);
        builder.Services.AddSingleton(contacts);
        builder.Services.AddSingleton(sync);
        builder.Services.AddSingleton(gate());
        builder.Services.AddSingleton(logger);
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = ApiLimits.MaxReceiveMessageBytes;
            options.MaxSendMessageSize = ApiLimits.MaxSendMessageBytes;
            options.Interceptors.Add<CallInterceptor>();
        });

        var web = builder.Build();
        web.MapGrpcService<TasksService>();
        web.MapGrpcService<ContactsService>();
        web.MapGrpcService<SyncApiService>();
        web.MapGrpcService<SettingsService>();
        try
        {
            await web.StartAsync();
        }
        catch
        {
            await web.DisposeAsync();
            throw;
        }

        app = web;
    }

    /// <summary>
    /// The pipe's access list: the current user may read, write, and open further instances for Kestrel's listeners, and network logons
    /// are denied. Nobody else is allowed, so every other account is refused.
    /// </summary>
    private static PipeSecurity OwnerOnlyPipeSecurity()
    {
        var owner = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("The current Windows user has no SID.");
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken giveUp)
    {
        if (app is not { } web)
            return;

        try
        {
            await web.StopAsync(giveUp);
        }
        finally
        {
            await web.DisposeAsync();
        }
    }
}
