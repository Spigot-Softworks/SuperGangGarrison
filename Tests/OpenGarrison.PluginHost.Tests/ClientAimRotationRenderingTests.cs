using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenGarrison.Client;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ClientAimRotationRenderingTests
{
    private const BindingFlags InstanceAll = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void HeldTommyGunFireUsesCurrentCursorRotationWithoutWaitingForPredictionTick()
    {
        var world = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var player = world.LocalPlayer;
        player.SetClassDefinition(CharacterClassCatalog.RuntimeRegistry.CreateCharacterClassDefinition("heavy"));
        player.Spawn(PlayerTeam.Red, 200f, 200f);
        Assert.True(player.TrySelectGameplayPrimaryItem("weapon.tommy-gun"));
        player.ApplyPredictionAimWorld(player.X - 100f, player.Y);
        var currentCursorX = player.X + 100f;
        var currentCursorY = player.Y + 100f;
        world.NetworkPlayers.SetLocalInput(default(PlayerInputSnapshot) with
        {
            FirePrimary = true,
            AimWorldX = currentCursorX,
            AimWorldY = currentCursorY,
        });

        var (game, gameplay) = CreateHeadlessGame(world);
        var inputUpdate = gameplay.InputUpdate;
        SetField(inputUpdate, "_latestLocalAimWorldX", currentCursorX);
        SetField(inputUpdate, "_latestLocalAimWorldY", currentCursorY);
        SetField(inputUpdate, "_hasLatestLocalAimWorldPosition", true);
        var weaponRenderer = new GameplayWeaponRenderController((IRenderContext)game);
        gameplay.RuntimeSettings.UseLocalWeaponRotation = false;

        Assert.True(world.LocalState.Input.FirePrimary);
        Assert.Equal(180f, player.AimDirectionDegrees);
        Assert.Equal(
            GameplayWeaponRenderController.GetWeaponRotation(player),
            GetRenderWeaponRotation(weaponRenderer, player));

        gameplay.RuntimeSettings.UseLocalWeaponRotation = true;
        var expectedCursorRotation = MathF.Atan2(currentCursorY - player.Y, currentCursorX - player.X);
        var renderedRotation = GetRenderWeaponRotation(weaponRenderer, player);

        Assert.InRange(MathF.Abs(renderedRotation - expectedCursorRotation), 0f, 0.0001f);
        Assert.NotEqual(GameplayWeaponRenderController.GetWeaponRotation(player), renderedRotation);

        var nextCursorX = player.X + 100f;
        var nextCursorY = player.Y - 100f;
        SetField(inputUpdate, "_latestLocalAimWorldX", nextCursorX);
        SetField(inputUpdate, "_latestLocalAimWorldY", nextCursorY);
        var nextRenderedRotation = GetRenderWeaponRotation(weaponRenderer, player);

        Assert.InRange(MathF.Abs(nextRenderedRotation - (-MathF.PI / 4f)), 0f, 0.0001f);
        Assert.NotEqual(renderedRotation, nextRenderedRotation);
        Assert.True(world.LocalState.Input.FirePrimary);
    }

    private static (Game1 Game, GameplayManager Gameplay) CreateHeadlessGame(SimulationWorld world)
    {
        var game = (Game1)RuntimeHelpers.GetUninitializedObject(typeof(Game1));
        SetField(game, "_world", world);
        var services = new ClientServiceContainer();
        var gameplay = new GameplayManager((IGameplayContext)game);
        services.Register(gameplay);
        SetField(game, "_services", services);
        return (game, gameplay);
    }

    private static float GetRenderWeaponRotation(GameplayWeaponRenderController renderer, PlayerEntity player)
    {
        var method = typeof(GameplayWeaponRenderController).GetMethod("GetRenderWeaponRotation", InstanceAll);
        Assert.NotNull(method);
        return (float)method!.Invoke(renderer, [player])!;
    }

    private static void SetField(object instance, string name, object value)
        => instance.GetType().GetField(name, InstanceAll)!.SetValue(instance, value);
}
