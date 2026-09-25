using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientInputEdgeLatchTests
{
    [Fact]
    public void LatchedPrimaryPressSurvivesAReleasedRenderFrame()
    {
        var renderFrameInput = default(PlayerInputSnapshot) with
        {
            AimWorldX = 128f,
            AimWorldY = 64f,
        };

        var fixedTickInput = Game1.ApplyLatchedOneShotInputEdges(
            renderFrameInput,
            jumpPressed: false,
            secondaryAbilityPressed: false,
            primaryPressed: true,
            swapWeaponPressed: false,
            abilityPressed: false);

        Assert.True(fixedTickInput.FirePrimary);
        Assert.Equal(renderFrameInput.AimWorldX, fixedTickInput.AimWorldX);
        Assert.Equal(renderFrameInput.AimWorldY, fixedTickInput.AimWorldY);
    }

    [Fact]
    public void LatchedPrimaryPressDoesNotReplaceAnAlreadyHeldPrimaryInput()
    {
        var heldInput = default(PlayerInputSnapshot) with { FirePrimary = true };

        var fixedTickInput = Game1.ApplyLatchedOneShotInputEdges(
            heldInput,
            jumpPressed: false,
            secondaryAbilityPressed: false,
            primaryPressed: true,
            swapWeaponPressed: false,
            abilityPressed: false);

        Assert.True(fixedTickInput.FirePrimary);
        Assert.Equal(heldInput, fixedTickInput);
    }

    [Fact]
    public void LatchedScrollWheelToggleSurvivesAReleasedRenderFrame()
    {
        var fixedTickInput = Game1.ApplyLatchedOneShotInputEdges(
            default,
            jumpPressed: false,
            secondaryAbilityPressed: false,
            primaryPressed: false,
            swapWeaponPressed: false,
            abilityPressed: false,
            toggleSecondaryWeaponPressed: true);

        Assert.True(fixedTickInput.ToggleSecondaryWeapon);
        Assert.False(fixedTickInput.SwapWeapon);
    }

    [Fact]
    public void PrimaryPressStartsLocalWeaponPresentationBeforeAuthorityStateChanges()
    {
        var pendingConfirmationSeconds = 0f;

        Assert.True(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: false,
            immediateLocalPrimaryPress: true,
            elapsedSeconds: 1f / 60f,
            ref pendingConfirmationSeconds));
        Assert.True(pendingConfirmationSeconds > 0f);
    }

    [Fact]
    public void PresentationPreviewDoesNotInventASecondShotAfterAuthorityConfirmsIt()
    {
        var pendingConfirmationSeconds = 0f;
        Assert.True(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: false,
            immediateLocalPrimaryPress: true,
            elapsedSeconds: 1f / 60f,
            ref pendingConfirmationSeconds));
        Assert.False(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: true,
            immediateLocalPrimaryPress: false,
            elapsedSeconds: 1f / 60f,
            ref pendingConfirmationSeconds));
        Assert.Equal(0f, pendingConfirmationSeconds);
    }

    [Fact]
    public void WeaponAnimationIgnoresPositiveCooldownRewindDuringReconciliation()
    {
        // The predicted timer may be 4 and the authoritative correction 9;
        // that is still the same shot and must not restart recoil.
        Assert.False(Game1.IsWeaponFireAnimationStart(
            previousAmmoCount: 5,
            currentAmmoCount: 5,
            previousCooldownTicks: 4,
            currentCooldownTicks: 9));

        Assert.True(Game1.IsWeaponFireAnimationStart(
            previousAmmoCount: 5,
            currentAmmoCount: 5,
            previousCooldownTicks: 0,
            currentCooldownTicks: 9));
    }

    [Fact]
    public void DemoknightSwordAnimationOnlyStartsOnCooldownEdge()
    {
        // Holding primary while cooldown reconciles upward must not machine-gun the swing art.
        Assert.False(Game1.IsDemoknightSwordAnimationStart(
            previousCooldownTicks: 4,
            currentCooldownTicks: 9));
        Assert.True(Game1.IsDemoknightSwordAnimationStart(
            previousCooldownTicks: 0,
            currentCooldownTicks: 5));
        Assert.False(Game1.IsDemoknightSwordAnimationStart(
            previousCooldownTicks: 5,
            currentCooldownTicks: 4));
    }

    [Fact]
    public void WeaponAnimationStillDetectsAutomaticShotAmmoConsumption()
    {
        Assert.True(Game1.IsWeaponFireAnimationStart(
            previousAmmoCount: 5,
            currentAmmoCount: 4,
            previousCooldownTicks: 4,
            currentCooldownTicks: 9));
    }

    [Fact]
    public void WeaponReloadAnimationOnlyRestartsAtAReloadEdge()
    {
        Assert.True(Game1.IsWeaponReloadAnimationRestart(0, 12));
        Assert.False(Game1.IsWeaponReloadAnimationRestart(4, 12));
        Assert.False(Game1.IsWeaponReloadAnimationRestart(4, 0));
    }

    [Fact]
    public void ExpiredPresentationPreviewDoesNotSuppressALaterShot()
    {
        var pendingConfirmationSeconds = 0.01f;

        Assert.True(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: true,
            immediateLocalPrimaryPress: false,
            elapsedSeconds: 0.02f,
            ref pendingConfirmationSeconds));
    }

    [Fact]
    public void ImmediatePresentationGateRejectsCooldownAndEmptyAmmo()
    {
        Assert.False(Game1.CanStartImmediateWeaponFirePresentation(
            cooldownTicks: 1,
            ammoPerShot: 1,
            availableAmmo: 8));
        Assert.False(Game1.CanStartImmediateWeaponFirePresentation(
            cooldownTicks: 0,
            ammoPerShot: 1,
            availableAmmo: 0));
        Assert.True(Game1.CanStartImmediateWeaponFirePresentation(
            cooldownTicks: 0,
            ammoPerShot: 1,
            availableAmmo: 1));
    }

    [Theory]
    [InlineData(PrimaryWeaponKind.PelletGun, null, (int)PredictedWeaponFireVisualFamily.Shot)]
    [InlineData(PrimaryWeaponKind.Custom, BuiltInGameplayBehaviorIds.ScoutNailgun, (int)PredictedWeaponFireVisualFamily.Needle)]
    [InlineData(PrimaryWeaponKind.Custom, BuiltInGameplayBehaviorIds.SniperBow, (int)PredictedWeaponFireVisualFamily.None)]
    [InlineData(PrimaryWeaponKind.RocketLauncher, BuiltInGameplayBehaviorIds.MortarLauncher, (int)PredictedWeaponFireVisualFamily.None)]
    [InlineData(PrimaryWeaponKind.RocketLauncher, null, (int)PredictedWeaponFireVisualFamily.Rocket)]
    [InlineData(PrimaryWeaponKind.Medigun, BuiltInGameplayBehaviorIds.Medigun, (int)PredictedWeaponFireVisualFamily.None)]
    [InlineData(PrimaryWeaponKind.Revolver, null, (int)PredictedWeaponFireVisualFamily.Revolver)]
    [InlineData(PrimaryWeaponKind.Blade, null, (int)PredictedWeaponFireVisualFamily.Bubble)]
    [InlineData(PrimaryWeaponKind.GrenadeLauncher, null, (int)PredictedWeaponFireVisualFamily.Grenade)]
    public void PredictedFireVisualMapsOnlySupportedWeaponFamilies(
        PrimaryWeaponKind weaponKind,
        string? behaviorId,
        int expectedFamily)
    {
        Assert.Equal(
            (PredictedWeaponFireVisualFamily)expectedFamily,
            Game1.ResolvePredictedWeaponFireVisualFamily(weaponKind, behaviorId));
    }

    [Fact]
    public void PredictedFireVisualDoesNotInventAProjectileForCustomWeapons()
    {
        Assert.Equal(
            PredictedWeaponFireVisualFamily.None,
            Game1.ResolvePredictedWeaponFireVisualFamily(PrimaryWeaponKind.Custom, "mod.weapon.custom_beam"));
    }

    [Fact]
    public void RocketSpriteFramesMatchTheirTeamPalette()
    {
        Assert.Equal(0, Game1.GetRocketSpriteFrame(PlayerTeam.Red));
        Assert.Equal(1, Game1.GetRocketSpriteFrame(PlayerTeam.Blue));
    }

    [Theory]
    [InlineData("ShotgunSnd", 42, 42, true)]
    [InlineData("ShotgunSnd", 42, 7, false)]
    [InlineData("ShotgunSnd", -1, 42, false)]
    [InlineData("ExplosionSnd", -1, -1, true)]
    public void PredictedWeaponSoundEchoRequiresTheSameKnownPlayer(
        string soundName,
        int recentSourcePlayerId,
        int currentSourcePlayerId,
        bool expected)
    {
        Assert.Equal(
            expected,
            Game1.AreProjectileSoundEchoSourcesCompatible(
                soundName,
                recentSourcePlayerId,
                currentSourcePlayerId));
    }

    [Fact]
    public void ChaingunNamedSmgShotIsNotManagedLoopButMinigunIs()
    {
        Assert.False(Game1.IsManagedRapidFirePresentationForWeapon(PrimaryWeaponKind.PelletGun, "ChaingunSnd"));
        Assert.True(Game1.IsManagedRapidFirePresentationForWeapon(PrimaryWeaponKind.Minigun, "ChaingunSnd"));
        Assert.False(SnapshotBroadcaster.IsManagedRapidFireWeaponSound("ChaingunSnd", PrimaryWeaponKind.PelletGun));
        Assert.True(SnapshotBroadcaster.IsManagedRapidFireWeaponSound("ChaingunSnd", PrimaryWeaponKind.Minigun));
        Assert.True(Game1.IsProjectileSoundEchoCandidate("ChaingunSnd"));
        Assert.True(Game1.IsProjectileSoundEchoCandidate("PistolSnd"));
    }

    [Fact]
    public void PistolEchoCorrelationIsOneShotCompatibleOnlyForSameKnownSource()
    {
        Assert.True(Game1.AreProjectileSoundEchoSourcesCompatible("PistolSnd", 7, 7));
        Assert.False(Game1.AreProjectileSoundEchoSourcesCompatible("PistolSnd", 7, 8));
        Assert.False(Game1.AreProjectileSoundEchoSourcesCompatible("PistolSnd", -1, 7));
    }

    [Theory]
    [InlineData("PistolSnd")]
    [InlineData("ChaingunSnd")]
    public void PredictedAndAuthoritativeShotMatcherConsumesEachEchoOnce(string soundName)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var eventField = typeof(Game1).GetField("_recentProjectileSoundEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(eventField);
        eventField!.SetValue(game, Activator.CreateInstance(eventField.FieldType));

        var remember = typeof(Game1).GetMethod("RememberPlayedProjectileSound", BindingFlags.Instance | BindingFlags.NonPublic);
        var suppress = typeof(Game1).GetMethod("ShouldSuppressPredictedProjectileSoundEcho", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(remember);
        Assert.NotNull(suppress);

        remember!.Invoke(game, [soundName, new WorldSoundEvent(soundName, 0f, 0f, SourcePlayerId: 22)]);
        var authoritative = new WorldSoundEvent(soundName, 0f, 0f, EventId: 1, SourcePlayerId: 22);
        Assert.True((bool)suppress!.Invoke(game, [soundName, authoritative])!);
        Assert.False((bool)suppress.Invoke(game, [soundName, authoritative])!);
    }

    [Fact]
    public void ShotMatcherLeavesPistolEchoForTheCorrectSourceAfterRejectingAnother()
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        var eventField = typeof(Game1).GetField("_recentProjectileSoundEvents", BindingFlags.Instance | BindingFlags.NonPublic)!;
        eventField.SetValue(game, Activator.CreateInstance(eventField.FieldType));
        var remember = typeof(Game1).GetMethod("RememberPlayedProjectileSound", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var suppress = typeof(Game1).GetMethod("ShouldSuppressPredictedProjectileSoundEcho", BindingFlags.Instance | BindingFlags.NonPublic)!;
        remember.Invoke(game, ["PistolSnd", new WorldSoundEvent("PistolSnd", 0f, 0f, SourcePlayerId: 22)]);

        Assert.False((bool)suppress.Invoke(game, ["PistolSnd", new WorldSoundEvent("PistolSnd", 0f, 0f, EventId: 1, SourcePlayerId: 23)])!);
        Assert.True((bool)suppress.Invoke(game, ["PistolSnd", new WorldSoundEvent("PistolSnd", 0f, 0f, EventId: 1, SourcePlayerId: 22)])!);
    }

    [Fact]
    public void BannerAndFlareMixUseDedicatedRangesAndImpactGain()
    {
        var listener = Vector2.Zero;
        Assert.Equal(1f, Game1.GetBannerSoundMix(96f, 0f, listener).Volume);
        Assert.Equal(0f, Game1.GetBannerSoundMix(512f, 0f, listener).Volume);
        Assert.Equal(0.5f, Game1.GetFlareImpactSoundMix(0f, 0f, listener).Volume);
        Assert.Equal(0f, Game1.GetFlareImpactSoundMix(1500f, 0f, listener).Volume);
    }
}
