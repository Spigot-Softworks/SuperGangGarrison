using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class LastToDieActionStatusHudTests
{
    [Theory]
    [InlineData(LastToDieWirePhase.Lobby, true)]
    [InlineData(LastToDieWirePhase.SurvivorChoice, true)]
    [InlineData(LastToDieWirePhase.RewardChoice, true)]
    [InlineData(LastToDieWirePhase.LoadingStage, true)]
    [InlineData(LastToDieWirePhase.Playing, false)]
    [InlineData(LastToDieWirePhase.Won, false)]
    [InlineData(LastToDieWirePhase.Lost, false)]
    public void HostedLastToDieMusicChangesOnlyAtGameplayBoundaries(
        LastToDieWirePhase phase,
        bool expectedMenuMusic)
    {
        Assert.Equal(expectedMenuMusic, Game1.ShouldPlayHostedLastToDieMenuMusic(phase));
    }

    [Theory]
    [InlineData(true, null, true, false, false, true)]
    [InlineData(true, null, false, false, false, false)]
    [InlineData(false, null, true, true, false, true)]
    [InlineData(false, null, true, false, false, false)]
    [InlineData(true, LastToDieWirePhase.LoadingStage, true, false, false, true)]
    [InlineData(true, LastToDieWirePhase.Playing, true, false, false, false)]
    [InlineData(true, LastToDieWirePhase.Lost, true, false, true, true)]
    [InlineData(true, LastToDieWirePhase.Lost, true, false, false, false)]
    public void HostedLastToDieRetryKeepsMenuMusicOwnershipAcrossSnapshotGap(
        bool connected,
        LastToDieWirePhase? phase,
        bool hasObservedRun,
        bool connectionPending,
        bool retryMusicPending,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldPlayHostedLastToDieMenuMusicDuringTransition(
                connected,
                phase,
                hasObservedRun,
                connectionPending,
                retryMusicPending));
    }

    [Theory]
    [InlineData(1, LastToDieWirePhase.Lost, LastToDieWirePhase.Lobby, true)]
    [InlineData(1, LastToDieWirePhase.Won, LastToDieWirePhase.Lobby, true)]
    [InlineData(1, LastToDieWirePhase.Playing, LastToDieWirePhase.Lobby, false)]
    [InlineData(2, LastToDieWirePhase.Lost, LastToDieWirePhase.Lobby, false)]
    [InlineData(2, LastToDieWirePhase.Won, LastToDieWirePhase.Lobby, false)]
    public void TerminalReturnSeparatesSoloMenuFromCoOpLobby(
        int maximumPlayers,
        LastToDieWirePhase observedPhase,
        LastToDieWirePhase currentPhase,
        bool expectedExitToMenu)
    {
        Assert.Equal(
            expectedExitToMenu,
            Game1.ShouldExitCompletedHostedLastToDieSolo(
                maximumPlayers,
                observedPhase,
                currentPhase));
    }

    [Theory]
    [InlineData(true, "127.0.0.1:8190", "Loading Last to Die...")]
    [InlineData(false, "Example Server", "Joining Example Server...")]
    public void LastToDieConnectionUsesModeSpecificLoadingCopy(
        bool isLastToDie,
        string serverLabel,
        string expected)
    {
        Assert.Equal(
            expected,
            Game1.FormatJoiningServerLoadingMessage(isLastToDie, serverLabel));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LastToDieUsesItsLoadingBarWithoutGenericJoiningPopup(
        bool isLastToDie,
        bool expectedGenericOverlay)
    {
        Assert.Equal(
            expectedGenericOverlay,
            Game1.ShouldShowJoiningServerLoadingOverlay(isLastToDie));
    }

    [Fact]
    public void ActionStatusPanelHasAMovableDefaultLayoutEntry()
    {
        var profile = new HudLayoutProfile();

        Assert.True(profile.TryResolve(
            HudElementId.LastToDieActionStatus,
            viewportWidth: 1280,
            viewportHeight: 720,
            out var resolved));
        Assert.True(resolved.Bounds.Width >= 285);
        Assert.True(resolved.Bounds.Height >= 144);
    }

    [Theory]
    [InlineData(true, true, false, false, null, true, false, true)]
    [InlineData(false, false, false, true, LastToDieWirePhase.Playing, true, false, true)]
    [InlineData(false, false, false, true, LastToDieWirePhase.RewardChoice, true, false, false)]
    [InlineData(true, true, true, false, null, true, false, false)]
    [InlineData(true, true, false, false, null, false, false, false)]
    [InlineData(true, true, false, false, null, true, true, false)]
    public void ActionStatusVisibilityCoversOfflineAndHostedLastToDie(
        bool offlineSessionActive,
        bool offlineRunAvailable,
        bool offlinePresentationBlocked,
        bool hostedConnected,
        LastToDieWirePhase? hostedPhase,
        bool localPlayerAlive,
        bool localPlayerAwaitingJoin,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.ShouldPresentLastToDieActionStatusHud(
                offlineSessionActive,
                offlineRunAvailable,
                offlinePresentationBlocked,
                hostedConnected,
                hostedPhase,
                localPlayerAlive,
                localPlayerAwaitingJoin));
    }

    [Fact]
    public void SpyActionStatesExposeActiveWindowsAndCooldowns()
    {
        var world = CreateWorld(PlayerClass.Spy);
        var spy = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate, LastToDiePerkIds.Spy.Afterlife]));
        Assert.True(spy.TryStartLastToDieSpyInfiltrate(world.Config.TicksPerSecond));
        Assert.True(spy.TryStartLastToDieSpyAfterlife(world.Config.TicksPerSecond));

        var activeLines = Game1.BuildLastToDieActionStatusLines(
            spy,
            world.Config.TicksPerSecond);
        Assert.Contains(activeLines, static line => line.Text.StartsWith(
            "INFILTRATE: PROJECTILE IMMUNE",
            System.StringComparison.Ordinal));
        Assert.Contains(activeLines, static line => line.Text.StartsWith(
            "AFTERLIFE: KILL TO REVIVE",
            System.StringComparison.Ordinal));

        spy.CompleteLastToDieSpyAfterlifeSuccess();
        spy.HydrateProtocol64LastToDieSpyInfiltrateState(
            checked((uint)(2 * world.Config.TicksPerSecond)),
            world.Config.TicksPerSecond);
        var cooldownLines = Game1.BuildLastToDieActionStatusLines(
            spy,
            world.Config.TicksPerSecond);
        Assert.Contains(cooldownLines, static line => line.Text == "INFILTRATE: 2s");
        Assert.Contains(cooldownLines, static line => line.Text.StartsWith(
            "AFTERLIFE: ",
            System.StringComparison.Ordinal));
    }

    [Fact]
    public void PredictedActionPlayerSuppliesImmediateLocalAbilityFeedback()
    {
        var authorityWorld = CreateWorld(PlayerClass.Spy);
        var predictedWorld = CreateWorld(PlayerClass.Spy);
        Assert.True(authorityWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate]));
        Assert.True(predictedWorld.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Infiltrate]));
        Assert.True(predictedWorld.LocalPlayer.TryStartLastToDieSpyInfiltrate(
            predictedWorld.Config.TicksPerSecond));

        var lines = Game1.BuildLastToDieActionStatusLines(
            authorityWorld.LocalPlayer,
            authorityWorld.Config.TicksPerSecond,
            predictedActionPlayer: predictedWorld.LocalPlayer);

        Assert.Contains(lines, static line => line.Text.StartsWith(
            "INFILTRATE: PROJECTILE IMMUNE",
            System.StringComparison.Ordinal));
    }

    [Fact]
    public void MedicLinkAndTimedProtectionStatesAreVisibleToAnyHealTargetClass()
    {
        var world = CreateWorld(PlayerClass.Heavy);
        var target = world.LocalPlayer;
        target.SetLastToDieMedicLinkProjection(
            stimulantDripActive: true,
            agilityDriveActive: true,
            martyrProtectedActive: true,
            martyrProtectorActive: false);
        target.HydrateProtocol64LastToDieMedicHailMaryTicks(15);
        target.SetLastToDieGuardianEvasionChance(0.3f);
        target.SetLastToDieStatusMovementSpeedMultiplier(0.7f);
        target.SetLastToDieStatusOutgoingDamageMultiplier(0.6f);

        var lines = Game1.BuildLastToDieActionStatusLines(target, ticksPerSecond: 30);
        Assert.Contains(lines, static line => line.Text == "MEDIC LINK: STIMULANT + AGILITY");
        Assert.Contains(lines, static line => line.Text == "MARTYR: PROTECTED AT 1 HP");
        Assert.Contains(lines, static line => line.Text == "HAIL MARY: INVULN 0.5s");
        Assert.Contains(lines, static line => line.Text == "GUARDIAN: +12 HP/S / 30% EVADE");
        Assert.Contains(lines, static line => line.Text == "TRANQ: -40% DAMAGE / -30% MOVE");
    }

    [Fact]
    public void SniperActionStatesExposeTargetStacksVolleyAndDetonation()
    {
        var world = CreateWorld(PlayerClass.Sniper);
        var sniper = world.LocalPlayer;
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [
                LastToDiePerkIds.Sniper.Ghost,
                LastToDiePerkIds.Sniper.Spotted,
                LastToDiePerkIds.Sniper.Conquistador,
                LastToDiePerkIds.Sniper.MenageATrois,
                LastToDiePerkIds.Sniper.ExplosiveTip,
            ]));
        Assert.True(sniper.TryActivateLastToDieSniperGhostCloak());
        sniper.SetLastToDieSniperMarkedTargetSlot(2);
        Assert.True(sniper.TryIncrementLastToDieSniperConquistadorStacks());
        Assert.True(sniper.TryIncrementLastToDieSniperConquistadorStacks());
        Assert.True(sniper.TryIncrementLastToDieSniperConquistadorStacks());
        sniper.HydrateProtocol64LastToDieSniperVolleyState(new LastToDieSniperVolleyState(
            QueuedArrowCount: 2,
            DueArrowCount: 0,
            SourceTicksUntilNextArrow: 3,
            VelocityX: 1f,
            VelocityY: 0f,
            Damage: 50,
            FakeSpeedMultiplier: 1f,
            Payload: default));

        var lines = Game1.BuildLastToDieActionStatusLines(
            sniper,
            world.Config.TicksPerSecond,
            static slot => slot == 2 ? "Heavy Bot" : null,
            armedExplosiveArrowCount: 2);
        Assert.Contains(lines, static line => line.Text == "GHOST: CLOAKED / FIRE x3");
        Assert.Contains(lines, static line => line.Text == "SPOTTED: Heavy Bot");
        Assert.Contains(lines, static line => line.Text == "CONQUISTADOR: +6% DAMAGE");
        Assert.Contains(lines, static line => line.Text == "VOLLEY: 2 ARROWS PENDING");
        Assert.Contains(lines, static line => line.Text == "M2 DETONATE: 2 ARROWS");
    }

    [Theory]
    [InlineData(MedicUberDeliveryMode.RegularInvulnerability, "SUPER", "BURST")]
    [InlineData(MedicUberDeliveryMode.Kritz, "CRIT", "CRAZE")]
    [InlineData(MedicUberDeliveryMode.RejuvenationRay, "REJUV", "RAY")]
    public void MedicUberHudLabelsIdentifyDeliveryMode(
        MedicUberDeliveryMode mode,
        string expectedTop,
        string expectedBottom)
    {
        Assert.Equal(
            (expectedTop, expectedBottom),
            Game1.GetLastToDieMedicUberHudLabels(mode));
    }

    [Theory]
    [InlineData(0, 30, "0.0s")]
    [InlineData(15, 30, "0.5s")]
    [InlineData(31, 30, "2s")]
    public void ActionTimersStayReadableAtSourceAndNetworkTickRates(
        int ticks,
        int ticksPerSecond,
        string expected)
    {
        Assert.Equal(expected, Game1.FormatLastToDieActionSeconds(ticks, ticksPerSecond));
    }

    private static SimulationWorld CreateWorld(PlayerClass playerClass)
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(playerClass);
        return world;
    }
}
