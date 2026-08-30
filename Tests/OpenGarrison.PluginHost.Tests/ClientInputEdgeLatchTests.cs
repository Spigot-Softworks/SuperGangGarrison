using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
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
}
