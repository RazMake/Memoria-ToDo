using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.HoverCard;

using HoverCard = Memoria.ToDo.Desktop.Platform.HoverCard;

[Integration]
[Collection(ScreenTests.Collection)]
public class HoverCardRealPointerIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);
    private static readonly Point OnPart = new(30, 20);
    private static readonly Point OffPart = new(200, 80);

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_real_pointer_move_onto_a_part_opens_its_card_and_asks_for_the_part_where_the_pointer_is(bool pointerStartsOnPart)
    {
        var owner = new Border { Width = 300, Height = 100, Background = System.Windows.Media.Brushes.White };
        var card = new Border { Width = 120, Height = 40, Background = System.Windows.Media.Brushes.LightYellow };
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        card.Loaded += (_, _) => shown.TrySetResult();
        var asked = new List<Point>();
        var window = new Window { Content = owner, Width = 400, Height = 300, WindowStartupLocation = WindowStartupLocation.Manual, Left = 50, Top = 50 };
        window.Show();
        window.UpdateLayout();
        try
        {
            RealPointer.MoveOver(window, owner, pointerStartsOnPart ? OnPart : OffPart);
            _ = new HoverCard(owner, point =>
            {
                asked.Add(point);
                var box = new Rect(10, 10, 60, 20);
                return Task.FromResult<HoverPart?>(box.Contains(point) ? new HoverPart(box, card) : null);
            }, new MousePointer(), Serilog.Core.Logger.None);

            RealPointer.MoveOver(window, owner, OnPart);

            try
            {
                await shown.Task.WaitAsync(Bound);
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException(
                    $"The native move completed but the card did not load: owner loaded={owner.IsLoaded}, window active={window.IsActive}, "
                    + $"directly over {System.Windows.Input.Mouse.DirectlyOver}, parts requested at [{string.Join("; ", asked)}].", error);
            }
            Assert.NotEmpty(asked);
            var first = asked[0];
            Assert.True(Math.Abs(first.X - OnPart.X) <= 2 && Math.Abs(first.Y - OnPart.Y) <= 2, $"The part was asked for at {first}, not at {OnPart}.");
        }
        finally
        {
            window.Close();
        }
    }
}
