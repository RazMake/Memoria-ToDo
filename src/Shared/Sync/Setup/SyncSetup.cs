namespace Memoria.ToDo.Shared.Sync;

/// <summary>The setup actions: device name, key, host trust, and choosing the repository clone.</summary>
internal sealed class SyncSetup(
    IGitOperations git,
    IDeviceKeyStore keys,
    IRepositorySelection repositories,
    DeviceFolderBoundary boundary,
    OwnedWorker worker,
    Operations operations,
    GitSession session,
    SetupModel model,
    DeviceMetadata metadata,
    HostTrust trust,
    Published<StatusSnapshot> status) : ISyncSetup
{
    public ISyncSetupState State => model.Snapshot.Current;

    public event EventHandler? Changed
    {
        add => model.Snapshot.Changed += value;
        remove => model.Snapshot.Changed -= value;
    }

    public Task SetDeviceNameAsync(string name, CancellationToken cancellationToken)
    {
        var valid = DeviceMetadata.ValidateName(name);
        return RunSetupAsync(async _ =>
        {
            await worker.Run(() => Backend(() => metadata.SaveName(valid))).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task GenerateKeyAsync(CancellationToken cancellationToken) =>
        RunSetupAsync(async _ =>
        {
            await worker.Run(() =>
            {
                Backend(() => metadata.LoadName());
                if (Backend(() => keys.HasKey))
                    return;
                using var key = Backend(() => keys.Generate());
                model.Observe(key);
            }).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }, cancellationToken);

    public Task UseExistingAsync(string address, string folder, CancellationToken cancellationToken)
    {
        var parsed = Validate(address, folder);
        return RunSetupAsync(async _ =>
        {
            await RejectDeviceFolderAsync(folder).ConfigureAwait(false);
            await worker.Run(() =>
            {
                Backend(() => git.UseExisting(folder, parsed));
                Backend(() => repositories.Select(folder));
            }).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task CloneAsync(string address, string folder, CancellationToken cancellationToken)
    {
        var parsed = Validate(address, folder);
        return RunSetupAsync(async token =>
        {
            await RejectDeviceFolderAsync(folder).ConfigureAwait(false);
            if (!await worker.Run(() => Backend(() => keys.HasKey)).ConfigureAwait(false))
                throw new InvalidOperationException("The device has no key yet.");
            await BackendAsync(() => session.RunAsync(async connection =>
            {
                await git.Clone(connection, parsed, folder, token).ConfigureAwait(false);
                return 0;
            })).ConfigureAwait(false);
            await worker.Run(() => Backend(() => repositories.Select(folder))).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task AcceptHostKeyAsync(string fingerprint, CancellationToken cancellationToken) =>
        TrustChangeAsync(() => trust.Accept(fingerprint), cancellationToken);

    public Task ForgetHostKeyAsync(string host, int port, CancellationToken cancellationToken) =>
        TrustChangeAsync(() => trust.Forget(host, port), cancellationToken);

    private Task TrustChangeAsync(Action change, CancellationToken cancellationToken) =>
        RunSetupAsync(async _ =>
        {
            await worker.Run(change).ConfigureAwait(false);
            await RefreshAsync().ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>
    /// Runs one setup action alone. An operational failure (Git, local storage, anything unexpected) is published as the error
    /// status and thrown as an <see cref="IOException"/>; invalid arguments, preconditions, and a stop pass through untouched.
    /// </summary>
    private Task RunSetupAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken) =>
        operations.RunAsync(async token =>
        {
            try
            {
                await action(token).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex) when (IsOperationalFailure(ex, token))
            {
                var cause = ex is BackendFault fault ? fault.InnerException! : ex;
                var (kind, details) = FailureReport.OfLocal(cause);
                status.Update(s => s.Failed(kind, details, s.SkippedFiles));
                await TryRefreshAsync(token).ConfigureAwait(false);
                if (cause is IOException)
                    throw;
                throw new IOException(details, cause);
            }
        }, cancellationToken);

    // A backend's own argument or state error is an operational failure, unlike this class's input and precondition errors.
    private sealed class BackendFault(Exception cause) : Exception(cause.Message, cause);

    private static void Backend(Action call) =>
        Backend(() =>
        {
            call();
            return 0;
        });

    private static T Backend<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException and not ObjectDisposedException)
        {
            throw new BackendFault(ex);
        }
    }

    private static async Task BackendAsync(Func<Task> call)
    {
        try
        {
            await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException and not ObjectDisposedException)
        {
            throw new BackendFault(ex);
        }
    }

    private Task RefreshAsync() => BackendAsync(model.RefreshAsync);

    // A cancellation is a stop or the caller's only while the operation's token is cancelled; likewise a disposed object only while stopping.
    private bool IsOperationalFailure(Exception ex, CancellationToken token) => ex switch
    {
        BackendFault => true,
        OperationCanceledException => !token.IsCancellationRequested,
        ObjectDisposedException => !operations.IsStopped,
        ArgumentException or InvalidOperationException => false,
        _ => true,
    };

    // A failed refresh never replaces the failure being reported; it is added to its details.
    private async Task TryRefreshAsync(CancellationToken token)
    {
        try
        {
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsOperationalFailure(ex, token))
        {
            var problem = FailureReport.Describe(ex is BackendFault fault ? fault.InnerException! : ex);
            status.Update(s => s.Failure is { } kind
                ? s.Failed(kind, $"{s.Details} The setup could not be refreshed: {problem}", s.SkippedFiles)
                : s);
            model.PublishFailed();
        }
    }

    // The clone folder must not contain the device folder, so the device's files can never become repository content.
    private async Task RejectDeviceFolderAsync(string folder)
    {
        if (await worker.Run(() => boundary.Contains(folder)).ConfigureAwait(false))
            throw new ArgumentException("The repository folder must not contain the device folder.", nameof(folder));
    }

    /// <summary>Parses the address and checks the folder text before any effect; <see cref="RejectDeviceFolderAsync"/> checks its real location.</summary>
    private SshAddress Validate(string address, string folder)
    {
        var parsed = AddressParser.Parse(address);
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
            throw new ArgumentException("The repository folder must be a full path.", nameof(folder));
        return parsed;
    }
}
