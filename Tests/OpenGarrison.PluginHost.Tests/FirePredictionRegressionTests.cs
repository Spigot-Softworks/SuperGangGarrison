using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
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
        Assert.True(player.TrySelectGameplayPrimaryItem("weapon.scout-nailgun"));
        Assert.True(player.HasPrimaryBehavior(OpenGarrison.GameplayModding.BuiltInGameplayBehaviorIds.ScoutNailgun));
        var initialNailgunAmmo = player.CurrentShells;
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

        Assert.Equal(
            initialNailgunAmmo - player.PrimaryWeapon.AmmoPerShot,
            player.CurrentShells);
        Assert.True(player.PrimaryCooldownTicks > 0);
        Assert.Empty(world.Needles);
    }

    [Fact]
    public void PredictedSecondaryDoesNotFireStowedPyroWeaponAltFire()
    {
        var world = new SimulationWorld();
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Pyro);
        Assert.True(world.TrySetNetworkPlayerGameplaySecondaryItem(
            SimulationWorld.LocalPlayerSlot,
            "weapon.rocketlauncher"));
        Assert.True(world.TrySetNetworkPlayerGameplayEquippedSlot(
            SimulationWorld.LocalPlayerSlot,
            GameplayEquipmentSlot.Secondary));

        var player = world.LocalPlayer;
        Assert.False(player.HasGameplayAbilityBehavior(
            GameplayAbilityConstants.SpecialChannel,
            BuiltInGameplayBehaviorIds.PyroAirblast));
        var initialFuel = player.PyroPrimaryFuelScaled;
        var game = CreatePredictionHarness(world);
        var predictedInput = CreatePredictedLocalInput(
            default(PlayerInputSnapshot) with
            {
                FireSecondary = true,
                AimWorldX = player.X + 256f,
                AimWorldY = player.Y,
            },
            primaryPressed: false,
            secondaryAbilityPressed: true);

        InvokePrivate(
            typeof(Game1),
            game,
            "ApplyPredictedInputStep",
            player,
            predictedInput);

        Assert.Equal(0, player.PyroAirblastCooldownTicks);
        Assert.Equal(initialFuel, player.PyroPrimaryFuelScaled);
        Assert.Equal(GameplayEquipmentSlot.Secondary, player.GameplayLoadoutState.EquippedSlot);
    }

    [Fact]
    public void PredictedBuffBannerDeploymentConsumesChargeAndBlocksFollowingPrimaryFire()
    {
        var world = new SimulationWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        var player = world.LocalPlayer;
        Assert.True(player.TryAddBuffBannerDamageCharge(PlayerEntity.BuffBannerDefaultMaxChargeDamage));
        var game = CreatePredictionHarness(world);

        InvokePrivate(
            typeof(Game1),
            game,
            "ApplyPredictedInputStep",
            player,
            CreatePredictedLocalInput(
                default(PlayerInputSnapshot) with { UseAbility = true },
                primaryPressed: false,
                abilityPressed: true));

        Assert.True(player.IsBuffBannerDeploying);
        Assert.Equal(0, player.BuffBannerChargeDamage);
        var ammoAfterDeploy = player.CurrentShells;

        InvokePrivate(
            typeof(Game1),
            game,
            "ApplyPredictedInputStep",
            player,
            CreatePredictedLocalInput(
                default(PlayerInputSnapshot) with
                {
                    FirePrimary = true,
                    AimWorldX = player.X + 256f,
                    AimWorldY = player.Y,
                },
                primaryPressed: true));

        Assert.Equal(ammoAfterDeploy, player.CurrentShells);
        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void PredictedMedicHealingDartUsesIndependentUtilityCooldown()
    {
        var world = new SimulationWorld();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableSecondaryAbilities: true));
        world.PrepareLocalPlayerJoin();
        world.CompleteLocalPlayerJoin(PlayerClass.Medic);
        var player = world.LocalPlayer;
        var primaryAmmoBefore = player.CurrentShells;
        var equippedSlotBefore = player.GameplayLoadoutState.EquippedSlot;
        var equippedItemBefore = player.GameplayLoadoutState.EquippedItemId;
        var game = CreatePredictionHarness(world);

        InvokePrivate(
            typeof(Game1),
            game,
            "ApplyPredictedInputStep",
            player,
            CreatePredictedLocalInput(
                default(PlayerInputSnapshot) with
                {
                    UseAbility = true,
                    AimWorldX = player.X + 256f,
                    AimWorldY = player.Y,
                },
                primaryPressed: false,
                abilityPressed: true));

        Assert.Equal(primaryAmmoBefore, player.CurrentShells);
        Assert.Equal(PlayerEntity.MedicHealDartDefaultCooldownTicks, player.MedicHealDartCooldownTicks);
        Assert.Equal(equippedSlotBefore, player.GameplayLoadoutState.EquippedSlot);
        Assert.Equal(equippedItemBefore, player.GameplayLoadoutState.EquippedItemId);
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
        bool primaryPressed,
        bool secondaryAbilityPressed = false,
        bool abilityPressed = false)
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
            secondaryAbilityPressed,
            false,
            abilityPressed,
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
