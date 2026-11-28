using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Updates.Tests.WhatIsNew;

/// <summary>When What is new opens: only while a newer version is known.</summary>
[Unit]
public class WhatIsNewOpeningTests
{
    [WpfFact]
    public void Does_not_open_when_there_is_no_update()
    {
        var app = new UpdatesHarness();
        app.Start();

        app.Updates.ShowWhatIsNew(new Window());

        Assert.Empty(app.WebViews);
    }
}
