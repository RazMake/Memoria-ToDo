using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.KeepAwake;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The sun that shows whether Prevent screen saver is on, in the start of Task View's status bar: where it sits, what it looks
/// like, what its tooltip says for each Keep awake state, and that it follows the state while the bar shows. Measured in a bare
/// container with no window; the container holds the theme resources the colors come from, each a different color.
/// </summary>
[Unit]
public class ListStatusStartTests
{
    private static readonly KeepAwakeState Off = new(false, false, null);
    private static readonly KeepAwakeState On = new(true, false, null);
    private static readonly KeepAwakeState Paused = new(true, true, null);
    private static readonly KeepAwakeState Failed = new(true, false, "Access is denied.");

    [WpfFact]
    public void The_sun_is_the_icon_font_glyph_U_E706_like_the_Sync_button_uses()
    {
        using var host = new Host(Off);

        var icon = host.Icon;

        Assert.StartsWith("Segoe Fluent Icons", icon.FontFamily.Source);
        Assert.Contains("Segoe MDL2 Assets", icon.FontFamily.Source);
    }

    [WpfFact]
    public void The_sun_sits_just_left_of_the_version_and_right_of_the_Sync_button()
    {
        using var host = new Host(On);

        var (iconLeft, iconRight) = host.Span(host.Icon);
        var (versionLeft, _) = host.Span(host.Version);
        var (_, syncRight) = host.Span(host.SyncButton);

        Assert.True(iconRight <= versionLeft + 0.5, "The sun isn't left of the version.");
        Assert.True(versionLeft - iconRight < 24, "The sun isn't just left of the version.");
        Assert.True(iconLeft >= syncRight - 0.5, "The sun isn't right of the Sync button.");
    }

    [WpfTheory]
    [InlineData(nameof(Off), "Prevent screen saver is off.")]
    [InlineData(nameof(On), "Prevent screen saver is on: the screen stays on.")]
    [InlineData(nameof(Paused), "Prevent screen saver is paused while the PC is locked.")]
    public void The_tooltip_says_which_state_Prevent_screen_saver_is_in(string stateName, string expected)
    {
        using var host = new Host(State(stateName));

        Assert.Equal(expected, KeepAwakeIcon.ToolTipOf(host.Icon));
    }

    [WpfFact]
    public void A_failed_tooltip_says_it_isnt_working_followed_by_the_reason()
    {
        using var host = new Host(Failed);

        var tooltip = KeepAwakeIcon.ToolTipOf(host.Icon);

        Assert.StartsWith("Prevent screen saver isn't working:", tooltip);
        Assert.EndsWith("Access is denied.", tooltip);
    }

    [WpfTheory]
    [InlineData(nameof(Off))]
    [InlineData(nameof(On))]
    [InlineData(nameof(Paused))]
    [InlineData(nameof(Failed))]
    public void Screen_readers_read_the_tooltip_as_the_sun_name(string stateName)
    {
        using var host = new Host(State(stateName));

        Assert.Equal(KeepAwakeIcon.ToolTipOf(host.Icon), KeepAwakeIcon.NameOf(host.Icon));
        Assert.NotEmpty(KeepAwakeIcon.NameOf(host.Icon));
    }

    [WpfFact]
    public void The_sun_is_not_a_button_and_never_a_keyboard_stop()
    {
        using var host = new Host(On);

        var icon = host.Icon;

        Assert.IsNotAssignableFrom<ButtonBase>(icon);
        Assert.DoesNotContain(icon, OpenWindows.Descendants(host.Bar).OfType<ButtonBase>().SelectMany(OpenWindows.Descendants));
        Assert.False(icon.Focusable);
        Assert.False(KeyboardNavigation.GetIsTabStop(icon));
        Assert.Equal([host.SyncButton], OpenWindows.Descendants(host.Bar).OfType<ButtonBase>().Where(button => button.Focusable));
    }

    [WpfTheory]
    [InlineData(nameof(On), "Primary")]
    [InlineData(nameof(Off), "Secondary")]
    [InlineData(nameof(Paused), "Secondary")]
    [InlineData(nameof(Failed), "Critical")]
    public void The_sun_is_in_the_normal_text_color_while_on_muted_while_off_or_paused_and_in_the_error_color_while_failed(string stateName, string color)
    {
        using var host = new Host(State(stateName));

        Assert.Equal(Host.Colors[color], host.IconColor);
    }

