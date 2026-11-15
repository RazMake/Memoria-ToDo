using System.IO;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.Updates;

public static class AppUpdates
{
    /// <summary>
    /// Creates Updates. Nothing checks until <see cref="IUpdateLifetime.Start"/>. The web view function makes a new web view
    /// each time What is new opens. Fails when an argument is null or <paramref name="deviceFolder"/> isn't a full path.
    /// </summary>
    public static IAppUpdates Create(IAppInstall install, IFileSystem files, string deviceFolder, TimeProvider time,
        ITheme theme, Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, CloseWindowKeys closeWindowKeys)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(deviceFolder);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(createWebView);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(closeWindowKeys);
        if (!Path.IsPathFullyQualified(deviceFolder))
        {
            throw new ArgumentException($"The device folder isn't a full path: {deviceFolder}", nameof(deviceFolder));
        }

        var ui = new UiThread();
        var model = new UpdateModel(install, new AutoUpdateSetting(files, deviceFolder), ui);
        var runner = new UpdateRunner(install, model, new NightSchedule(time), ui);
        return new UpdatesService(model, runner, new WhatIsNewDialog(theme, createWebView, browser, closeWindowKeys));
    }
}
