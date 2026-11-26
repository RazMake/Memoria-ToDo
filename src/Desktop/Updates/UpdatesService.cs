using System.Windows;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>Updates as the composition root holds it: the model, the runner, and the dialog behind the two interfaces.</summary>
internal sealed class UpdatesService(UpdateModel model, UpdateRunner runner, WhatIsNewDialog dialog) : IAppUpdates
{
    public UpdateState State => model.State;

    public bool AutoUpdate => model.AutoUpdate;

    public event EventHandler? Changed
    {
        add => model.Changed += value;
        remove => model.Changed -= value;
    }

    public void SetAutoUpdate(bool on) => model.SetAutoUpdate(on);

    public void UpdateNow() => runner.UpdateNow();

    public Task<string?> CheckNowAsync() => runner.CheckNowAsync();

    public void ShowWhatIsNew(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (model.Newer is { } newer)
        {
            dialog.Show(owner, newer, this);
        }
    }

    public void Start(IAppExit exit) => runner.Start(exit);

    public Task StopAsync() => runner.StopAsync();
}
