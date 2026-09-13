using System.Reflection;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CivilianUmbrellaOpeningRegressionTests
{
    [Fact]
    public void UmbrellaPresentationUsesItsGrantedAbilityWithAnEmptySecondaryWeaponSlot()
    {
        var player = CreateWorld(30).LocalPlayer;
        Assert.True(string.IsNullOrEmpty(player.GameplayLoadoutState.SecondaryItemId));
        var controller = typeof(Game1).GetNestedType("GameplayWeaponRenderController", BindingFlags.NonPublic)!;
        var resolve = controller.GetMethod("ResolveRenderPresentation", BindingFlags.NonPublic | BindingFlags.Static)!;
        GameplayItemPresentationDefinition Presentation(bool force = false)
            => (GameplayItemPresentationDefinition)resolve.Invoke(null, [player, force])!;

        Assert.Equal("CivvieUmbrellaS", Presentation().WorldSpriteName);
        Assert.True(player.TryActivateCivvieUmbrella());
        Assert.Equal("CivvieUmbrellaOpenAnimS", Presentation().WorldSpriteName);
        player.SyncCivvieUmbrellaSecondaryInput(false);
        // Closing still uses the ability strip after its active flag has cleared.
        Assert.Equal("CivvieUmbrellaOpenAnimS", Presentation(force: true).WorldSpriteName);
        Assert.Equal("CivvieUmbrellaS", Presentation().WorldSpriteName);
        Assert.True(player.TryActivateCivvieUmbrella());
        Assert.Equal("CivvieUmbrellaOpenAnimS", Presentation().WorldSpriteName);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void HeldUmbrellaWaitsForOpeningAnimationAndSpendsChargeOnlyOnce(int rate)
    {
        var world = CreateWorld(rate);
        var player = world.LocalPlayer;
        var blastCount = 0;
        var blastTime = -1d;
        for (var tick = 0; tick < rate * 2; tick++)
        {
            Advance(world, held: true);
            var blasts = world.DrainPendingVisualEvents().Count(v => v.EffectName == "AirBlast");
            if (blasts > 0) blastTime = (double)tick / rate;
            blastCount += blasts;
        }
        Assert.Equal(1, blastCount);
        Assert.InRange(blastTime, 4d / 30 - 0.0001, 4d / 30 + 1d / rate + 0.0001);
        Assert.Equal(PlayerEntity.CivvieUmbrellaMaxChargeTicks - PlayerEntity.CivvieUmbrellaOpeningChargeCost,
            player.CivvieUmbrellaChargeTicks);
        Assert.True(player.IsCivvieUmbrellaActive);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(120)]
    public void RapidTapsCannotReusePartialOpeningOrProduceFreeBlasts(int rate)
    {
        var world = CreateWorld(rate);
        for (var i = 0; i < 30; i++)
        {
            Advance(world, true);
            Advance(world, false);
        }
        Assert.DoesNotContain(world.DrainPendingVisualEvents(), v => v.EffectName == "AirBlast");
        Assert.Equal(0, world.LocalPlayer.CivvieUmbrellaOpeningElapsedTicks);

        var blasts = 0;
        for (var opening = 0; opening < 20; opening++)
        {
            for (var tick = 0; tick < rate / 5 + 1; tick++) Advance(world, true);
            Advance(world, false);
            blasts += world.DrainPendingVisualEvents().Count(v => v.EffectName == "AirBlast");
        }
        // Releases regenerate a little charge, but sustained reopening must eventually run out.
        Assert.InRange(blasts, 1, 19);
        Assert.True(world.LocalPlayer.IsCivvieUmbrellaBroken
            || world.LocalPlayer.CivvieUmbrellaChargeTicks < PlayerEntity.CivvieUmbrellaOpeningChargeCost);
    }

    [Fact]
    public void PredictionRestoreKeepsOpeningProgressAndCannotSpendTheSameBlastTwice()
    {
        var world = CreateWorld(30);
        var player = world.LocalPlayer;
        for (var tick = 0; tick < 5; tick++) Advance(world, true);
        var saved = player.CapturePredictionState();
        Assert.True(saved.CivvieUmbrellaOpeningAirblastTriggered);
        player.SyncCivvieUmbrellaSecondaryInput(false);
        player.TryActivateCivvieUmbrella();
        player.RestorePredictionState(saved);
        Assert.False(player.TrySpendCivvieUmbrellaOpeningCharge());
        Assert.Equal(saved.CivvieUmbrellaChargeTicks, player.CivvieUmbrellaChargeTicks);
        Assert.Equal(saved.CivvieUmbrellaOpeningSequence, player.CivvieUmbrellaOpeningSequence);
        Assert.Equal(saved.CivvieUmbrellaOpeningElapsedTicks, player.CivvieUmbrellaOpeningElapsedTicks);
    }

    [Fact]
    public void ReopeningBetweenRenderUpdatesRestartsTheOpeningAnimation()
    {
        var player = CreateWorld(30).LocalPlayer;
        var type = typeof(Game1).GetNestedType("PlayerRenderState", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type, nonPublic: true)!;
        var update = typeof(Game1).GetMethod("UpdateCivvieUmbrellaWeaponAnimationState", BindingFlags.Static | BindingFlags.NonPublic)!;
        player.TryActivateCivvieUmbrella();
        update.Invoke(null, [player, state, true]);
        type.GetProperty("WeaponAnimationTimeRemainingSeconds")!.SetValue(state, 0f);
        update.Invoke(null, [player, state, true]);
        Assert.Equal("CivvieUmbrellaHold", type.GetProperty("WeaponAnimationMode")!.GetValue(state)!.ToString());
        // No render update sees the intervening closed state.
        player.SyncCivvieUmbrellaSecondaryInput(false);
        player.TryActivateCivvieUmbrella();
        update.Invoke(null, [player, state, true]);
        Assert.Equal("CivvieUmbrellaOpening", type.GetProperty("WeaponAnimationMode")!.GetValue(state)!.ToString());
        Assert.Equal(0.2f, type.GetProperty("WeaponAnimationTimeRemainingSeconds")!.GetValue(state));
    }

    private static SimulationWorld CreateWorld(int rate)
    {
        var world = new SimulationWorld(new() { TicksPerSecond = rate, EnableLocalDummies = false });
        world.LocalPlayer.SetClassDefinition(CharacterClassCatalog.Quote);
        return world;
    }

    private static void Advance(SimulationWorld world, bool held)
    {
        world.SetLocalInput(default(PlayerInputSnapshot) with { FireSecondary = held,
            AimWorldX = world.LocalPlayer.X + 256, AimWorldY = world.LocalPlayer.Y });
        world.AdvanceOneTick();
    }
}
