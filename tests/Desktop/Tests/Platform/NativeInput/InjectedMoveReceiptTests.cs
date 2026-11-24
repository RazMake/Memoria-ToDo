using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class InjectedMoveReceiptTests
{
    private const uint MouseMove = 0x0200;
    private const uint KeyDown = 0x0100;
    private const int Marker = 0x1234;
    private static readonly Win32.Point Target = At(100, 50);

    private static Win32.Point At(int x, int y) => new() { X = x, Y = y };

    private static ObservedMessage Message(uint id = MouseMove, nint extraInfo = Marker, Win32.Point? at = null, bool inExpectedWindow = true) =>
        new(id, extraInfo, at ?? Target, inExpectedWindow);

    private static readonly Win32.Point FarStart = At(600, 550);

    private static InjectedMoveReceipt Receipt() => new(Marker, FarStart, Target);

    [Fact]
    public void Rejects_a_zero_marker_that_cannot_identify_an_injected_packet()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InjectedMoveReceipt(0, FarStart, Target));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 1)]
    public void Rejects_a_start_within_one_pixel_of_the_target(int dx, int dy)
    {
        Assert.Throws<ArgumentException>(() => new InjectedMoveReceipt(Marker, At(Target.X + dx, Target.Y + dy), Target));
    }

    [Fact]
    public void Accepts_a_start_two_pixels_from_the_target()
    {
        var receipt = new InjectedMoveReceipt(Marker, At(Target.X + 2, Target.Y), Target);

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Acknowledges_a_move_windows_made_in_its_place_at_the_target_in_the_expected_window()
    {
        var receipt = Receipt();
        receipt.Observe(Message(extraInfo: 0));

        Assert.True(receipt.TakeAcknowledgement());
        Assert.Null(receipt.MisplacedAt);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 1)]
    public void Acknowledges_a_move_windows_made_one_pixel_from_the_target(int dx, int dy)
    {
        var receipt = Receipt();
        receipt.Observe(Message(extraInfo: 0, at: At(Target.X + dx, Target.Y + dy)));

        Assert.True(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Does_not_acknowledge_a_move_at_the_target_tagged_with_another_marker()
    {
        var receipt = Receipt();
        receipt.Observe(Message(extraInfo: Marker - 1));

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Does_not_acknowledge_a_move_windows_made_at_the_target_in_another_window()
    {
        var receipt = Receipt();
        receipt.Observe(Message(extraInfo: 0, inExpectedWindow: false));

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, -2)]
    [InlineData(-5, 3)]
    public void Neither_acknowledges_nor_reports_a_move_windows_made_away_from_the_target(int dx, int dy)
    {
        var receipt = Receipt();
        receipt.Observe(Message(extraInfo: 0, at: At(Target.X + dx, Target.Y + dy)));

        Assert.False(receipt.TakeAcknowledgement());
        Assert.Null(receipt.MisplacedAt);
    }

    [Fact]
    public void Does_not_acknowledge_a_move_windows_made_at_the_target_after_its_packet_landed_off_the_target()
    {
        var receipt = Receipt();
        receipt.Observe(Message(at: At(Target.X + 5, Target.Y)));
        receipt.Observe(Message(extraInfo: 0));

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Acknowledges_the_routed_move_that_its_own_packet_causes_at_the_target()
    {
        var receipt = Receipt();
        receipt.Observe(Message());

        Assert.True(receipt.TakeAcknowledgement());
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 1)]
    public void Acknowledges_a_packet_that_landed_one_pixel_from_the_target(int dx, int dy)
    {
        var receipt = Receipt();
        receipt.Observe(Message(at: At(Target.X + dx, Target.Y + dy)));

        Assert.True(receipt.TakeAcknowledgement());
        Assert.Null(receipt.MisplacedAt);
    }

    [Fact]
    public void Ignores_messages_before_its_packet()
    {
        var receipt = Receipt();
        receipt.Observe(Message(extraInfo: Marker - 1));
        receipt.Observe(Message(id: KeyDown));
        receipt.Observe(Message());

        Assert.True(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Does_not_acknowledge_a_routed_move_when_no_message_was_observed()
    {
        Assert.False(Receipt().TakeAcknowledgement());
    }

    [Theory]
    [InlineData(MouseMove, Marker + 1, true)]
    [InlineData(KeyDown, Marker, true)]
    [InlineData(MouseMove, Marker, false)]
    public void Does_not_acknowledge_a_routed_move_whose_message_is_not_its_packet_in_the_expected_window(
        uint id, int extraInfo, bool inExpectedWindow)
    {
        var receipt = Receipt();
        receipt.Observe(Message(id, extraInfo, inExpectedWindow: inExpectedWindow));

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Does_not_acknowledge_a_routed_move_after_another_message_has_been_fetched()
    {
        var receipt = Receipt();
        receipt.Observe(Message());
        receipt.Observe(Message(extraInfo: Marker - 1));

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Fact]
    public void Acknowledges_its_packet_only_once()
    {
        var receipt = Receipt();
        receipt.Observe(Message());
        receipt.TakeAcknowledgement();

        Assert.False(receipt.TakeAcknowledgement());
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, -2)]
    [InlineData(-5, 3)]
    public void Reports_a_packet_that_landed_off_the_target_and_does_not_acknowledge_it(int dx, int dy)
    {
        var receipt = Receipt();
        var landed = At(Target.X + dx, Target.Y + dy);
        receipt.Observe(Message(at: landed));

        var misplaced = Assert.NotNull(receipt.MisplacedAt);
        Assert.Equal((landed.X, landed.Y), (misplaced.X, misplaced.Y));
        Assert.False(receipt.TakeAcknowledgement());
    }

    [Theory]
    [InlineData(MouseMove, 0, true)]
    [InlineData(MouseMove, Marker + 1, true)]
    [InlineData(MouseMove, Marker, false)]
    [InlineData(KeyDown, Marker, true)]
    public void Ignores_the_position_of_messages_that_are_not_its_packet_in_the_expected_window(uint id, int extraInfo, bool inExpectedWindow)
    {
        var receipt = Receipt();
        receipt.Observe(Message(id, extraInfo, at: At(Target.X + 40, Target.Y), inExpectedWindow: inExpectedWindow));

        Assert.Null(receipt.MisplacedAt);
    }
}
