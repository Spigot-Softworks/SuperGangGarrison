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
    public void ExpiredPresentationPreviewDoesNotSuppressALaterShot()
    {
        var pendingConfirmationSeconds = 0.01f;

        Assert.True(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: true,
            immediateLocalPrimaryPress: false,
            elapsedSeconds: 0.02f,
            ref pendingConfirmationSeconds));
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
        var eventField = typeof(Game1).GetField("_recentProjectileSoundEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(eventField);
        eventField!.SetValue(game, Activator.CreateInstance(eventField.FieldType));

        var remember = typeof(Game1).GetMethod("RememberPlayedProjectileSound", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var suppress = typeof(Game1).GetMethod("ShouldSuppressPredictedProjectileSoundEcho", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
        var eventField = typeof(Game1).GetField("_recentProjectileSoundEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        eventField.SetValue(game, Activator.CreateInstance(eventField.FieldType));
        var remember = typeof(Game1).GetMethod("RememberPlayedProjectileSound", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var suppress = typeof(Game1).GetMethod("ShouldSuppressPredictedProjectileSoundEcho", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        remember.Invoke(game, ["PistolSnd", new WorldSoundEvent("PistolSnd", 0f, 0f, SourcePlayerId: 22)]);

        Assert.False((bool)suppress.Invoke(game, ["PistolSnd", new WorldSoundEvent("PistolSnd", 0f, 0f, EventId: 1, SourcePlayerId: 23)])!);
        Assert.True((bool)suppress.Invoke(game, ["PistolSnd", new WorldSoundEvent("PistolSnd", 0f, 0f, EventId: 1, SourcePlayerId: 22)])!);
    }

}
