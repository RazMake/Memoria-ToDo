using System.Windows;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// When a hover card is asked for, shown, and closed, as plain inputs in and show/close out, on a clock these tests move by
/// hand. The real owner, popup, mouse, and windows only call these inputs; placement at a screen's edge is WPF's own popup
/// placement and is left to screen checks.
/// </summary>
[Unit]
public class HoverRestTests
{
    private static readonly TimeSpan HoverTime = TimeSpan.FromMilliseconds(400);
    private static readonly Point Here = new(10, 10);
    private static readonly Point ElsewhereOutside = new(500, 500);
    private static readonly Rect Box = new(0, 0, 100, 20);
    private static readonly Point InsideBox = new(50, 10);

    private sealed class Harness : IHoverRestOutput
    {
        private readonly FakeTimeProvider _time = new();
        private int _lastToken;

        public Harness(HoverCardSlot? slot = null, bool windowActive = true, TimeSpan? hoverTime = null)
        {
            HoverTime = hoverTime ?? HoverRestTests.HoverTime;
            Rest = new HoverRest(_time, HoverTime, slot ?? new HoverCardSlot(), this, windowActive, "Test owner", Serilog.Core.Logger.None);
        }

        public TimeSpan HoverTime { get; }

        public HoverRest Rest { get; }

        public List<(Point At, int Token)> Asked { get; } = [];

        public List<int> Shown { get; } = [];

        public int Closed { get; private set; }

        public void FindPart(Point at, int token)
        {
            Asked.Add((at, token));
            _lastToken = token;
        }

        /// <summary>When set, posted actions wait in <see cref="Held"/> until a test runs them; otherwise they run at once.</summary>
        public bool HoldPosts { get; set; }

        public List<Action> Held { get; } = [];

        public void Post(Action action)
        {
            if (HoldPosts)
            {
                Held.Add(action);
            }
            else
            {
                action();
            }
        }

        public void Show(int token) => Shown.Add(token);

        public void Close() => Closed++;

        public void Pass(TimeSpan time) => _time.Advance(time);

        public void PassHoverTime() => _time.Advance(HoverTime);

        /// <summary>Moves to the place, waits the hover time, and answers the request it made with the box.</summary>
        public void RestAndAnswer(Point at, Rect? box)
        {
            Rest.PointerMoved(at);
            PassHoverTime();
            Rest.PartAnswered(_lastToken, box);
        }

        public void Cause(string trigger)
        {
            switch (trigger)
            {
                case "button": Rest.ButtonPressed(onCard: false); break;
                case "wheel": Rest.WheelTurned(onCard: false); break;
                case "key": Rest.KeyPressed(isModifier: false); break;
                case "deactivated": Rest.WindowDeactivated(); break;
                case "moved": Rest.WindowMoved(); break;
                case "hidden": Rest.WindowHidden(); break;
                case "unloaded": Rest.Unloaded(); break;
                case "left": Rest.PointerLeft(); break;
                default: throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null);
            }
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(1500)]
    public void Asks_for_the_part_under_the_pointer_once_it_has_rested_for_the_hover_time(int milliseconds)
    {
        var harness = new Harness(hoverTime: TimeSpan.FromMilliseconds(milliseconds));

        harness.Rest.PointerMoved(Here);
        harness.Pass(harness.HoverTime - TimeSpan.FromMilliseconds(1));
        Assert.Empty(harness.Asked);
        harness.Pass(TimeSpan.FromMilliseconds(1));

        Assert.Equal(Here, Assert.Single(harness.Asked).At);
    }