    [WpfFact]
    public void The_sun_follows_Keep_awake_while_the_bar_shows_in_its_tooltip_name_and_color()
    {
        using var host = new Host(Off);

        host.KeepAwake.Publish(On);
        Assert.Equal("Prevent screen saver is on: the screen stays on.", KeepAwakeIcon.ToolTipOf(host.Icon));
        Assert.Equal(KeepAwakeIcon.ToolTipOf(host.Icon), KeepAwakeIcon.NameOf(host.Icon));
        Assert.Equal(Host.Colors["Primary"], host.IconColor);

        host.KeepAwake.Publish(Failed);
        Assert.StartsWith("Prevent screen saver isn't working:", KeepAwakeIcon.ToolTipOf(host.Icon));
        Assert.Equal(KeepAwakeIcon.ToolTipOf(host.Icon), KeepAwakeIcon.NameOf(host.Icon));
        Assert.Equal(Host.Colors["Critical"], host.IconColor);

        host.KeepAwake.Publish(Paused);
        Assert.Equal("Prevent screen saver is paused while the PC is locked.", KeepAwakeIcon.ToolTipOf(host.Icon));
        Assert.Equal(Host.Colors["Secondary"], host.IconColor);

        host.KeepAwake.Publish(Off);
        Assert.Equal("Prevent screen saver is off.", KeepAwakeIcon.ToolTipOf(host.Icon));
    }

    [WpfFact]
    public void Stops_following_Keep_awake_once_the_bar_is_unloaded()
    {
        using var host = new Host(Off);
        host.Bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        host.KeepAwake.Publish(On);

        Assert.Equal("Prevent screen saver is off.", KeepAwakeIcon.ToolTipOf(host.Icon));
    }

    [WpfFact]
    public void The_sun_always_shows_even_while_Prevent_screen_saver_is_off()
    {
        using var host = new Host(Off);

        Assert.Equal(Visibility.Visible, host.Icon.Visibility);
    }

    [WpfFact]
    public void Refuses_a_missing_Keep_awake_status_by_name()
    {
        using var sync = new ListSync(new FakeSyncService(), Palette.Colors, new FakeTheme(), Serilog.Core.Logger.None);

        var failure = Assert.Throws<ArgumentNullException>(() => new ListStatusStart(sync, null!));

        Assert.Equal("keepAwake", failure.ParamName);
    }

    private static KeepAwakeState State(string name) => name switch
    {
        nameof(Off) => Off,
        nameof(On) => On,
        nameof(Paused) => Paused,
        nameof(Failed) => Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private sealed class Host : IDisposable
    {
        public static readonly IReadOnlyDictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            ["Primary"] = System.Windows.Media.Colors.Navy,
            ["Secondary"] = System.Windows.Media.Colors.Gray,
            ["Critical"] = System.Windows.Media.Colors.Crimson,
        };

        private readonly ListSync sync = new(new FakeSyncService(), Palette.Colors, new FakeTheme(), Serilog.Core.Logger.None);
        private readonly Grid container = new();

        public Host(KeepAwakeState state)
        {
            KeepAwake = new FakeKeepAwakeStatus { State = state };
            container.Resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(Colors["Primary"]);
            container.Resources["TextFillColorSecondaryBrush"] = new SolidColorBrush(Colors["Secondary"]);
            container.Resources["SystemFillColorCriticalBrush"] = new SolidColorBrush(Colors["Critical"]);
            Bar = new ListStatusStart(sync, KeepAwake);
            container.Children.Add(Bar);
            Bar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        }

        public FakeKeepAwakeStatus KeepAwake { get; }

        public ListStatusStart Bar { get; }

        public TextBlock Icon => KeepAwakeIcon.In(Bar) ?? throw new InvalidOperationException("The bar shows no sun.");

        public Button SyncButton => OpenWindows.Descendants(Bar).OfType<Button>().Single();

        public TextBlock Version => OpenWindows.Descendants(Bar).OfType<TextBlock>().Single(block => OpenWindows.TextOf(block) == StatusBarText.Version);

        public Color IconColor => ((SolidColorBrush)Icon.Foreground).Color;

        public (double Left, double Right) Span(FrameworkElement element)
        {
            container.Measure(new Size(500, 100));
            container.Arrange(new Rect(0, 0, 500, 100));
            var left = element.TranslatePoint(new Point(0, 0), container).X;
            return (left, left + element.ActualWidth);
        }

        public void Dispose() => sync.Dispose();
    }
}
