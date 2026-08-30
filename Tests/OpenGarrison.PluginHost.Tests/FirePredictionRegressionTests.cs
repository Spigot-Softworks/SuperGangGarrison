using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class FirePredictionRegressionTests
{
    [Fact]
    public void ShortPracticePrimaryTapReachesTheSimulationExactlyOnce()
    {
        var world = new SimulationWorld();
        var player = world.LocalPlayer;
        var initialAmmo = player.CurrentShells;
        var expectedAmmoAfterOneShot = initialAmmo - player.PrimaryWeapon.AmmoPerShot;
        var expectedProjectileCount = player.PrimaryWeapon.ProjectilesPerShot;
        var renderFrameInput = default(PlayerInputSnapshot) with
        {
            AimWorldX = player.X + 256f,
            AimWorldY = player.Y,
        };

        // This is the same one-shot edge handoff used by the client when a
        // render-frame tap arrives between fixed simulation ticks.  Exercise
        // the resulting input through the real practice simulation rather than
        // stopping at the input helper assertion.
        var fixedTickInput = Game1.ApplyLatchedOneShotInputEdges(
            renderFrameInput,
            jumpPressed: false,
            secondaryAbilityPressed: false,
            primaryPressed: true,
            swapWeaponPressed: false,
            abilityPressed: false);

        world.SetLocalInput(fixedTickInput);
        world.AdvanceOneTick();

        Assert.Equal(expectedAmmoAfterOneShot, player.CurrentShells);
        Assert.Equal(expectedProjectileCount, world.Shots.Count);

        // Releasing the button on the next render frame must not replay the
        // latched edge or consume a second shell.
        world.SetLocalInput(default);
        world.AdvanceOneTick();

        Assert.Equal(expectedAmmoAfterOneShot, player.CurrentShells);
        Assert.Equal(expectedProjectileCount, world.Shots.Count);
    }

    [Fact]
    public void PredictedPrimaryReplayAdvancesAmmoAndCooldownBeforeAuthorityProjectileExists()
    {
        var world = new SimulationWorld();
        var player = world.LocalPlayer;
        var initialAmmo = player.CurrentShells;
        var game = CreatePredictionHarness(world);
        var predictedInput = CreatePredictedLocalInput(
            default(PlayerInputSnapshot) with
            {
                FirePrimary = true,
                AimWorldX = player.X + 256f,
                AimWorldY = player.Y,
            },
            primaryPressed: true);

        InvokePrivate(
            typeof(Game1),
            game,
            "ApplyPredictedInputStep",
            player,
            predictedInput);

        Assert.Equal(initialAmmo - player.PrimaryWeapon.AmmoPerShot, player.CurrentShells);
        Assert.True(player.PrimaryCooldownTicks > 0);
        // The prediction replay owns local weapon state only. Projectile
        // creation remains on the authority path, so this proves the client
        // is not waiting for a projectile to update ammo/cooldown.
        Assert.Empty(world.Shots);
    }

    [Fact]
    public void PredictedNailgunReplayUsesTheSelectedAlternatePrimaryState()
    {
        var world = new SimulationWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        var player = world.LocalPlayer;
        player.EquipExperimentalOffhandWeapon();
        Assert.True(player.IsExperimentalOffhandSelected);
        var initialPrimaryAmmo = player.CurrentShells;
        var initialNailgunAmmo = player.ExperimentalOffhandCurrentShells;
        var game = CreatePredictionHarness(world);
        var predictedInput = CreatePredictedLocalInput(
            default(PlayerInputSnapshot) with
            {
                FirePrimary = true,
                AimWorldX = player.X + 256f,
                AimWorldY = player.Y,
            },
            primaryPressed: true);

        InvokePrivate(
            typeof(Game1),
            game,
            "ApplyPredictedInputStep",
            player,
            predictedInput);

        Assert.Equal(initialPrimaryAmmo, player.CurrentShells);
        Assert.Equal(
            initialNailgunAmmo - player.ExperimentalOffhandWeapon!.AmmoPerShot,
            player.ExperimentalOffhandCurrentShells);
        Assert.True(player.ExperimentalOffhandCooldownTicks > 0);
        Assert.Empty(world.Needles);
    }

    [Fact]
    public void ImmediatePresentationLatchAndAuthorityConfirmationAreSeparateOneShotStates()
    {
        var pendingConfirmationSeconds = 0f;
        Assert.True(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: false,
            immediateLocalPrimaryPress: true,
            elapsedSeconds: 1f / 60f,
            ref pendingConfirmationSeconds));

        Assert.False(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: false,
            immediateLocalPrimaryPress: false,
            elapsedSeconds: 1f / 60f,
            ref pendingConfirmationSeconds));

        Assert.False(Game1.ResolvePredictedWeaponAnimationStart(
            authoritativeShotStarted: true,
            immediateLocalPrimaryPress: false,
            elapsedSeconds: 1f / 60f,
            ref pendingConfirmationSeconds));
        Assert.Equal(0f, pendingConfirmationSeconds);
    }

    private static object CreatePredictionHarness(SimulationWorld world)
    {
        var game = RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetPrivateField(game, "_config", world.Config);
        SetPrivateField(game, "_world", world);
        return game;
    }

    private static object CreatePredictedLocalInput(
        PlayerInputSnapshot input,
        bool primaryPressed)
    {
        var inputType = typeof(Game1).GetNestedType(
            "PredictedLocalInput",
            BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(Game1).FullName, "PredictedLocalInput");
        var constructor = inputType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            [
                typeof(uint),
                typeof(PlayerInputSnapshot),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
            ],
            modifiers: null)
            ?? throw new MissingMethodException(inputType.FullName, ".ctor");

        return constructor.Invoke(
        [
            1u,
            input,
            false,
            primaryPressed,
            false,
            false,
            false,
            false,
            false,
            false,
        ]);
    }

    private static void InvokePrivate(
        Type declaringType,
        object target,
        string methodName,
        params object[] arguments)
    {
        var method = declaringType.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(declaringType.FullName, methodName);
        method.Invoke(target, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, fieldName);
        field.SetValue(target, value);
    }
}