    [Fact]
    public void Asks_only_once_while_the_pointer_keeps_resting()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);

        harness.Pass(HoverTime * 5);

        Assert.Single(harness.Asked);
    }

    [Fact]
    public void Moving_to_another_place_starts_the_wait_again()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(100));

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(TimeSpan.FromMilliseconds(100));
        Assert.Empty(harness.Asked);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(100));

        Assert.Equal(ElsewhereOutside, Assert.Single(harness.Asked).At);
    }

    [Fact]
    public void A_move_event_at_the_same_place_does_not_start_the_wait_again()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(100));

        harness.Rest.PointerMoved(Here);
        harness.Pass(TimeSpan.FromMilliseconds(100));

        Assert.Single(harness.Asked);
    }

    [Fact]
    public void Shows_nothing_until_the_part_is_answered()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);

        harness.PassHoverTime();

        Assert.Empty(harness.Shown);
    }

    [Fact]
    public void Shows_the_card_when_the_answer_names_a_part()
    {
        var harness = new Harness();

        harness.RestAndAnswer(Here, Box);

        Assert.Equal([harness.Asked.Single().Token], harness.Shown);
    }

    [Fact]
    public void Shows_nothing_when_the_answer_names_no_part()
    {
        var harness = new Harness();

        harness.RestAndAnswer(Here, box: null);

        Assert.Empty(harness.Shown);
    }

    [Fact]
    public void Asks_nothing_more_after_an_answer_with_no_part_until_the_pointer_moves()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, box: null);

        harness.Pass(HoverTime * 5);

        Assert.Single(harness.Asked);
    }

    [Fact]
    public void Drops_an_answer_that_comes_after_the_pointer_moved_to_another_place()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.PassHoverTime();
        var late = harness.Asked.Single().Token;

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Rest.PartAnswered(late, Box);

        Assert.Empty(harness.Shown);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("wheel")]
    [InlineData("key")]
    [InlineData("deactivated")]
    [InlineData("moved")]
    [InlineData("hidden")]
    [InlineData("unloaded")]
    [InlineData("left")]
    public void Drops_an_answer_that_comes_after_a_trigger_that_closes_a_card(string trigger)
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.PassHoverTime();
        var late = harness.Asked.Single().Token;

        harness.Cause(trigger);
        harness.Rest.PartAnswered(late, Box);

        Assert.Empty(harness.Shown);
    }

    [Fact]
    public void Shows_the_answer_of_the_newest_request_and_drops_an_older_one()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.PassHoverTime();
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();
        var (older, newer) = (harness.Asked[0].Token, harness.Asked[1].Token);

        harness.Rest.PartAnswered(older, Box);
        harness.Rest.PartAnswered(newer, new Rect(480, 490, 100, 20));

        Assert.Equal([newer], harness.Shown);
    }

    [Fact]
    public void Drops_an_answer_it_already_took()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        var token = harness.Asked.Single().Token;

        harness.Rest.PartAnswered(token, Box);

        Assert.Single(harness.Shown);
    }

    [Fact]
    public void Keeps_the_card_while_the_pointer_moves_inside_the_part()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.PointerMoved(InsideBox);
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
        Assert.Single(harness.Asked);
    }

    [Fact]
    public void Closes_the_card_once_when_the_pointer_stays_out_of_the_part()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime * 3);

        Assert.Equal(1, harness.Closed);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("wheel")]
    [InlineData("key")]
    [InlineData("deactivated")]
    [InlineData("moved")]
    [InlineData("hidden")]
    [InlineData("unloaded")]
    [InlineData("left")]
    public void Closes_the_card_on_each_trigger(string trigger)
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Cause(trigger);

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Closes_the_card_once_when_closing_triggers_follow_each_other()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.ButtonPressed(onCard: false);
        harness.Rest.WheelTurned(onCard: false);
        harness.Rest.PointerLeft();

        Assert.Equal(1, harness.Closed);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("wheel")]
    [InlineData("key")]
    [InlineData("moved")]
    [InlineData("hidden")]
    [InlineData("unloaded")]
    public void Closes_nothing_on_a_trigger_while_no_card_shows(string trigger)
    {
        var harness = new Harness();

        harness.Cause(trigger);

        Assert.Equal(0, harness.Closed);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("wheel")]
    [InlineData("key")]
    [InlineData("deactivated")]
    [InlineData("moved")]
    [InlineData("hidden")]
    [InlineData("unloaded")]
    [InlineData("left")]
    public void A_trigger_while_the_pointer_is_still_resting_cancels_the_pending_request(string trigger)
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));

        harness.Cause(trigger);
        harness.Pass(HoverTime * 3);

        Assert.Empty(harness.Asked);
    }

    [Fact]
    public void Does_not_open_the_card_again_after_a_trigger_until_the_pointer_moves_and_rests()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.ButtonPressed(onCard: false);

        harness.Pass(HoverTime * 5);
        Assert.Single(harness.Asked);

        harness.Rest.PointerMoved(Here);
        harness.PassHoverTime();
        Assert.Equal(2, harness.Asked.Count);
    }

    [Fact]
    public void A_modifier_key_does_not_close_the_card()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.KeyPressed(isModifier: true);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void A_modifier_key_does_not_cancel_a_pending_request()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));

        harness.Rest.KeyPressed(isModifier: true);
        harness.Pass(TimeSpan.FromMilliseconds(1));

        Assert.Single(harness.Asked);
    }

    [Fact]
    public void A_modifier_key_does_not_drop_a_pending_answer()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.PassHoverTime();

        harness.Rest.KeyPressed(isModifier: true);
        harness.Rest.PartAnswered(harness.Asked.Single().Token, Box);

        Assert.Single(harness.Shown);
    }

    [Fact]
    public void Asks_nothing_while_the_window_is_not_active()
    {
        var harness = new Harness(windowActive: false);
        harness.Rest.PointerMoved(Here);

        harness.Pass(HoverTime * 3);

        Assert.Empty(harness.Asked);
    }

    [Fact]
    public void Asks_nothing_when_the_window_goes_inactive_while_the_pointer_rests()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.Rest.WindowDeactivated();

        harness.Pass(HoverTime * 3);

        Assert.Empty(harness.Asked);
    }

    [Fact]
    public void Asks_for_a_part_once_the_window_is_active_and_the_pointer_moves_and_rests()
    {
        var harness = new Harness(windowActive: false);
        harness.Rest.PointerMoved(Here);
        harness.Rest.WindowActivated();

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        Assert.Equal(ElsewhereOutside, Assert.Single(harness.Asked).At);
    }

    [Fact]
    public void Shows_nothing_for_an_answer_that_comes_while_the_window_is_not_active()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.PassHoverTime();
        var token = harness.Asked.Single().Token;

        harness.Rest.WindowDeactivated();
        harness.Rest.WindowActivated();
        harness.Rest.PartAnswered(token, Box);

        Assert.Empty(harness.Shown);
    }

    [Fact]
    public void Showing_a_card_closes_the_card_another_owner_shows()
    {
        var slot = new HoverCardSlot();
        var first = new Harness(slot);
        var second = new Harness(slot);
        first.RestAndAnswer(Here, Box);

        second.RestAndAnswer(Here, Box);

        Assert.Equal(1, first.Closed);
        Assert.Equal(0, second.Closed);
        Assert.Single(second.Shown);
    }

    [Fact]
    public void Showing_a_card_closes_nothing_when_no_other_owner_shows_one()
    {
        var slot = new HoverCardSlot();
        var first = new Harness(slot);
        var second = new Harness(slot);
        first.RestAndAnswer(Here, Box);
        first.Rest.ButtonPressed(onCard: false);

        second.RestAndAnswer(Here, Box);

        Assert.Equal(1, first.Closed);
    }

    [Fact]
    public void An_owner_with_its_own_slot_leaves_other_owners_cards_alone()
    {
        var first = new Harness();
        var second = new Harness();
        first.RestAndAnswer(Here, Box);

        second.RestAndAnswer(Here, Box);

        Assert.Equal(0, first.Closed);
    }

    [Fact]
    public void A_card_another_owner_replaced_does_not_close_again_on_a_trigger()
    {
        var slot = new HoverCardSlot();
        var first = new Harness(slot);
        var second = new Harness(slot);
        first.RestAndAnswer(Here, Box);
        second.RestAndAnswer(Here, Box);

        first.Rest.ButtonPressed(onCard: false);

        Assert.Equal(1, first.Closed);
    }

    [Fact]
    public void Keeps_the_card_for_a_moment_when_the_pointer_moves_out_of_the_part()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Closes_the_card_when_the_hover_time_passes_with_the_pointer_outside_the_part_and_off_the_card()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);

        harness.PassHoverTime();

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Posts_the_close_back_to_the_owner_when_the_grace_period_ends_instead_of_closing_on_the_timer_thread()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);

        harness.PassHoverTime();

        Assert.Equal(0, harness.Closed);
        harness.Held.Single()();
        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_the_pointer_enters_it_before_the_posted_close_runs()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        harness.Rest.CardEntered();
        harness.Held.Single()();

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_a_new_grace_period_started_before_the_old_posted_close_runs()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();
        harness.Rest.PointerMoved(InsideBox);
        harness.Rest.PointerMoved(ElsewhereOutside);

        harness.Held.Single()();

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Another_move_outside_the_part_does_not_restart_the_grace_period()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(100));

        harness.Rest.PointerMoved(new Point(600, 600));
        harness.Pass(TimeSpan.FromMilliseconds(100));

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Moving_back_inside_the_part_during_the_grace_period_keeps_the_card()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime / 2);

        harness.Rest.PointerMoved(InsideBox);
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Leaving_the_part_again_after_moving_back_inside_gives_a_new_grace_period()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));
        harness.Rest.PointerMoved(InsideBox);

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, harness.Closed);
        harness.Pass(TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Asks_for_the_part_where_the_pointer_rests_outside_the_part_while_the_card_shows()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);

        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        Assert.Equal(ElsewhereOutside, harness.Asked[1].At);
    }

    [Fact]
    public void Replaces_the_card_when_the_part_the_pointer_rests_on_outside_the_part_is_found()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        harness.Rest.PartAnswered(harness.Asked[1].Token, new Rect(480, 490, 100, 20));
        foreach (var held in harness.Held)
        {
            held();
        }

        Assert.Equal(1, harness.Closed);
        Assert.Equal(2, harness.Shown.Count);
    }

    [Fact]
    public void Keeps_the_card_until_the_grace_period_ends_when_no_part_is_found_where_the_pointer_rested()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        harness.Rest.PartAnswered(harness.Asked[1].Token, null);
        Assert.Equal(0, harness.Closed);
        harness.Held.Single()();

        Assert.Equal(1, harness.Closed);
        Assert.Single(harness.Shown);
    }

    [Fact]
    public void Moving_back_inside_the_part_cancels_the_wait_for_a_rest_outside_it()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime / 2);

        harness.Rest.PointerMoved(InsideBox);
        harness.Pass(HoverTime * 3);

        Assert.Single(harness.Asked);
    }

    [Fact]
    public void Drops_the_answer_for_a_rest_outside_the_part_when_the_pointer_moved_back_inside()
    {
        var harness = new Harness { HoldPosts = true };
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        harness.Rest.PointerMoved(InsideBox);
        harness.Rest.PartAnswered(harness.Asked[1].Token, new Rect(480, 490, 100, 20));

        Assert.Equal(0, harness.Closed);
        Assert.Single(harness.Shown);
    }

    [Fact]
    public void Opens_the_next_card_when_the_pointer_rests_on_another_part_after_the_grace_period_closed_the_card()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.PassHoverTime();

        harness.RestAndAnswer(new Point(600, 600), new Rect(580, 590, 100, 20));

        Assert.Equal(2, harness.Shown.Count);
    }

    [Fact]
    public void A_card_closed_by_the_grace_period_frees_the_slot_so_another_owner_does_not_close_it_again()
    {
        var slot = new HoverCardSlot();
        var first = new Harness(slot);
        var second = new Harness(slot);
        first.RestAndAnswer(Here, Box);
        first.Rest.PointerMoved(ElsewhereOutside);
        first.PassHoverTime();

        second.RestAndAnswer(Here, Box);

        Assert.Equal(1, first.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_the_pointer_enters_it_during_the_grace_period()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime / 2);

        harness.Rest.CardEntered();
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Asks_for_no_part_while_the_pointer_is_on_the_card()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Rest.CardEntered();

        harness.Pass(HoverTime * 5);

        Assert.Single(harness.Asked);
    }

    [Fact]
    public void Closes_the_card_when_the_hover_time_passes_after_the_pointer_left_the_card()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Rest.CardEntered();

        harness.Rest.CardLeft();
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, harness.Closed);
        harness.Pass(TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_the_pointer_leaves_the_card_back_into_the_part_within_the_hover_time()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Rest.CardEntered();
        harness.Rest.CardLeft();
        harness.Pass(HoverTime / 2);

        harness.Rest.PointerMoved(InsideBox);
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Closes_the_card_when_the_hover_time_passes_after_the_pointer_left_the_owner()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.OwnerLeft();
        Assert.Equal(0, harness.Closed);
        harness.Pass(HoverTime - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, harness.Closed);
        harness.Pass(TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_the_pointer_leaves_the_owner_onto_the_card_within_the_hover_time()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.OwnerLeft();
        harness.Pass(HoverTime / 2);
        harness.Rest.CardEntered();
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_the_pointer_enters_the_card_before_the_owner_is_left()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.CardEntered();
        harness.Rest.OwnerLeft();
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Keeps_the_card_when_the_pointer_leaves_the_owner_and_returns_to_the_part_within_the_hover_time()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);

        harness.Rest.OwnerLeft();
        harness.Pass(HoverTime / 2);
        harness.Rest.PointerMoved(InsideBox);
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void Leaving_the_owner_does_not_restart_a_grace_period_that_is_already_running()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Pass(HoverTime / 2);

        harness.Rest.OwnerLeft();
        Assert.Equal(0, harness.Closed);
        harness.Pass(HoverTime / 2);

        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Leaving_the_owner_cancels_a_wait_for_another_part_while_the_card_stays_for_its_grace_period()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);

        harness.Rest.OwnerLeft();
        Assert.Equal(0, harness.Closed);
        harness.Pass(HoverTime * 3);

        Assert.Single(harness.Asked);
        Assert.Equal(1, harness.Closed);
    }

    [Fact]
    public void Leaving_the_owner_with_no_card_shown_cancels_the_wait_for_a_part_and_closes_nothing()
    {
        var harness = new Harness();
        harness.Rest.PointerMoved(Here);
        harness.Pass(HoverTime / 2);

        harness.Rest.OwnerLeft();
        harness.Pass(HoverTime * 3);

        Assert.Empty(harness.Asked);
        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void A_button_pressed_on_the_card_keeps_it_open()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Rest.CardEntered();

        harness.Rest.ButtonPressed(onCard: true);
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Fact]
    public void A_wheel_turned_on_the_card_keeps_it_open()
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);
        harness.Rest.CardEntered();

        harness.Rest.WheelTurned(onCard: true);
        harness.Pass(HoverTime * 3);

        Assert.Equal(0, harness.Closed);
    }

    [Theory]
    [InlineData("button")]
    [InlineData("wheel")]
    [InlineData("key")]
    [InlineData("deactivated")]
    [InlineData("moved")]
    [InlineData("hidden")]
    [InlineData("unloaded")]
    [InlineData("left")]
    public void Closes_the_card_at_once_on_each_trigger_during_the_grace_period_and_only_once(string trigger)
    {
        var harness = new Harness();
        harness.RestAndAnswer(Here, Box);
        harness.Rest.PointerMoved(ElsewhereOutside);

        harness.Cause(trigger);
        Assert.Equal(1, harness.Closed);
        harness.Pass(HoverTime * 3);

        Assert.Equal(1, harness.Closed);
    }
}
