using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// Modal dialog, with a plain dialog on the real screen. The dialog closes itself from the step after WPF has drawn it,
/// since the call returns only when it closes.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class ModalDialogTests
{
    [WpfFact]
    public void A_dialog_shown_for_the_first_time_is_cloaked_when_it_loads()
    {
        var dialog = NewDialog();
        bool? cloakedWhenLoaded = null;
        dialog.Loaded += (_, _) => cloakedWhenLoaded = Win32.IsCloaked(Handle(dialog));
        CloseWhenDrawn(dialog, () => { });

        ModalDialog.Show(dialog);

        Assert.True(cloakedWhenLoaded);
    }

    [WpfFact]
    public void A_dialog_is_uncloaked_once_WPF_has_drawn_it_while_it_is_still_open()
    {
        var dialog = NewDialog();
        bool? cloakedWhenDrawn = null;
        var openWhenDrawn = false;
        CloseWhenDrawn(dialog, () =>
        {
            cloakedWhenDrawn = Win32.IsCloaked(Handle(dialog));
            openWhenDrawn = dialog.IsVisible;
        });

        ModalDialog.Show(dialog);

        Assert.False(cloakedWhenDrawn);
        Assert.True(openWhenDrawn);
    }

    [WpfFact]
    public void A_dialog_with_no_content_is_uncloaked_once_WPF_has_drawn_it()
    {
        var dialog = new Window { Width = 300, Height = 200 };
        bool? cloakedWhenDrawn = null;
        CloseWhenDrawn(dialog, () => cloakedWhenDrawn = Win32.IsCloaked(Handle(dialog)));

        ModalDialog.Show(dialog);

        Assert.False(cloakedWhenDrawn);
    }

    [WpfFact]
    public void Show_returns_only_after_the_dialog_closes()
    {
        var dialog = NewDialog();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        CloseWhenDrawn(dialog, () => { });

        ModalDialog.Show(dialog);

        Assert.True(closed);
    }

    [WpfFact]
    public void Show_throws_when_called_off_the_dialogs_UI_thread()
    {
        var dialog = NewDialog();
        try
        {
            var call = Task.Run(() => ModalDialog.Show(dialog));

            var failure = Assert.Throws<AggregateException>(() => call.Wait(TestWait.ForSignal));
            Assert.IsType<InvalidOperationException>(failure.InnerException);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static Window NewDialog() => new() { Width = 300, Height = 200, Content = new Border() };

    // Runs what the test reads, then closes the dialog so that the modal call returns.
    private static void CloseWhenDrawn(Window dialog, Action read) =>
        dialog.Loaded += (_, _) => AfterFirstDraw.Post(dialog.Dispatcher, () =>
        {
            try
            {
                read();
            }
            finally
            {
                dialog.Close();
            }
        });

    private static nint Handle(Window window) => new WindowInteropHelper(window).Handle;
}
