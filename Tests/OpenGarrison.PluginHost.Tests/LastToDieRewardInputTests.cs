using OpenGarrison.Client;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieRewardInputTests
{
    [Fact]
    public void ActualMenuClicksSelectButOnlySeparateConfirmSubmits()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var input = new LastToDieRewardInput();
        input.Reset(Environment.TickCount64 - 1000);
        Assert.False(MenuFrame(game, input, new(), new())); // Arm on release.
        for (var i = 0; i < 10; i++)
        {
            Assert.False(MenuFrame(game, input, new(), Mouse(100, 250, true)));
            Assert.False(MenuFrame(game, input, new(), Mouse(100, 250, false)));
        }
        Assert.Equal(0, input.SelectedIndex);
        Assert.False(input.Submitted);
        Assert.True(MenuFrame(game, input, new(), Mouse(850, 100, true)));
        Assert.False(MenuFrame(game, input, new(), Mouse(850, 100, true)));
    }

    [Fact]
    public void NumberAndEnterCannotSelectAndConfirmOnTheSameFrame()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var input = new LastToDieRewardInput();
        input.Reset(Environment.TickCount64 - 1000);
        MenuFrame(game, input, new(), new());
        Assert.False(MenuFrame(game, input, new(Keys.D1, Keys.Enter), new()));
        Assert.Equal(-1, input.SelectedIndex);
        MenuFrame(game, input, new(), new());
        Assert.False(MenuFrame(game, input, new(Keys.D1), new()));
        Assert.Equal(0, input.SelectedIndex);
        MenuFrame(game, input, new(), new());
        Assert.True(MenuFrame(game, input, new(Keys.Enter), new()));
    }

    [Theory]
    [InlineData(Keys.D1)]
    [InlineData(Keys.NumPad2)]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Space)]
    public void HeldSelectionKeysMustBeReleasedBeforeMenuArms(Keys held)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var input = new LastToDieRewardInput();
        input.Reset(Environment.TickCount64 - 1000);
        Assert.False(MenuFrame(game, input, new(held), new()));
        Assert.False(input.Ready);
        Assert.False(MenuFrame(game, input, new(), new()));
        Assert.True(input.Ready);
    }

    private static MouseState Mouse(int x, int y, bool pressed)
        => new(x, y, 0, pressed ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static bool MenuFrame(Game1 game, LastToDieRewardInput input, KeyboardState keyboard, MouseState mouse)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(Game1).GetNestedType("LastToDieChoiceMenuLayout", BindingFlags.NonPublic)!;
        var layout = Activator.CreateInstance(type, new object[]
        {
            new Rectangle(0, 0, 980, 440),
            new[] { new Rectangle(32, 136, 290, 224), new Rectangle(340, 136, 290, 224) }
        })!;
        var result = (bool)typeof(Game1).GetMethod("UpdateLastToDieRewardInput", flags)!
            .Invoke(game, [input, layout, keyboard, mouse, (Func<int, bool>)(_ => true), null, null])!;
        typeof(Game1).GetField("_previousMouse", flags)!.SetValue(game, mouse);
        typeof(Game1).GetField("_previousKeyboard", flags)!.SetValue(game, keyboard);
        return result;
    }

    [Fact]
    public void EntryClicksAndHeldControlsCannotArmTheDraft()
    {
        var input = new LastToDieRewardInput();
        input.Reset(1000);
        Assert.False(input.Update(1000, false));
        Assert.False(input.Update(1749, true));
        Assert.False(input.Update(4000, false));
        input.Select(0);
        Assert.Equal(-1, input.SelectedIndex);
        Assert.False(input.TrySubmit());
        Assert.False(input.Update(4001, true));
        Assert.True(input.Update(4002, false));
    }

    [Fact]
    public void RepeatedCardSelectionsNeverSubmitAndConfirmationIsSingleShot()
    {
        var input = new LastToDieRewardInput();
        input.Reset(0);
        input.Update(750, true);
        for (var i = 0; i < 50; i++) input.Select(i % 3);
        Assert.False(input.Submitted);
        Assert.Equal(1, input.SelectedIndex);
        Assert.True(input.TrySubmit());
        Assert.False(input.TrySubmit());
        Assert.False(input.Update(3000, true));
        input.Select(2);
        Assert.Equal(1, input.SelectedIndex);
    }

    [Fact]
    public void NewOfferClearsSelectionAndRestartsDelay()
    {
        var input = new LastToDieRewardInput();
        input.Reset(0);
        input.Update(750, true);
        input.Select(2);
        input.TrySubmit();
        input.Reset(5000);
        Assert.Equal(-1, input.SelectedIndex);
        Assert.False(input.Submitted);
        Assert.False(input.Update(5749, true));
        Assert.False(input.TrySubmit());
    }

    [Fact]
    public void ReconnectedHostedRoomClearsSubmittedDraftEvenWhenOfferIsUnchanged()
    {
        var input = new LastToDieRewardInput();
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.True(input.ReconcileHostedContext(runId, offerId: 9, connectionGeneration: 1, now: 0));
        input.Update(750, controlsReleased: true);
        input.Select(1);
        Assert.True(input.TrySubmit());

        Assert.False(input.ReconcileHostedContext(runId, offerId: 9, connectionGeneration: 1, now: 1_000));
        Assert.True(input.Submitted);
        Assert.True(input.ReconcileHostedContext(runId, offerId: 9, connectionGeneration: 2, now: 1_000));
        Assert.False(input.Submitted);
        Assert.Equal(-1, input.SelectedIndex);
        Assert.False(input.Update(1_749, controlsReleased: true));
        Assert.False(input.Update(1_750, controlsReleased: true));
        Assert.True(input.Ready);
    }

    [Fact]
    public void RejectionRequiresReleaseAndDoesNotResubmitAutomatically()
    {
        var input = new LastToDieRewardInput();
        input.Reset(0);
        input.Update(750, true);
        input.Select(1);
        input.TrySubmit();
        input.Reject();
        Assert.False(input.TrySubmit());
        Assert.False(input.Update(2000, false));
        Assert.False(input.Update(2001, true));
        Assert.False(input.Submitted);
        Assert.True(input.TrySubmit());
    }

    [Fact]
    public void CoopPlayersDoNotSharePendingSelections()
    {
        var owner = new LastToDieRewardInput();
        var guest = new LastToDieRewardInput();
        owner.Reset(0);
        guest.Reset(0);
        owner.Update(750, true);
        guest.Update(750, true);
        owner.Select(0);
        Assert.True(owner.TrySubmit());
        Assert.Equal(-1, guest.SelectedIndex);
        Assert.False(guest.TrySubmit());
        guest.Select(2);
        Assert.True(guest.TrySubmit());
    }
}
