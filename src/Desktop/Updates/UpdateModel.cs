namespace Memoria.ToDo.Desktop.Updates;

/// <summary>
/// What the app knows about updates: the stage, the newer version, Auto update, and why the last update or setting read failed.
/// Every change runs on the UI thread, and raises <see cref="Changed"/> there when it changed <see cref="State"/> or Auto update.
/// </summary>
internal sealed class UpdateModel(IAppInstall install, AutoUpdateSetting setting, UiThread ui)
{
    private UpdateStage _stage = UpdateStage.NoUpdate;
    private NewerVersion? _newer;
    private string? _updateFailure;
    private string? _settingFailure;

    public event EventHandler? Changed;

    public UpdateStage Stage => install.IsInstalled ? _stage : UpdateStage.NotInstalled;

    public bool AutoUpdate { get; private set; }

    /// <summary>The version a check found, while it is available or being installed.</summary>
    public NewerVersion? Newer => Stage is UpdateStage.Available or UpdateStage.Updating ? _newer : null;

    public UpdateState State => new(Stage, install.Version, Newer?.Version, _updateFailure ?? _settingFailure);

    /// <summary>Reads Auto update from the device folder.</summary>
    public void ReadAutoUpdate() => Change(() =>
    {
        (AutoUpdate, _settingFailure) = setting.Read();
    });

    /// <summary>Saves Auto update, then shows it. Fails, changing nothing, when it can't be saved, or when the app isn't installed or is updating.</summary>
    public void SetAutoUpdate(bool on) => Change(() =>
    {
        if (Stage is UpdateStage.NotInstalled or UpdateStage.Updating)
        {
            throw new InvalidOperationException(Stage == UpdateStage.Updating
                ? "Auto update can't change while an update is under way."
                : "Auto update works only in an installed copy.");
        }

        setting.Write(on);
        AutoUpdate = on;
        _settingFailure = null;
    });

    /// <summary>Shows what a check found: a version, or none. Does nothing while an update is under way.</summary>
    public void CheckFound(NewerVersion? found) => Change(() =>
    {
        if (_stage == UpdateStage.Updating)
        {
            return;
        }

        if (found?.Version != _newer?.Version)
        {
            _updateFailure = null;
        }

        _stage = found is null ? UpdateStage.NoUpdate : UpdateStage.Available;
        _newer = found;
    });

    /// <summary>Turns to Updating and returns the version when one is available; otherwise changes nothing and returns null.</summary>
    public NewerVersion? TryBeginUpdate() => Change(() =>
    {
        if (Stage != UpdateStage.Available)
        {
            return null;
        }

        _stage = UpdateStage.Updating;
        _updateFailure = null;
        return _newer;
    });

    /// <summary>Returns from Updating to Available, with the reason when the update failed.</summary>
    public void EndUpdate(string? failure) => Change(() =>
    {
        _stage = UpdateStage.Available;
        _updateFailure = failure;
    });

    private void Change(Action mutate) => Change<object?>(() =>
    {
        mutate();
        return null;
    });

    private T Change<T>(Func<T> mutate) => ui.Run(() =>
    {
        var before = (State, AutoUpdate);
        var result = mutate();
        if (before != (State, AutoUpdate))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return result;
    });
}
