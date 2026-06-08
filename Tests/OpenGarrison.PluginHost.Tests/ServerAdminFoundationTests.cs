using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Diagnostics;
using OpenGarrison.Core.BotBrain;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using OpenGarrison.Server.Plugins;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class ServerAdminFoundationTests
{
    [Fact]
    public void PluginCommandRegistryRequiresPermissionForProtectedCommands()
    {
        var registry = new PluginCommandRegistry();
        registry.RegisterBuiltIn(
            "kick",
            "Kick a player.",
            "kick <slot>",
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>(["ok"]),
            OpenGarrisonServerAdminPermissions.ManagePlayers);

        var unauthorizedContext = CreateCommandContext(OpenGarrisonServerAdminIdentity.CreateUnauthenticated());
        Assert.True(registry.TryExecute("kick 3", unauthorizedContext, CancellationToken.None, out var unauthorizedResponse));
        Assert.Contains("requires", Assert.Single(unauthorizedResponse), StringComparison.Ordinal);

        var authorizedContext = CreateCommandContext(new OpenGarrisonServerAdminIdentity(
            "Admin",
            OpenGarrisonServerAdminAuthority.RconSession,
            OpenGarrisonServerAdminPermissions.FullAccess,
            SourceSlot: 3));
        Assert.True(registry.TryExecute("kick 3", authorizedContext, CancellationToken.None, out var authorizedResponse));
        Assert.Equal("ok", Assert.Single(authorizedResponse));
    }

    [Fact]
    public void PluginCommandRegistryMatchesChatPrefixesForRegisteredCommandNames()
    {
        var registry = new PluginCommandRegistry();
        registry.RegisterBuiltIn(
            "where",
            "Show position.",
            "/where",
            (_, arguments, _) => Task.FromResult<IReadOnlyList<string>>([$"where:{arguments}"]));
        registry.RegisterBuiltIn(
            "!vote",
            "Vote.",
            "!vote <yes|no>",
            (_, arguments, _) => Task.FromResult<IReadOnlyList<string>>([$"vote:{arguments}"]));

        var context = CreateCommandContext(OpenGarrisonServerAdminIdentity.CreateUnauthenticated(1));

        Assert.True(registry.TryExecute("/where @me", context, CancellationToken.None, out var slashResponse));
        Assert.Equal("where:@me", Assert.Single(slashResponse));

        Assert.True(registry.TryExecute("!where @me", context, CancellationToken.None, out var bangResponse));
        Assert.Equal("where:@me", Assert.Single(bangResponse));

        Assert.True(registry.TryExecute("/vote yes", context, CancellationToken.None, out var prefixedRegistrationResponse));
        Assert.Equal("vote:yes", Assert.Single(prefixedRegistrationResponse));
    }

    [Fact]
    public void ServerCvarRegistryRedactsProtectedValuesAndAppliesTypedUpdates()
    {
        var registry = new ServerCvarRegistry();
        var rconPassword = "secret";
        var autoBalance = false;
        registry.RegisterString(
            "sv_rcon_password",
            "RCON password",
            string.Empty,
            () => rconPassword,
            value =>
            {
                rconPassword = value;
                return null;
            },
            isProtected: true);
        registry.RegisterBoolean(
            "sv_autobalance",
            "Auto-balance",
            defaultValue: false,
            () => autoBalance,
            value => autoBalance = value);

        Assert.True(registry.TryGet("sv_rcon_password", out var protectedCvar));
        Assert.Equal("<protected>", protectedCvar.CurrentValue);
        Assert.True(registry.TryGet("sv_rcon_password", includeProtectedValue: true, out var revealedProtectedCvar));
        Assert.Equal("secret", revealedProtectedCvar.CurrentValue);

        Assert.True(registry.TrySet("sv_autobalance", "on", out var updatedCvar, out var errorMessage));
        Assert.Equal(string.Empty, errorMessage);
        Assert.True(autoBalance);
        Assert.Equal("true", updatedCvar.CurrentValue);
    }

    [Fact]
    public void ServerCvarRegistrySupportsRuntimeProtectionOverrides()
    {
        var root = CreateTempRoot();
        var policyPath = Path.Combine(root, "config", "server-cvar-policy.json");
        var autoBalance = true;

        var registry = new ServerCvarRegistry();
        registry.RegisterBoolean(
            "sv_autobalance",
            "Auto-balance",
            defaultValue: true,
            () => autoBalance,
            value => autoBalance = value);
        registry.EnableRuntimeProtectionPersistence(policyPath);

        Assert.True(registry.TryProtect("sv_autobalance", out var protectedCvar, out var protectError));
        Assert.Equal(string.Empty, protectError);
        Assert.True(protectedCvar.IsProtected);
        Assert.Equal("<protected>", protectedCvar.CurrentValue);

        Assert.True(registry.TryGet("sv_autobalance", out var maskedCvar));
        Assert.True(maskedCvar.IsProtected);
        Assert.Equal("<protected>", maskedCvar.CurrentValue);

        Assert.True(registry.TryGet("sv_autobalance", includeProtectedValue: true, out var unmaskedCvar));
        Assert.True(unmaskedCvar.IsProtected);
        Assert.Equal("true", unmaskedCvar.CurrentValue);

        Assert.False(registry.TrySet("sv_autobalance", "false", allowProtectedMutation: false, out var rejectedCvar, out var rejectedError));
        Assert.Equal("Cvar is protected.", rejectedError);
        Assert.True(rejectedCvar.IsProtected);
        Assert.Equal("<protected>", rejectedCvar.CurrentValue);

        Assert.True(registry.TrySet("sv_autobalance", "false", allowProtectedMutation: true, out var updatedCvar, out var updateError));
        Assert.Equal(string.Empty, updateError);
        Assert.True(updatedCvar.IsProtected);
        Assert.Equal("false", updatedCvar.CurrentValue);
        Assert.False(autoBalance);

        var reloadedRegistry = new ServerCvarRegistry();
        reloadedRegistry.RegisterBoolean(
            "sv_autobalance",
            "Auto-balance",
            defaultValue: true,
            () => autoBalance,
            value => autoBalance = value);
        reloadedRegistry.EnableRuntimeProtectionPersistence(policyPath);

        Assert.True(reloadedRegistry.TryGet("sv_autobalance", out var reloadedMaskedCvar));
        Assert.True(reloadedMaskedCvar.IsProtected);
        Assert.Equal("<protected>", reloadedMaskedCvar.CurrentValue);
        Assert.True(reloadedRegistry.TryGet("sv_autobalance", includeProtectedValue: true, out var reloadedUnmaskedCvar));
        Assert.True(reloadedUnmaskedCvar.IsProtected);
        Assert.Equal("false", reloadedUnmaskedCvar.CurrentValue);
    }

    [Fact]
    public void ServerCvarRegistryAppliesTimeLimitAndRespawnRuleUpdates()
    {
        var world = new SimulationWorld();
        var registry = new ServerCvarRegistry();
        registry.RegisterInteger(
            "sv_timelimit",
            "Time limit",
            world.MatchRules.TimeLimitMinutes,
            () => world.MatchRules.TimeLimitMinutes,
            world.SetTimeLimitMinutes,
            minValue: 1,
            maxValue: 255);
        registry.RegisterInteger(
            "sv_respawnseconds",
            "Respawn time",
            world.ConfiguredRespawnSeconds,
            () => world.ConfiguredRespawnSeconds,
            world.SetRespawnSeconds,
            minValue: 0,
            maxValue: 255);

        for (var index = 0; index < 60; index += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.True(registry.TrySet("sv_timelimit", "20", out var updatedTimeLimit, out var timeLimitError));
        Assert.Equal(string.Empty, timeLimitError);
        Assert.Equal(20, world.MatchRules.TimeLimitMinutes);
        Assert.Equal("20", updatedTimeLimit.CurrentValue);
        Assert.Equal(20 * world.Config.TicksPerSecond * 60 - 60, world.MatchState.TimeRemainingTicks);

        Assert.True(registry.TrySet("sv_respawnseconds", "9", out var updatedRespawn, out var respawnError));
        Assert.Equal(string.Empty, respawnError);
        Assert.Equal(9, world.ConfiguredRespawnSeconds);
        Assert.Equal("9", updatedRespawn.CurrentValue);
    }

    [Fact]
    public void ServerCvarRegistryAppliesFloatGameplayTuningUpdates()
    {
        var world = new SimulationWorld();
        var registry = new ServerCvarRegistry();
        registry.RegisterFloat(
            "sv_player_scale",
            "Player scale",
            world.ConfiguredPlayerScale,
            () => world.ConfiguredPlayerScale,
            world.SetPlayerScale,
            minValue: PlayerEntity.MinPlayerScale,
            maxValue: PlayerEntity.MaxPlayerScale);
        registry.RegisterFloat(
            "sv_map_scale",
            "Map scale",
            world.ConfiguredMapScale,
            () => world.ConfiguredMapScale,
            world.SetMapScale,
            minValue: 0.25f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_movement_speed_scale",
            "Movement speed scale",
            world.ConfiguredMovementSpeedScale,
            () => world.ConfiguredMovementSpeedScale,
            world.SetMovementSpeedScale,
            minValue: 0.1f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_projectile_speed_scale",
            "Projectile speed scale",
            world.ConfiguredProjectileSpeedScale,
            () => world.ConfiguredProjectileSpeedScale,
            world.SetProjectileSpeedScale,
            minValue: 0.1f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_damage_scale",
            "Damage scale",
            world.ConfiguredDamageScale,
            () => world.ConfiguredDamageScale,
            world.SetDamageScale,
            minValue: 0f,
            maxValue: 10f);
        registry.RegisterFloat(
            "sv_gravity_scale",
            "Gravity scale",
            world.ConfiguredGravityScale,
            () => world.ConfiguredGravityScale,
            world.SetGravityScale,
            minValue: 0f,
            maxValue: 4f);
        registry.RegisterFloat(
            "sv_horizontal_speed_clamp",
            "Horizontal clamp",
            world.ConfiguredHorizontalSpeedClampPerTick,
            () => world.ConfiguredHorizontalSpeedClampPerTick,
            world.SetHorizontalSpeedClampPerTick,
            minValue: 1f,
            maxValue: 60f);
        registry.RegisterFloat(
            "sv_vertical_speed_clamp",
            "Vertical clamp",
            world.ConfiguredVerticalSpeedClampPerTick,
            () => world.ConfiguredVerticalSpeedClampPerTick,
            world.SetVerticalSpeedClampPerTick,
            minValue: 1f,
            maxValue: 60f);
        registry.RegisterBoolean(
            "sv_roundendff",
            "Round-end friendly fire",
            world.RoundEndFriendlyFireEnabled,
            () => world.RoundEndFriendlyFireEnabled,
            world.SetRoundEndFriendlyFire);

        var originalWorldWidth = world.Bounds.Width;
        var originalWorldHeight = world.Bounds.Height;

        Assert.True(registry.TrySet("sv_player_scale", "1.5", out var updatedPlayerScale, out var playerScaleError));
        Assert.Equal(string.Empty, playerScaleError);
        Assert.Equal(1.5f, world.ConfiguredPlayerScale);
        Assert.Equal(1.5f, world.LocalPlayer.PlayerScale);
        Assert.Equal("1.5", updatedPlayerScale.CurrentValue);

        Assert.True(registry.TrySet("sv_map_scale", "1.25", out var updatedMapScale, out var mapScaleError));
        Assert.Equal(string.Empty, mapScaleError);
        Assert.Equal(1.25f, world.ConfiguredMapScale);
        Assert.Equal(1.25f, world.Level.MapScale);
        Assert.Equal("1.25", updatedMapScale.CurrentValue);
        Assert.True(world.Bounds.Width > originalWorldWidth);
        Assert.True(world.Bounds.Height > originalWorldHeight);

        Assert.True(registry.TrySet("sv_movement_speed_scale", "1.5", out var updatedMovementScale, out var movementScaleError));
        Assert.Equal(string.Empty, movementScaleError);
        Assert.Equal(1.5f, world.ConfiguredMovementSpeedScale);
        Assert.Equal("1.5", updatedMovementScale.CurrentValue);

        Assert.True(registry.TrySet("sv_projectile_speed_scale", "1.25", out var updatedProjectileScale, out var projectileScaleError));
        Assert.Equal(string.Empty, projectileScaleError);
        Assert.Equal(1.25f, world.ConfiguredProjectileSpeedScale);
        Assert.Equal("1.25", updatedProjectileScale.CurrentValue);

        Assert.True(registry.TrySet("sv_damage_scale", "2", out var updatedDamageScale, out var damageScaleError));
        Assert.Equal(string.Empty, damageScaleError);
        Assert.Equal(2f, world.ConfiguredDamageScale);
        Assert.Equal("2", updatedDamageScale.CurrentValue);

        Assert.True(registry.TrySet("sv_gravity_scale", "0", out var updatedGravityScale, out var gravityScaleError));
        Assert.Equal(string.Empty, gravityScaleError);
        Assert.Equal(0f, world.ConfiguredGravityScale);
        Assert.Equal("0", updatedGravityScale.CurrentValue);

        Assert.True(registry.TrySet("sv_horizontal_speed_clamp", "8", out var updatedHorizontalClamp, out var horizontalClampError));
        Assert.Equal(string.Empty, horizontalClampError);
        Assert.Equal(8f, world.ConfiguredHorizontalSpeedClampPerTick);
        Assert.Equal("8", updatedHorizontalClamp.CurrentValue);

        Assert.True(registry.TrySet("sv_vertical_speed_clamp", "6", out var updatedVerticalClamp, out var verticalClampError));
        Assert.Equal(string.Empty, verticalClampError);
        Assert.Equal(6f, world.ConfiguredVerticalSpeedClampPerTick);
        Assert.Equal("6", updatedVerticalClamp.CurrentValue);

        Assert.True(registry.TrySet("sv_roundendff", "on", out var updatedRoundEndFriendlyFire, out var roundEndFriendlyFireError));
        Assert.Equal(string.Empty, roundEndFriendlyFireError);
        Assert.True(world.RoundEndFriendlyFireEnabled);
        Assert.Equal("true", updatedRoundEndFriendlyFire.CurrentValue);

        Assert.True(world.LocalPlayer.MaxRunSpeed > CharacterClassCatalog.Scout.MaxRunSpeed);
        world.LocalPlayer.AddImpulse(1000f, 1000f);
        var startedGrounded = world.LocalPlayer.PrepareMovement(
            new PlayerInputSnapshot(false, false, false, false, false, false, false, false, false, 0f, 0f, false),
            world.Level,
            world.LocalPlayerTeam,
            world.Config.FixedDeltaSeconds,
            out _);
        Assert.True(startedGrounded || !startedGrounded);
        Assert.True(world.LocalPlayer.HorizontalSpeed <= 8f * LegacyMovementModel.SourceTicksPerSecond + 0.001f);
        Assert.True(world.LocalPlayer.VerticalSpeed <= 6f * LegacyMovementModel.SourceTicksPerSecond + 0.001f);
    }

    [Fact]
    public void ServerGameplayTuningAppliesDamageAndGravityAtRuntime()
    {
        var defaultDamageWorld = new SimulationWorld();
        var boostedDamageWorld = new SimulationWorld();
        boostedDamageWorld.SetDamageScale(2f);

        defaultDamageWorld.LocalPlayer.IgniteAfterburn(2, 30f, PlayerEntity.BurnMaxIntensity, afterburnFalloff: false, burnFalloffAmount: 0f);
        boostedDamageWorld.LocalPlayer.IgniteAfterburn(2, 30f, PlayerEntity.BurnMaxIntensity, afterburnFalloff: false, burnFalloffAmount: 0f);

        var neutralInput = new PlayerInputSnapshot(false, false, false, false, false, false, false, false, false, 0f, 0f, false);
        for (var index = 0; index < 5; index += 1)
        {
            defaultDamageWorld.LocalPlayer.AdvanceTickState(neutralInput, defaultDamageWorld.Config.FixedDeltaSeconds);
            boostedDamageWorld.LocalPlayer.AdvanceTickState(neutralInput, boostedDamageWorld.Config.FixedDeltaSeconds);
        }
        Assert.True(boostedDamageWorld.LocalPlayer.Health < defaultDamageWorld.LocalPlayer.Health);

        var defaultGravityWorld = new SimulationWorld();
        var zeroGravityWorld = new SimulationWorld();
        zeroGravityWorld.SetGravityScale(0f);

        defaultGravityWorld.LocalPlayer.TeleportTo(defaultGravityWorld.LocalPlayer.X, defaultGravityWorld.LocalPlayer.Y - 192f);
        zeroGravityWorld.LocalPlayer.TeleportTo(zeroGravityWorld.LocalPlayer.X, zeroGravityWorld.LocalPlayer.Y - 192f);

        var defaultStartedGrounded = defaultGravityWorld.LocalPlayer.PrepareMovement(
            neutralInput,
            defaultGravityWorld.Level,
            defaultGravityWorld.LocalPlayerTeam,
            defaultGravityWorld.Config.FixedDeltaSeconds,
            out _);
        var zeroGravityStartedGrounded = zeroGravityWorld.LocalPlayer.PrepareMovement(
            neutralInput,
            zeroGravityWorld.Level,
            zeroGravityWorld.LocalPlayerTeam,
            zeroGravityWorld.Config.FixedDeltaSeconds,
            out _);

        Assert.False(defaultStartedGrounded);
        Assert.False(zeroGravityStartedGrounded);

        defaultGravityWorld.LocalPlayer.CompleteMovement(
            defaultGravityWorld.Level,
            defaultGravityWorld.LocalPlayerTeam,
            defaultGravityWorld.Config.FixedDeltaSeconds,
            defaultStartedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);
        zeroGravityWorld.LocalPlayer.CompleteMovement(
            zeroGravityWorld.Level,
            zeroGravityWorld.LocalPlayerTeam,
            zeroGravityWorld.Config.FixedDeltaSeconds,
            zeroGravityStartedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);

        Assert.True(defaultGravityWorld.LocalPlayer.VerticalSpeed > zeroGravityWorld.LocalPlayer.VerticalSpeed + 0.001f);
    }

    [Fact]
    public void ServerGameplayTuningSupportsPerPlayerMovementAndGravityOverrides()
    {
        var world = new SimulationWorld();

        Assert.True(world.TrySetNetworkPlayerMovementSpeedScale(SimulationWorld.LocalPlayerSlot, 2.5f));
        Assert.True(world.TrySetNetworkPlayerGravityScale(SimulationWorld.LocalPlayerSlot, 0.5f));
        Assert.Equal(2.5f, world.LocalPlayer.ServerMovementSpeedScale);
        Assert.Equal(0.5f, world.LocalPlayer.ServerGravityScale);
        Assert.True(world.HasNetworkPlayerMovementSpeedScaleOverride(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.HasNetworkPlayerGravityScaleOverride(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.LocalPlayer.TryGetReplicatedStateFloat(PlayerEntity.ServerTuningReplicatedStateOwnerId, PlayerEntity.MovementSpeedScaleReplicatedStateKey, out var replicatedMovementSpeedScale));
        Assert.True(world.LocalPlayer.TryGetReplicatedStateFloat(PlayerEntity.ServerTuningReplicatedStateOwnerId, PlayerEntity.GravityScaleReplicatedStateKey, out var replicatedGravityScale));
        Assert.Equal(2.5f, replicatedMovementSpeedScale);
        Assert.Equal(0.5f, replicatedGravityScale);

        world.SetMovementSpeedScale(1.5f);
        world.SetGravityScale(1.25f);
        Assert.Equal(2.5f, world.LocalPlayer.ServerMovementSpeedScale);
        Assert.Equal(0.5f, world.LocalPlayer.ServerGravityScale);
        Assert.Equal(1.5f, world.EnemyPlayer.ServerMovementSpeedScale);
        Assert.Equal(1.25f, world.EnemyPlayer.ServerGravityScale);

        Assert.True(world.TryClearNetworkPlayerMovementSpeedScale(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.TryClearNetworkPlayerGravityScale(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.HasNetworkPlayerMovementSpeedScaleOverride(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.HasNetworkPlayerGravityScaleOverride(SimulationWorld.LocalPlayerSlot));
        Assert.Equal(1.5f, world.LocalPlayer.ServerMovementSpeedScale);
        Assert.Equal(1.25f, world.LocalPlayer.ServerGravityScale);
    }

    [Fact]
    public void DemoknightVanillaChargeReorientsVelocityFromMouseFacing()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        world.ForceRespawnLocalPlayer();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableDemoknightKit: true,
            EnableDemoknightFullControlDuringCharge: false));

        var player = world.LocalPlayer;
        Assert.True(player.TryStartExperimentalDemoknightCharge());

        var dt = world.Config.FixedDeltaSeconds;
        var aimRight = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 256f,
            AimWorldY: player.Y,
            DebugKill: false);

        player.Advance(aimRight, jumpPressed: false, world.Level, world.LocalPlayerTeam, dt);
        Assert.True(player.HorizontalSpeed > 0f);

        var aimLeft = aimRight with { AimWorldX = player.X - 256f };
        for (var tick = 0; tick < 30; tick += 1)
        {
            player.Advance(aimLeft, jumpPressed: false, world.Level, world.LocalPlayerTeam, dt);
            if (player.HorizontalSpeed < 0f)
            {
                break;
            }
        }

        Assert.True(player.FacingDirectionX < 0f);
        Assert.True(player.HorizontalSpeed < 0f);
    }

    [Fact]
    public void DemoknightFullControlChargeAllowsJump()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        world.ForceRespawnLocalPlayer();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableDemoknightKit: true,
            EnableDemoknightFullControlDuringCharge: true));

        var player = world.LocalPlayer;
        var neutralInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 256f,
            AimWorldY: player.Y,
            DebugKill: false);
        for (var tick = 0; tick < 60 && !player.IsGrounded; tick += 1)
        {
            var preGrounded = player.PrepareMovement(neutralInput, world.Level, world.LocalPlayerTeam, world.Config.FixedDeltaSeconds, out _);
            player.CompleteMovement(world.Level, world.LocalPlayerTeam, world.Config.FixedDeltaSeconds, preGrounded, jumped: false, allowDropdownFallThrough: false);
        }

        Assert.True(player.IsGrounded);
        Assert.True(player.TryStartExperimentalDemoknightCharge());

        var input = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: true,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 256f,
            AimWorldY: player.Y,
            DebugKill: false);
        var previousInput = input with { Up = false };

        var team = world.LocalPlayerTeam;
        _ = player.PrepareMovement(input, world.Level, team, world.Config.FixedDeltaSeconds, out var canMove);
        Assert.True(player.IsAlive);
        Assert.True(player.IsExperimentalDemoknightCharging);
        Assert.True(canMove);
        var jumped = player.TryJumpIfPossible(canMove, jumpPressed: input.Up && !previousInput.Up);

        Assert.True(jumped);
        Assert.True(player.VerticalSpeed < 0f);
    }

    [Fact]
    public void DemoknightFullControlChargeAllowsHeldJumpIntentWhileGrounded()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        world.ForceRespawnLocalPlayer();
        world.ConfigureExperimentalGameplaySettings(new ExperimentalGameplaySettings(
            EnableDemoknightKit: true,
            EnableDemoknightFullControlDuringCharge: true));

        var player = world.LocalPlayer;
        var neutralInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 256f,
            AimWorldY: player.Y,
            DebugKill: false);
        for (var tick = 0; tick < 60 && !player.IsGrounded; tick += 1)
        {
            var preGrounded = player.PrepareMovement(neutralInput, world.Level, world.LocalPlayerTeam, world.Config.FixedDeltaSeconds, out _);
            player.CompleteMovement(world.Level, world.LocalPlayerTeam, world.Config.FixedDeltaSeconds, preGrounded, jumped: false, allowDropdownFallThrough: false);
        }

        Assert.True(player.IsGrounded);
        Assert.True(player.TryStartExperimentalDemoknightCharge());

        var input = neutralInput with { Up = true };

        _ = player.PrepareMovement(input, world.Level, world.LocalPlayerTeam, world.Config.FixedDeltaSeconds, out var canMove);
        var jumped = player.TryJumpIfPossible(canMove, jumpPressed: false);

        Assert.True(jumped);
        Assert.True(player.VerticalSpeed < 0f);
    }

    [Fact]
    public void LivePlayerScalePreservesCollisionFootprintAnchorWhenSpaceAllows()
    {
        var world = new SimulationWorld();
        world.LocalPlayer.GetCollisionBounds(out var previousLeft, out _, out var previousRight, out var previousBottom);
        var previousCenterX = (previousLeft + previousRight) * 0.5f;

        Assert.True(world.LocalPlayer.TryApplyLiveScale(0.5f, world.Level, world.LocalPlayer.Team));

        world.LocalPlayer.GetCollisionBounds(out var scaledLeft, out _, out var scaledRight, out var scaledBottom);
        var scaledCenterX = (scaledLeft + scaledRight) * 0.5f;

        Assert.Equal(previousBottom, scaledBottom, precision: 3);
        Assert.Equal(previousCenterX, scaledCenterX, precision: 3);
    }

    [Fact]
    public void ServerAdminTargetResolverResolvesUserIdsSelectorsAndUniquePartialNames()
    {
        var players = new[]
        {
            CreatePlayerInfo(1, 101, "Alpha", team: PlayerTeam.Red, playerClass: PlayerClass.Soldier, isAlive: true, playerId: 11),
            CreatePlayerInfo(2, 202, "Bravo", team: PlayerTeam.Blue, playerClass: PlayerClass.Medic, isAlive: false, playerId: 22),
            CreatePlayerInfo(3, 303, "Watcher", isSpectator: true),
        };
        var resolver = new ServerAdminTargetResolver(() => players);

        var self = resolver.Resolve("@me", new ServerAdminTargetQueryOptions(SourceSlot: 1, AllowMultiple: false));
        Assert.True(self.Success);
        Assert.Equal(101, Assert.Single(self.Targets).UserId);

        var byUserId = resolver.Resolve("#202", new ServerAdminTargetQueryOptions(AllowMultiple: false));
        Assert.True(byUserId.Success);
        Assert.Equal((byte)2, Assert.Single(byUserId.Targets).Slot);

        var partial = resolver.Resolve("lph", new ServerAdminTargetQueryOptions(AllowMultiple: false));
        Assert.True(partial.Success);
        Assert.Equal("Alpha", Assert.Single(partial.Targets).Name);

        var alive = resolver.Resolve("@alive", new ServerAdminTargetQueryOptions());
        Assert.True(alive.Success);
        Assert.Equal(101, Assert.Single(alive.Targets).UserId);
    }

    [Fact]
    public void ServerAdminTargetResolverRejectsAmbiguousAndUnavailableTargets()
    {
        var players = new[]
        {
            CreatePlayerInfo(1, 101, "Alice", team: PlayerTeam.Red, playerClass: PlayerClass.Scout, isAlive: true, playerId: 1),
            CreatePlayerInfo(2, 202, "Alicia", team: PlayerTeam.Red, playerClass: PlayerClass.Soldier, isAlive: true, playerId: 2),
            CreatePlayerInfo(3, 303, "Alice", team: PlayerTeam.Blue, playerClass: PlayerClass.Medic, isAlive: false, playerId: 3),
        };
        var resolver = new ServerAdminTargetResolver(() => players);

        var ambiguousPartial = resolver.Resolve("ali", new ServerAdminTargetQueryOptions(AllowMultiple: false));
        Assert.False(ambiguousPartial.Success);
        Assert.Equal("ambiguous_name", ambiguousPartial.ErrorCode);

        var duplicateExact = resolver.Resolve("Alice", new ServerAdminTargetQueryOptions(AllowMultiple: false));
        Assert.False(duplicateExact.Success);
        Assert.Equal("ambiguous_name", duplicateExact.ErrorCode);

        var missingSelf = resolver.Resolve("@me", new ServerAdminTargetQueryOptions(AllowMultiple: false));
        Assert.False(missingSelf.Success);
        Assert.Equal("source_slot_required", missingSelf.ErrorCode);

        var multiSelector = resolver.Resolve("@red", new ServerAdminTargetQueryOptions(AllowMultiple: false));
        Assert.False(multiSelector.Success);
        Assert.Equal("multiple_targets", multiSelector.ErrorCode);
    }

    [Fact]
    public void AdminChatRouterAuthenticatesAndReplaysReservedCommandPrivately()
    {
        AdminCommandCapturePlugin.Reset();

        var now = TimeSpan.Zero;
        var sessionManager = new ServerAdminSessionManager("secret", () => now);
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", now);
        var messages = new List<(byte Slot, string Text)>();
        var rootPath = CreateTempRoot();
        var host = new OpenGarrison.Server.PluginHost(
            static () => throw new InvalidOperationException("Unexpected world access."),
            new PluginCommandRegistry(),
            new FakeServerReadOnlyState(),
            new FakeServerAdminOperations(),
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            slot => slot == client.Slot
                ? ServerAdminSessionManager.GetClientIdentity(client)
                : OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            static (_, _, _, _, _, _, _) => { },
            static (_, _, _, _, _, _) => { },
            Path.Combine(rootPath, "plugins"),
            Path.Combine(rootPath, "config"),
            Path.Combine(rootPath, "maps"),
            _ => { });
        host.LoadPlugins([typeof(AdminCommandCapturePlugin).Assembly]);

        var router = new ServerAdminChatRouter(
            sessionManager,
            () => host,
            (slot, text) => messages.Add((slot, text)));

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_status", teamOnly: false));
        Assert.Contains(messages, message => message.Slot == 1 && message.Text.Contains("!gt_auth <password>", StringComparison.Ordinal));
        Assert.Empty(AdminCommandCapturePlugin.HandledCommands);

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_auth secret", teamOnly: false));
        Assert.Contains(messages, message => message.Slot == 1 && message.Text.Contains("granted", StringComparison.OrdinalIgnoreCase));

        var handled = Assert.Single(AdminCommandCapturePlugin.HandledCommands);
        Assert.Equal("!gt_status", handled.Text);
        Assert.True(handled.Identity.IsAuthenticated);
        Assert.Equal(OpenGarrisonServerAdminAuthority.RconSession, handled.Identity.Authority);
        Assert.Equal((byte)1, handled.Identity.SourceSlot);
    }

    [Fact]
    public void AdminChatRouterAuthenticatesAndReplaysBundledGarrisonToolsCommand()
    {
        var now = TimeSpan.Zero;
        var sessionManager = new ServerAdminSessionManager("secret", () => now);
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", now);
        var routerMessages = new List<(byte Slot, string Text)>();
        var logs = new List<string>();
        var repoRoot = FindRepositoryRoot();
        var configRoot = CreateTempRoot();
        var cvars = new FakeServerCvarRegistry();
        cvars.Add(new OpenGarrisonServerCvarInfo(
            "sv_autobalance",
            "Auto-balance",
            OpenGarrisonServerCvarValueType.Boolean,
            "true",
            "true",
            IsProtected: false,
            IsReadOnly: false));
        var adminOperations = new FakeServerAdminOperations();
        var host = new OpenGarrison.Server.PluginHost(
            static () => throw new InvalidOperationException("Unexpected world access."),
            new PluginCommandRegistry(),
            new FakeServerReadOnlyState(),
            adminOperations,
            cvars,
            new FakeServerScheduler(),
            slot => slot == client.Slot
                ? ServerAdminSessionManager.GetClientIdentity(client)
                : OpenGarrisonServerAdminIdentity.CreateUnauthenticated(slot),
            static (_, _, _, _, _, _, _) => { },
            static (_, _, _, _, _, _) => { },
            Path.Combine(repoRoot, "Plugins", "Packaged"),
            Path.Combine(configRoot, "config"),
            Path.Combine(configRoot, "maps"),
            logs.Add);
        host.LoadPlugins();

        var router = new ServerAdminChatRouter(
            sessionManager,
            () => host,
            (slot, text) => routerMessages.Add((slot, text)));

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_cvar sv_autobalance off", teamOnly: false));
        Assert.Contains(routerMessages, message => message.Slot == 1 && message.Text.Contains("!gt_auth <password>", StringComparison.Ordinal));
        Assert.True(cvars.TryGet("sv_autobalance", out var beforeAuthCvar));
        Assert.Equal("true", beforeAuthCvar.CurrentValue);

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_auth secret", teamOnly: false));
        Assert.Contains(routerMessages, message => message.Slot == 1 && message.Text.Contains("granted", StringComparison.OrdinalIgnoreCase));
        Assert.True(cvars.TryGet("sv_autobalance", out var afterAuthCvar));
        Assert.Equal("off", afterAuthCvar.CurrentValue);
        Assert.Contains(adminOperations.SystemMessages, message => message.Slot == 1 && message.Text.Contains("sv_autobalance", StringComparison.Ordinal));

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_demo status", teamOnly: false));
        Assert.Contains(adminOperations.SystemMessages, message => message.Slot == 1 && message.Text.Contains("demo | status=idle", StringComparison.Ordinal));

        Assert.True(router.TryHandlePrivateChatCommand(client, "!gt_demo start", teamOnly: false));
        Assert.Contains(adminOperations.SystemMessages, message => message.Slot == 1 && message.Text.Contains("demo recording started", StringComparison.Ordinal));
    }

    [Fact]
    public void ServerAdminOperationsSplitOversizedPrivateSystemMessagesSafely()
    {
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sentMessages = new List<IProtocolMessage>();
        var operations = new ServerAdminOperations(
            _ => { },
            (_, message) =>
            {
                ProtocolCodec.Serialize(message);
                sentMessages.Add(message);
            },
            () => clients,
            static () => throw new InvalidOperationException("Unexpected session manager access."),
            static () => throw new InvalidOperationException("Unexpected world access."),
            static () => null,
            static () => throw new InvalidOperationException("Unexpected map rotation access."),
            static () => throw new InvalidOperationException("Unexpected snapshot broadcaster access."),
            static () => throw new InvalidOperationException("Unexpected bot manager access."));

        var oversizedMessage = string.Join(" | ", Enumerable.Repeat("!gt_help", 40));
        operations.SendSystemMessage(client.Slot, oversizedMessage);

        Assert.True(sentMessages.Count > 1);
        Assert.All(sentMessages, message =>
        {
            var chatRelay = Assert.IsType<ChatRelayMessage>(message);
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(chatRelay.Text) <= ProtocolCodec.MaxChatBytes);
        });
    }

    [Fact]
    public void ServerAdminOperationsCanRenameIgniteAndGagPlayers()
    {
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var world = new SimulationWorld();
        var sessionManager = CreateSessionManager(world, clients);
        var operations = new ServerAdminOperations(
            _ => { },
            static (_, _) => { },
            () => clients,
            () => sessionManager,
            () => world,
            static () => null,
            static () => throw new InvalidOperationException("Unexpected map rotation access."),
            static () => throw new InvalidOperationException("Unexpected snapshot broadcaster access."),
            static () => throw new InvalidOperationException("Unexpected bot manager access."));

        Assert.True(operations.TryRenamePlayer(client.Slot, "##RenamedPlayer"));
        Assert.Equal("RenamedPlayer", client.Name);
        Assert.Equal("RenamedPlayer", world.LocalPlayer.DisplayName);

        Assert.True(operations.TrySetPlayerGagged(client.Slot, true));
        Assert.True(client.IsGagged);
        Assert.True(operations.TrySetPlayerGagged(client.Slot, false));
        Assert.False(client.IsGagged);

        Assert.True(operations.TryIgnitePlayer(client.Slot, 4f));
        Assert.True(world.LocalPlayer.IsBurning);
    }

    [Fact]
    public void ServerIncomingMessageDispatcherBlocksChatForGaggedClients()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Tester", TimeSpan.Zero)
        {
            IsAuthorized = true,
            IsGagged = true,
        };
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sessionManager = CreateSessionManager(world, clients);
        var sentMessages = new List<IProtocolMessage>();
        var broadcastAttempts = new List<string>();
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            (_, message) => sentMessages.Add(message),
            static _ => { },
            static _ => { },
            (_, text, _) => broadcastAttempts.Add(text),
            static (_, _) => { },
            static _ => { });

        dispatcher.Dispatch(new ChatSubmitMessage("hello", TeamOnly: false), client.EndPoint);

        Assert.Empty(broadcastAttempts);
        var rejection = Assert.Single(sentMessages);
        var relay = Assert.IsType<ChatRelayMessage>(rejection);
        Assert.Contains("gagged", relay.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServerIncomingMessageDispatcherReturnsDetailsWithoutAllocatingClient()
    {
        var world = new SimulationWorld();
        var clients = new Dictionary<byte, ClientSession>();
        var sessionManager = CreateSessionManager(world, clients);
        var sentMessages = new List<IProtocolMessage>();
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            (_, message) => sentMessages.Add(message),
            static _ => { },
            peer => sentMessages.Add(new ServerDetailsResponseMessage(
                "Test Server",
                "ctf_truefort",
                GameMode: 1,
                PlayerCount: 0,
                MaxPlayerCount: 24,
                SpectatorCount: 0,
                RedScore: 0,
                BlueScore: 0,
                TimeRemainingTicks: 0,
                TimeLimitTicks: 0,
                TickRate: 30,
                [])),
            static (_, _, _) => { },
            static (_, _) => { },
            static _ => { });

        dispatcher.Dispatch(new ServerDetailsRequestMessage(), new IPEndPoint(IPAddress.Loopback, 8190));

        Assert.Empty(clients);
        Assert.IsType<ServerDetailsResponseMessage>(Assert.Single(sentMessages));
    }

    [Fact]
    public void ServerIncomingMessageDispatcherWatchHelloAllocatesSpectatorSlot()
    {
        var world = new SimulationWorld();
        var clients = new Dictionary<byte, ClientSession>();
        var sessionManager = CreateSessionManager(world, clients);
        var sentMessages = new List<IProtocolMessage>();
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 1,
            maxTotalClients: 2,
            maxSpectatorClients: 1,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
                static () => (false, string.Empty, string.Empty),
                (_, message) => sentMessages.Add(message),
                static _ => { },
                static _ => { },
                static (_, _, _) => { },
            static (_, _) => { },
            static _ => { });

        dispatcher.Dispatch(
            new HelloMessage("Watcher", ProtocolVersion.Current, 0, Intent: ConnectionIntent.Watch),
            new IPEndPoint(IPAddress.Loopback, 8190));

        var client = Assert.Single(clients).Value;
        Assert.Equal(SimulationWorld.FirstSpectatorSlot, client.Slot);
        Assert.True(client.IsWatchOnly);
        var welcome = Assert.IsType<WelcomeMessage>(Assert.Single(sentMessages));
        Assert.Equal(SimulationWorld.FirstSpectatorSlot, welcome.PlayerSlot);
        Assert.False(clients.ContainsKey(1));
    }

    [Fact]
    public void ServerSessionManagerRejectsPlayableControlsForWatchOnlyClient()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(SimulationWorld.FirstSpectatorSlot, 101, new IPEndPoint(IPAddress.Loopback, 8190), "Watcher", TimeSpan.Zero)
        {
            IsAuthorized = true,
            IsWatchOnly = true,
        };
        var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
        var sentMessages = new List<IProtocolMessage>();
        var sessionManager = CreateSessionManager(world, clients, (_, message) => sentMessages.Add(message));

        sessionManager.HandleControlCommand(client, new ControlCommandMessage(1, ControlCommandKind.SelectTeam, (byte)PlayerTeam.Red));

        var ack = Assert.IsType<ControlAckMessage>(Assert.Single(sentMessages));
        Assert.False(ack.Accepted);
        Assert.Equal(SimulationWorld.FirstSpectatorSlot, client.Slot);
    }

    [Fact]
    public void ServerBanServicePersistsRejectsAndExpiresBans()
    {
        var root = CreateTempRoot();
        try
        {
            var now = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero);
            var path = Path.Combine(root, "server-bans.json");
            var service = new ServerBanService(path, () => now);

            var banned = service.BanIpAddress(IPAddress.Parse("127.0.0.44"), TimeSpan.FromMinutes(5), "griefing", "Admin");
            Assert.True(banned.Success);

            var denyReason = service.GetConnectionDeniedReason(new IPEndPoint(IPAddress.Parse("127.0.0.44"), 9000));
            Assert.NotNull(denyReason);
            Assert.Contains("griefing", denyReason, StringComparison.OrdinalIgnoreCase);

            var reloaded = new ServerBanService(path, () => now);
            Assert.NotNull(reloaded.GetConnectionDeniedReason(new IPEndPoint(IPAddress.Parse("127.0.0.44"), 9001)));

            now = now.AddMinutes(6);
            Assert.Null(reloaded.GetConnectionDeniedReason(new IPEndPoint(IPAddress.Parse("127.0.0.44"), 9002)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ServerAdminOperationsBanPlayerDisconnectsClientAndSupportsUnban()
    {
        var root = CreateTempRoot();
        try
        {
            var world = new SimulationWorld();
            var client = new ClientSession(1, 101, new IPEndPoint(IPAddress.Parse("127.0.0.44"), 8190), "Tester", TimeSpan.Zero)
            {
                IsAuthorized = true,
            };
            var clients = new Dictionary<byte, ClientSession> { [client.Slot] = client };
            var sessionManager = CreateSessionManager(world, clients);
            var sentMessages = new List<IProtocolMessage>();
            var banService = new ServerBanService(Path.Combine(root, "server-bans.json"), () => new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero));
            var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
            var operations = new ServerAdminOperations(
                static _ => { },
                (_, message) => sentMessages.Add(message),
                () => clients,
                () => sessionManager,
                () => world,
                static () => null,
                () => new MapRotationManager(world, requestedMap: null, mapRotationFile: null, stockMapRotation: [], static _ => { }),
                () => new SnapshotBroadcaster(world, new SimulationConfig(), clients, maxPlayableClients: 24, botManager, transientEventReplayTicks: 0, new ServerMapMetadataResolver(world), static (_, _, _) => { }),
                () => botManager,
                banService: banService);

            var banResult = operations.TryBanPlayer(client.Slot, TimeSpan.FromMinutes(15), "griefing");
            Assert.True(banResult.Success);
            Assert.DoesNotContain(client.Slot, clients.Keys);
            Assert.NotNull(banService.GetConnectionDeniedReason(new IPEndPoint(IPAddress.Parse("127.0.0.44"), 9000)));
            Assert.Contains(sentMessages, message => message is ConnectionDeniedMessage denied && denied.Reason.Contains("banned", StringComparison.OrdinalIgnoreCase));

            var unbanResult = operations.TryUnbanIpAddress("127.0.0.44");
            Assert.True(unbanResult.Success);
            Assert.Null(banService.GetConnectionDeniedReason(new IPEndPoint(IPAddress.Parse("127.0.0.44"), 9001)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ServerIncomingMessageDispatcherRejectsHelloFromBannedIp()
    {
        var root = CreateTempRoot();
        try
        {
            var world = new SimulationWorld();
            var clients = new Dictionary<byte, ClientSession>();
            var sessionManager = CreateSessionManager(world, clients);
            var sentMessages = new List<IProtocolMessage>();
            var banService = new ServerBanService(Path.Combine(root, "server-bans.json"), () => new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero));
            Assert.True(banService.BanIpAddress("127.0.0.66", TimeSpan.FromMinutes(30), "abuse", "Admin").Success);
            var dispatcher = new ServerIncomingMessageDispatcher(
                new SimulationConfig(),
                "Test Server",
                passwordRequired: false,
                maxPlayableClients: 24,
                maxTotalClients: 32,
                maxSpectatorClients: 8,
                clients,
                sessionManager,
                world,
                () => TimeSpan.Zero,
                static () => null,
                static () => 999,
                static _ => null,
                static _ => { },
            static () => (false, string.Empty, string.Empty),
            (_, message) => sentMessages.Add(message),
            static _ => { },
            static _ => { },
            static (_, _, _) => { },
                static (_, _) => { },
                static _ => { },
                banService: banService);

            dispatcher.Dispatch(new HelloMessage("Banned", ProtocolVersion.Current, 0), new IPEndPoint(IPAddress.Parse("127.0.0.66"), 8190));

            var denial = Assert.Single(sentMessages);
            var message = Assert.IsType<ConnectionDeniedMessage>(denial);
            Assert.Contains("banned", message.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(clients);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ServerIncomingPacketPumpReturnsAfterBudgetWhenTransportStaysBacklogged()
    {
        var world = new SimulationWorld();
        var clients = new Dictionary<byte, ClientSession>();
        var sessionManager = CreateSessionManager(world, clients);
        var transport = new AlwaysBackloggedServerTransport(new ServerStatusRequestMessage());
        var dispatcher = new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            static (_, _) => { },
            static _ => { },
            static _ => { },
            static (_, _, _) => { },
            static (_, _) => { },
            static _ => { });
        var pump = new ServerIncomingPacketPump(transport, dispatcher, wsaConnReset: 10054, static _ => { });

        pump.PumpAvailablePackets();

        Assert.Equal(ServerIncomingPacketPump.MaxPacketsPerPump, transport.ReceiveCount);
    }

    [Fact]
    public void ServerIncomingPacketPumpReturnsAfterBudgetWhenBackloggedTransportKeepsResetting()
    {
        var transport = new AlwaysResettingServerTransport(wsaConnReset: 10054);
        var dispatcher = CreateNoOpMessageDispatcher();
        var pump = new ServerIncomingPacketPump(transport, dispatcher, wsaConnReset: 10054, static _ => { });

        pump.PumpAvailablePackets();

        Assert.Equal(ServerIncomingPacketPump.MaxPacketsPerPump, transport.ReceiveCount);
    }

    [Fact]
    public void ServerBotManagerRejectsLocalAndReservedSlots()
    {
        var world = new SimulationWorld();
        var reservedSlots = new HashSet<byte> { 2 };
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController(),
            slot => !reservedSlots.Contains(slot));

        Assert.False(botManager.TryAddBot(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red, PlayerClass.Soldier, "Local Bot"));
        Assert.False(botManager.TryAddBot(2, PlayerTeam.Blue, PlayerClass.Soldier, "Reserved Bot"));

        Assert.True(botManager.TryAddBot(3, PlayerTeam.Red, PlayerClass.Soldier, "Open Bot"));
        var slot = Assert.Single(botManager.BotSlots);
        Assert.Equal(3, slot.Key);
    }

    [Fact]
    public void ServerBotManagerAppliesRequestedTeamAndClassToWorldPlayer()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Red, PlayerClass.Pyro, "Pyro Bot"));
        Assert.True(world.TryGetNetworkPlayer(2, out var bot));
        Assert.Equal(PlayerTeam.Red, bot.Team);
        Assert.Equal(PlayerClass.Pyro, bot.ClassId);
        Assert.Equal(PlayerClass.Pyro, botManager.BotSlots[2].ClassId);

        Assert.True(botManager.TrySetBotClass(2, PlayerClass.Demoman));
        Assert.Equal(PlayerClass.Demoman, bot.ClassId);
        Assert.Equal(PlayerClass.Demoman, botManager.BotSlots[2].ClassId);

        Assert.True(botManager.TrySetBotTeam(2, PlayerTeam.Blue));
        Assert.Equal(PlayerTeam.Blue, bot.Team);
        Assert.Equal(PlayerTeam.Blue, botManager.BotSlots[2].Team);
    }

    [Fact]
    public void ServerBotManagerFillSkipsReservedClientSlots()
    {
        var world = new SimulationWorld();
        var reservedSlots = new HashSet<byte> { 2, 3 };
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController(),
            slot => !reservedSlots.Contains(slot));

        var added = botManager.TryFillTeam(PlayerTeam.Blue, targetCount: 2, PlayerClass.Soldier);

        Assert.Equal(2, added);
        Assert.DoesNotContain((byte)2, botManager.BotSlots.Keys);
        Assert.DoesNotContain((byte)3, botManager.BotSlots.Keys);
        Assert.Equal(new byte[] { 4, 5 }, botManager.BotSlots.Keys.OrderBy(static slot => slot).ToArray());
    }

    [Fact]
    public void ServerBotManagerFillCyclesClassesWhenClassIsNotSpecified()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        var added = botManager.TryFillTeam(PlayerTeam.Blue, targetCount: 4, requestedClass: null);

        Assert.Equal(4, added);
        var classes = botManager.BotSlots
            .OrderBy(static entry => entry.Key)
            .Select(static entry => entry.Value.ClassId)
            .ToArray();
        Assert.Equal(
            new[] { PlayerClass.Scout, PlayerClass.Pyro, PlayerClass.Soldier, PlayerClass.Heavy },
            classes);
    }

    [Fact]
    public void ServerBotManagerFillUsesExplicitClassWhenSpecified()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        var added = botManager.FillBots(targetPerTeam: 2, PlayerClass.Medic);

        Assert.Equal(4, added);
        Assert.All(botManager.BotSlots.Values, state => Assert.Equal(PlayerClass.Medic, state.ClassId));
    }

    [Fact]
    public void ServerSlotAllocatorSkipsServerBotSlots()
    {
        var clients = new Dictionary<byte, ClientSession>();
        var botSlots = new HashSet<byte> { 1, 2 };

        var slot = global::ServerHelpers.FindAvailableSlot(
            clients,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            maxPlayableClients: 24,
            isPlayableSlotAvailable: candidate => !botSlots.Contains(candidate));

        Assert.Equal(3, slot);
    }

    [Fact]
    public void ServerSessionManagerDoesNotMoveSpectatorIntoServerBotSlot()
    {
        var world = new SimulationWorld();
        var clients = new Dictionary<byte, ClientSession>();
        var sentMessages = new List<IProtocolMessage>();
        var spectator = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 1,
            ServerTransportPeer.FromUdpEndPoint(new IPEndPoint(IPAddress.Loopback, 9001)),
            "Spectator",
            TimeSpan.Zero)
        {
            IsAuthorized = true,
        };
        clients[spectator.Slot] = spectator;
        var botSlots = new HashSet<byte> { 1 };
        var sessionManager = CreateSessionManager(
            world,
            clients,
            (_, message) => sentMessages.Add(message),
            slot => !botSlots.Contains(slot));

        Assert.True(sessionManager.TrySetClientTeam(spectator.Slot, PlayerTeam.Blue));

        Assert.Equal(2, spectator.Slot);
        Assert.False(clients.ContainsKey(1));
        Assert.True(clients.ContainsKey(2));
    }

    [Fact]
    public async Task ServerBotManagerFillDoesNotLoopForeverWhenAvailabilityChangesBetweenChecks()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController(),
            _ =>
            {
                return false;
            });

        var fillTask = Task.Factory.StartNew(
            () => botManager.TryFillTeam(PlayerTeam.Blue, targetCount: 1, PlayerClass.Soldier),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var completedTask = await Task.WhenAny(fillTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(fillTask, completedTask);
        Assert.Equal(0, await fillTask);
        Assert.Empty(botManager.BotSlots);
    }

    [Fact]
    public async Task ServerBotManagerFillBothTeamsDoesNotLoopForeverWhenAvailabilityChangesBetweenChecks()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController(),
            _ =>
            {
                return false;
            });

        var fillTask = Task.Factory.StartNew(
            () => botManager.FillBots(targetPerTeam: 1, PlayerClass.Soldier),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var completedTask = await Task.WhenAny(fillTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(fillTask, completedTask);
        Assert.Equal(0, await fillTask);
        Assert.Empty(botManager.BotSlots);
    }

    [Fact]
    public async Task ServerBotManagerFirstBotThinkCompletesPromptlyAfterAdd()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Blue, PlayerClass.Soldier, "Blue Bot"));

        var thinkTask = Task.Run(() => botManager.FeedBotInputsBeforeSimulationAdvance());
        var completedTask = await Task.WhenAny(thinkTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(thinkTask, completedTask);
        await thinkTask;
    }

    [Fact]
    public async Task ServerBotManagerHarvestMixedTeamBotThinkDoesNotColdBuildSynchronously()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Harvest"));
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Red, PlayerClass.Scout, "Friendly Bot"));
        Assert.True(botManager.TryAddBot(3, PlayerTeam.Blue, PlayerClass.Heavy, "Enemy Bot"));

        var thinkTask = Task.Run(() => botManager.FeedBotInputsBeforeSimulationAdvance());
        var completedTask = await Task.WhenAny(thinkTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(thinkTask, completedTask);
        await thinkTask;
    }

    [Fact]
    public void ServerBotManagerAutoNamesBotsFromPracticeNamePool()
    {
        var practiceNames = PracticeBotDisplayNamePool.LoadDefaultNames();
        Assert.True(practiceNames.Count >= 2);

        var world = new SimulationWorld();
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController(),
            botDisplayNamePool: new PracticeBotDisplayNamePool(practiceNames, shuffleNames: false));

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Blue, PlayerClass.Soldier, string.Empty));
        Assert.Equal(TrimExpectedPlayerDisplayName(practiceNames[0]), botManager.BotSlots[2].DisplayName);

        Assert.True(botManager.TryAddBot(3, PlayerTeam.Red, PlayerClass.Soldier, string.Empty));
        Assert.Equal(TrimExpectedPlayerDisplayName(practiceNames[1]), botManager.BotSlots[3].DisplayName);
    }

    [Fact]
    public async Task ServerBotManagerSecondBotThinkCompletesPromptlyAfterFirstSimulationTick()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        Assert.Equal(2, botManager.FillBots(targetPerTeam: 1, PlayerClass.Soldier));

        botManager.FeedBotInputsBeforeSimulationAdvance();
        world.AdvanceOneTick();
        botManager.AdvanceBotReactions();

        var thinkTask = Task.Run(() =>
        {
            botManager.FeedBotInputsBeforeSimulationAdvance();
        });
        var completedTask = await Task.WhenAny(thinkTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(thinkTask, completedTask);
        await thinkTask;
    }

    [Fact]
    public async Task ServerBotManagerSecondSimulationTickCompletesPromptlyAfterFilledRosterThink()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        Assert.Equal(2, botManager.FillBots(targetPerTeam: 1, PlayerClass.Soldier));

        botManager.FeedBotInputsBeforeSimulationAdvance();
        world.AdvanceOneTick();
        botManager.AdvanceBotReactions();
        botManager.FeedBotInputsBeforeSimulationAdvance();

        var tickTask = Task.Run(() =>
        {
            world.AdvanceOneTick();
            botManager.AdvanceBotReactions();
        });
        var completedTask = await Task.WhenAny(tickTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(tickTask, completedTask);
        await tickTask;
    }

    [Fact]
    public async Task ServerBotManagerFilledRosterTickSequenceCompletesPromptly()
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        Assert.Equal(2, botManager.FillBots(targetPerTeam: 1, PlayerClass.Soldier));

        const int tickCount = 8;
        var currentTick = -1;
        var currentPhase = "starting";
        var tickTask = Task.Run(() =>
        {
            for (var index = 0; index < tickCount; index += 1)
            {
                currentTick = index;
                currentPhase = "feed";
                botManager.FeedBotInputsBeforeSimulationAdvance();
                currentPhase = "advance";
                world.AdvanceOneTick();
                currentPhase = "reactions";
                botManager.AdvanceBotReactions();
            }

            currentPhase = "done";
        });
        var completedTask = await Task.WhenAny(tickTask, Task.Delay(TimeSpan.FromSeconds(3)));

        Assert.True(
            ReferenceEquals(tickTask, completedTask),
            $"Timed out during tick={currentTick} phase={currentPhase}.");
        await tickTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServerBotManagerSingleTeamTickSequenceCompletesPromptly(bool fillViaHelper)
    {
        var world = new SimulationWorld();
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());

        if (fillViaHelper)
        {
            Assert.Equal(1, botManager.TryFillTeam(PlayerTeam.Blue, targetCount: 1, PlayerClass.Soldier));
        }
        else
        {
            Assert.True(botManager.TryAddBot(2, PlayerTeam.Blue, PlayerClass.Soldier, "Blue Bot"));
        }

        const int tickCount = 8;
        var currentTick = -1;
        var currentPhase = "starting";
        var tickTask = Task.Run(() =>
        {
            for (var index = 0; index < tickCount; index += 1)
            {
                currentTick = index;
                currentPhase = "feed";
                botManager.FeedBotInputsBeforeSimulationAdvance();
                currentPhase = "advance";
                world.AdvanceOneTick();
                currentPhase = "reactions";
                botManager.AdvanceBotReactions();
            }

            currentPhase = "done";
        });
        var completedTask = await Task.WhenAny(tickTask, Task.Delay(TimeSpan.FromSeconds(3)));

        Assert.True(
            ReferenceEquals(tickTask, completedTask),
            $"Timed out during tick={currentTick} phase={currentPhase} fillViaHelper={fillViaHelper}.");
        await tickTask;
    }

    [Fact]
    public async Task ServerBotManagerShippedNavigationTickSequenceCompletesPromptly()
    {
        var world = new SimulationWorld();
        Assert.True(world.TryLoadLevel("Truefort"));

        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        Assert.Equal(2, botManager.FillBots(targetPerTeam: 1, PlayerClass.Soldier));

        const int tickCount = 8;
        var currentTick = -1;
        var currentPhase = "starting";
        var maxFeedMilliseconds = 0d;
        var maxFeedTick = -1;
        var maxAdvanceMilliseconds = 0d;
        var maxReactionMilliseconds = 0d;
        var tickTask = Task.Run(() =>
        {
            for (var index = 0; index < tickCount; index += 1)
            {
                currentTick = index;
                currentPhase = "feed";
                var stepStopwatch = Stopwatch.StartNew();
                botManager.FeedBotInputsBeforeSimulationAdvance();
                if (stepStopwatch.Elapsed.TotalMilliseconds > maxFeedMilliseconds)
                {
                    maxFeedMilliseconds = stepStopwatch.Elapsed.TotalMilliseconds;
                    maxFeedTick = index;
                }
                currentPhase = "advance";
                stepStopwatch.Restart();
                world.AdvanceOneTick();
                maxAdvanceMilliseconds = Math.Max(maxAdvanceMilliseconds, stepStopwatch.Elapsed.TotalMilliseconds);
                currentPhase = "reactions";
                stepStopwatch.Restart();
                botManager.AdvanceBotReactions();
                maxReactionMilliseconds = Math.Max(maxReactionMilliseconds, stepStopwatch.Elapsed.TotalMilliseconds);
            }

            currentPhase = "done";
        });
        var completedTask = await Task.WhenAny(tickTask, Task.Delay(TimeSpan.FromSeconds(15)));

        Assert.True(
            ReferenceEquals(tickTask, completedTask),
            $"Timed out during tick={currentTick} phase={currentPhase} on shipped nav map.");
        await tickTask;
        Assert.True(
            maxFeedMilliseconds < 500d,
            $"Shipped-nav feed spike too high: tick={maxFeedTick} feed={maxFeedMilliseconds:0.0}ms advance={maxAdvanceMilliseconds:0.0}ms reactions={maxReactionMilliseconds:0.0}ms.");
    }

    [Fact]
    public void SnapshotBroadcasterKeepsServerBotRosterDeltasWithinRemoteBudget()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Parse("203.0.113.10"), 8190),
            "Tester",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();
        var baseline = Assert.Single(sentSnapshots).Message;
        client.AcknowledgeSnapshot(baseline.Frame);
        sentSnapshots.Clear();

        Assert.Equal(19, botManager.FillBots(targetPerTeam: 10, PlayerClass.Soldier));
        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var sent = Assert.Single(sentSnapshots);
        Assert.True(sent.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.True(sent.Message.IsDelta);
        Assert.InRange(sent.Message.Players.Count, 1, 19);
        var merged = SnapshotDelta.ToFullSnapshot(sent.Message, baseline);
        Assert.InRange(merged.Players.Count, 2, 20);
        client.AcknowledgeSnapshot(sent.Message.Frame);
        sentSnapshots.Clear();

        SnapshotBaselineState? latestBaseline = null;
        for (var index = 0; index < 40; index += 1)
        {
            world.AdvanceOneTick();
            broadcaster.BroadcastSnapshot();
            var next = Assert.Single(sentSnapshots);
            Assert.True(next.Payload.Length <= SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
            client.AcknowledgeSnapshot(next.Message.Frame);
            Assert.True(client.TryGetSnapshotState(client.LastAcknowledgedSnapshotFrame, out latestBaseline));
            sentSnapshots.Clear();

            if (latestBaseline.Players.Count >= 20)
            {
                break;
            }
        }

        Assert.NotNull(latestBaseline);
        Assert.Equal(20, latestBaseline.Players.Count);
        Assert.Contains(latestBaseline.Players, player => player.Slot == SimulationWorld.FirstSpectatorSlot);
        for (var slot = 2; slot <= 20; slot += 1)
        {
            Assert.Contains(latestBaseline.Players, player => player.Slot == slot);
        }
    }

    [Fact]
    public void SnapshotBroadcasterSkipsCanonicalDemoCaptureWhileRecorderIsIdle()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var canonicalSnapshots = new List<SnapshotMessage>();
        var shouldRecordCanonicalSnapshot = false;
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)),
            recordCanonicalSnapshot: snapshot => canonicalSnapshots.Add(snapshot),
            shouldRecordCanonicalSnapshot: () => shouldRecordCanonicalSnapshot);

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        Assert.Single(sentSnapshots);
        Assert.Empty(canonicalSnapshots);

        shouldRecordCanonicalSnapshot = true;
        sentSnapshots.Clear();
        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        Assert.Single(sentSnapshots);
        Assert.Single(canonicalSnapshots);
    }

    [Fact]
    public void SnapshotBroadcasterPrefersAckBasedDeltaEvenWhenFullSnapshotWouldFit()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var initial = Assert.Single(sentSnapshots);
        Assert.False(initial.Message.IsDelta);
        client.AcknowledgeSnapshot(initial.Message.Frame);
        sentSnapshots.Clear();

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var next = Assert.Single(sentSnapshots);
        Assert.True(next.Message.IsDelta);
        Assert.Equal(initial.Message.Frame, next.Message.BaselineFrame);
        Assert.True(next.Payload.Length < initial.Payload.Length);
    }

    [Fact]
    public void SnapshotBroadcasterSendsCompleteInitialRemoteSnapshotEvenWhenItExceedsSteadyStateUdpBudget()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Parse("203.0.113.10"), 8190),
            "RemoteTester",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        Assert.Equal(19, botManager.FillBots(targetPerTeam: 10, PlayerClass.Soldier));

        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var initial = Assert.Single(sentSnapshots);
        Assert.False(initial.Message.IsDelta);
        Assert.True(ProtocolCodec.MeasureSerializedSize(initial.Message) > SnapshotDeltaBudgeter.TargetSnapshotPayloadBytes);
        Assert.Equal(20, initial.Message.Players.Count);
        Assert.Contains(initial.Message.Players, player => player.Slot == SimulationWorld.FirstSpectatorSlot);
        for (var slot = 2; slot <= 20; slot += 1)
        {
            Assert.Contains(initial.Message.Players, player => player.Slot == slot);
        }
    }

    [Fact]
    public void SnapshotBroadcasterOmitsCloakedSpyBehindEnemyViewer()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            slot: 2,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "EnemyViewer",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        Assert.True(world.TrySetNetworkPlayerName(2, "EnemyViewer"));
        Assert.True(world.TrySetNetworkPlayerName(3, "HiddenSpy"));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Spy));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red));
        Assert.True(world.TryGetNetworkPlayer(2, out var viewer));
        Assert.True(world.TryGetNetworkPlayer(3, out var spy));

        spy.Spawn(PlayerTeam.Red, 40f, 0f);
        Assert.True(spy.TryToggleSpyCloak());

        viewer.Spawn(PlayerTeam.Blue, 50f, 0f);
        var viewerInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 60f,
            AimWorldY: 0f,
            DebugKill: false);
        Assert.True(world.TrySetNetworkPlayerInput(client.Slot, viewerInput));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var sent = Assert.Single(sentSnapshots);
        Assert.DoesNotContain(sent.Message.Players, player => player.Slot == 3);
    }

    [Fact]
    public void SnapshotBroadcasterOmitsUncloakedSpyBehindEnemyViewer()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            slot: 2,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "EnemyViewer",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        Assert.True(world.TrySetNetworkPlayerName(2, "EnemyViewer"));
        Assert.True(world.TrySetNetworkPlayerName(3, "HiddenSpy"));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Spy));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red));
        Assert.True(world.TryGetNetworkPlayer(2, out var viewer));
        Assert.True(world.TryGetNetworkPlayer(3, out var spy));

        spy.Spawn(PlayerTeam.Red, 40f, 0f);
        Assert.False(spy.IsSpyCloaked);
        Assert.Equal(1f, spy.SpyCloakAlpha);

        viewer.Spawn(PlayerTeam.Blue, 50f, 0f);
        var viewerInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 60f,
            AimWorldY: 0f,
            DebugKill: false);
        Assert.True(world.TrySetNetworkPlayerInput(client.Slot, viewerInput));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var sent = Assert.Single(sentSnapshots);
        Assert.DoesNotContain(sent.Message.Players, player => player.Slot == 3);
    }

    [Fact]
    public void SnapshotBroadcasterOmitsBackstabAnimatingSpyBehindEnemyViewer()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            slot: 2,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "EnemyViewer",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        Assert.True(world.TrySetNetworkPlayerName(2, "EnemyViewer"));
        Assert.True(world.TrySetNetworkPlayerName(3, "BackstabSpy"));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(2, PlayerClass.Soldier));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(3, PlayerClass.Spy));
        Assert.True(world.TrySetNetworkPlayerTeam(2, PlayerTeam.Blue));
        Assert.True(world.TrySetNetworkPlayerTeam(3, PlayerTeam.Red));
        Assert.True(world.TryGetNetworkPlayer(2, out var viewer));
        Assert.True(world.TryGetNetworkPlayer(3, out var spy));

        spy.Spawn(PlayerTeam.Red, 40f, 0f);
        Assert.True(spy.TryToggleSpyCloak());
        Assert.True(spy.TryStartSpyBackstab(0f));

        viewer.Spawn(PlayerTeam.Blue, 50f, 0f);
        var viewerInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: 60f,
            AimWorldY: 0f,
            DebugKill: false);
        Assert.True(world.TrySetNetworkPlayerInput(client.Slot, viewerInput));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var sent = Assert.Single(sentSnapshots);
        Assert.DoesNotContain(sent.Message.Players, player => player.Slot == 3);
    }

    [Fact]
    public void SnapshotBroadcasterSerializesServerBotClassIndependentOfLocalPlayerClass()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Demoman));
        var client = new ClientSession(
            SimulationWorld.LocalPlayerSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(
            world,
            new SimulationConfig(),
            new BotBrainPracticeBotController(),
            slot => !clients.ContainsKey(slot));
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        Assert.True(botManager.TryAddBot(2, PlayerTeam.Blue, PlayerClass.Soldier, "Soldier Bot"));
        SnapshotBaselineState? baseline = null;
        for (var index = 0; index < 8; index += 1)
        {
            world.AdvanceOneTick();
            broadcaster.BroadcastSnapshot();

            var sent = Assert.Single(sentSnapshots);
            var resolved = SnapshotDelta.ToFullSnapshot(sent.Message, baseline);
            var localPlayer = Assert.Single(resolved.Players, player => player.Slot == SimulationWorld.LocalPlayerSlot);
            Assert.Equal((byte)PlayerClass.Demoman, localPlayer.ClassId);

            var botPlayer = resolved.Players.FirstOrDefault(player => player.Slot == 2);
            if (botPlayer is not null)
            {
                Assert.Equal((byte)PlayerClass.Soldier, botPlayer.ClassId);
                Assert.Equal("Soldier Bot", botPlayer.Name);
                return;
            }

            baseline = SnapshotBaselineState.FromSnapshot(resolved);
            client.AcknowledgeSnapshot(sent.Message.Frame);
            sentSnapshots.Clear();
        }

        Assert.Fail("Server bot did not enter the client snapshot baseline.");
    }

    [Fact]
    public void SnapshotBroadcasterResendsStringCacheUpdatesAfterClientSlotStateIsCleared()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "TesterA",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var botManager = new ServerBotManager(world, new SimulationConfig(), new BotBrainPracticeBotController());
        Assert.True(botManager.TryAddBot(2, PlayerTeam.Blue, PlayerClass.Soldier, "Soldier Bot"));
        var sentSnapshots = new List<(SnapshotMessage Message, byte[] Payload)>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            new SimulationConfig(),
            clients,
            maxPlayableClients: 24,
            botManager,
            transientEventReplayTicks: 0,
            new ServerMapMetadataResolver(world),
            (_, message, payload) => sentSnapshots.Add((message, payload)));

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var firstSnapshot = Assert.Single(sentSnapshots).Message;
        Assert.NotNull(firstSnapshot.StringCacheUpdates);
        Assert.NotEmpty(firstSnapshot.StringCacheUpdates!);

        broadcaster.RemoveClientState(client.Slot);
        sentSnapshots.Clear();
        clients[client.Slot] = new ClientSession(
            client.Slot,
            userId: 102,
            new IPEndPoint(IPAddress.Loopback, 8191),
            "TesterB",
            TimeSpan.Zero);

        world.AdvanceOneTick();
        broadcaster.BroadcastSnapshot();

        var secondSnapshot = Assert.Single(sentSnapshots).Message;
        Assert.NotNull(secondSnapshot.StringCacheUpdates);
        Assert.NotEmpty(secondSnapshot.StringCacheUpdates!);
    }

    [Fact]
    public void ServerSessionManagerInvokesSlotChangeCallbackWhenMovingClient()
    {
        var world = new SimulationWorld();
        var client = new ClientSession(
            SimulationWorld.FirstSpectatorSlot,
            userId: 101,
            new IPEndPoint(IPAddress.Loopback, 8190),
            "Tester",
            TimeSpan.Zero);
        var clients = new Dictionary<byte, ClientSession>
        {
            [client.Slot] = client,
        };
        var slotChanges = new List<(byte OldSlot, byte NewSlot)>();
        var sessionManager = new ServerSessionManager(
            world,
            clients,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            nowProvider: () => TimeSpan.Zero,
            serverPassword: null,
            passwordRequired: false,
            clientTimeoutSeconds: 20,
            passwordTimeoutSeconds: 20,
            passwordRetrySeconds: 5,
            getPasswordRateLimitReason: static _ => null,
            recordPasswordFailure: static _ => { },
            clearPasswordFailures: static _ => { },
            sendMessage: static (_, _) => { },
            log: static _ => { },
            isPlayableSlotAvailable: static _ => true,
            clientSlotChanged: (oldSlot, newSlot) => slotChanges.Add((oldSlot, newSlot)));

        Assert.True(sessionManager.TrySetClientTeam(client.Slot, PlayerTeam.Red));
        var slotChange = Assert.Single(slotChanges);
        Assert.Equal(SimulationWorld.FirstSpectatorSlot, slotChange.OldSlot);
        Assert.Equal(client.Slot, slotChange.NewSlot);
        Assert.Contains(slotChange.NewSlot, SimulationWorld.NetworkPlayerSlots);
    }

    private static string TrimExpectedPlayerDisplayName(string displayName)
    {
        return displayName.Length <= 20 ? displayName : displayName[..20];
    }

    private static OpenGarrisonServerCommandContext CreateCommandContext(OpenGarrisonServerAdminIdentity identity)
    {
        return new OpenGarrisonServerCommandContext(
            new FakeServerReadOnlyState(),
            new FakeServerAdminOperations(),
            new FakeServerCvarRegistry(),
            new FakeServerScheduler(),
            identity,
            OpenGarrisonServerCommandSource.PrivateChat);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "OpenGarrison.PluginHost.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenGarrison.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Failed to locate repository root from test output directory.");
    }

    private static ServerSessionManager CreateSessionManager(
        SimulationWorld world,
        Dictionary<byte, ClientSession> clients,
        Action<ServerTransportPeer, IProtocolMessage>? sendMessage = null,
        Func<byte, bool>? isPlayableSlotAvailable = null)
    {
        return new ServerSessionManager(
            world,
            clients,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            nowProvider: () => TimeSpan.Zero,
            serverPassword: null,
            passwordRequired: false,
            clientTimeoutSeconds: 20,
            passwordTimeoutSeconds: 20,
            passwordRetrySeconds: 5,
            getPasswordRateLimitReason: static _ => null,
            recordPasswordFailure: static _ => { },
            clearPasswordFailures: static _ => { },
            sendMessage: sendMessage ?? ((_, _) => { }),
            log: static _ => { },
            isPlayableSlotAvailable: isPlayableSlotAvailable);
    }

    private static ServerIncomingMessageDispatcher CreateNoOpMessageDispatcher()
    {
        var world = new SimulationWorld();
        var clients = new Dictionary<byte, ClientSession>();
        var sessionManager = CreateSessionManager(world, clients);
        return new ServerIncomingMessageDispatcher(
            new SimulationConfig(),
            "Test Server",
            passwordRequired: false,
            maxPlayableClients: 24,
            maxTotalClients: 32,
            maxSpectatorClients: 8,
            clients,
            sessionManager,
            world,
            () => TimeSpan.Zero,
            static () => null,
            static () => 999,
            static _ => null,
            static _ => { },
            static () => (false, string.Empty, string.Empty),
            static (_, _) => { },
            static _ => { },
            static _ => { },
            static (_, _, _) => { },
            static (_, _) => { },
            static _ => { });
    }

    private static OpenGarrisonServerPlayerInfo CreatePlayerInfo(
        byte slot,
        int userId,
        string name,
        bool isSpectator = false,
        bool isAuthorized = true,
        bool isAlive = false,
        int? playerId = null,
        PlayerTeam? team = null,
        PlayerClass? playerClass = null)
    {
        return new OpenGarrisonServerPlayerInfo(
            slot,
            userId,
            name,
            IsSpectator: isSpectator,
            IsAuthorized: isAuthorized,
            IsGagged: false,
            IsAlive: isAlive,
            PlayerId: playerId,
            Team: team,
            PlayerClass: playerClass,
            PlayerScale: 1f,
            EndPoint: $"127.0.0.1:{8190 + slot}",
            GameplayLoadoutId: string.Empty,
            GameplaySecondaryItemId: string.Empty,
            GameplayAcquiredItemId: string.Empty,
            GameplayEquippedSlot: GameplayEquipmentSlot.Primary,
            GameplayEquippedItemId: string.Empty);
    }

    private sealed class FakeServerReadOnlyState : IOpenGarrisonServerReadOnlyState
    {
        public string ServerName => "test";
        public string LevelName => "ctf_test";
        public int MapAreaIndex => 1;
        public int MapAreaCount => 1;
        public float MapScale => 1f;
        public GameModeKind GameMode => GameModeKind.CaptureTheFlag;
        public MatchPhase MatchPhase => MatchPhase.Running;
        public int RedCaps => 0;
        public int BlueCaps => 0;

        public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetGameplayAbilities() => [];

        public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetPlayerGameplayAbilities(int playerId) => [];

        public bool TryGetPlayerGameplayAbility(int playerId, string category, out OpenGarrisonServerGameplayAbilityInfo ability)
        {
            ability = default;
            return false;
        }

        public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId) => [];

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot) => [];

        public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot) => [];

        public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
        {
            value = false;
            return false;
        }

        public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
        {
            value = 0;
            return false;
        }

        public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
        {
            value = 0f;
            return false;
        }

        public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
        {
            value = false;
            return false;
        }
    }

    private sealed class AlwaysResettingServerTransport(int wsaConnReset) : IServerMessageTransport
    {
        public int ReceiveCount { get; private set; }

        public bool HasPendingMessages => true;

        public ServerMessagePacket Receive()
        {
            ReceiveCount += 1;
            throw new SocketException(wsaConnReset);
        }

        public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
        {
            _ = messageType;
        }
    }

    private sealed class AlwaysBackloggedServerTransport(IProtocolMessage message) : IServerMessageTransport
    {
        private readonly byte[] _payload = ProtocolCodec.Serialize(message);

        public int ReceiveCount { get; private set; }

        public bool HasPendingMessages => true;

        public ServerMessagePacket Receive()
        {
            ReceiveCount += 1;
            return new ServerMessagePacket(
                ServerTransportPeer.FromUdpEndPoint(new IPEndPoint(IPAddress.Loopback, 8190 + ReceiveCount)),
                _payload);
        }

        public void Send(ServerTransportPeer remotePeer, byte[] payload, MessageType? messageType = null)
        {
            _ = messageType;
        }
    }

    private sealed class FakeServerAdminOperations : IOpenGarrisonServerAdminOperations
    {
        public List<(byte Slot, string Text)> SystemMessages { get; } = [];

        public void BroadcastSystemMessage(string text)
        {
        }

        public void SendSystemMessage(byte slot, string text)
        {
            SystemMessages.Add((slot, text));
        }

        public bool TryRenamePlayer(byte slot, string newName) => true;

        public bool TryDisconnect(byte slot, string reason) => true;

        public OpenGarrisonServerBanActionResult TryBanPlayer(byte slot, TimeSpan? duration, string reason) => new(true, "127.0.0.1", string.Empty, !duration.HasValue, 0);

        public OpenGarrisonServerBanActionResult TryBanIpAddress(string ipAddress, TimeSpan? duration, string reason) => new(true, ipAddress, string.Empty, !duration.HasValue, 0);

        public OpenGarrisonServerAddressActionResult TryUnbanIpAddress(string ipAddress) => new(true, ipAddress, string.Empty);

        public bool TrySetPlayerGagged(byte slot, bool isGagged) => true;

        public bool TryMoveToSpectator(byte slot) => true;

        public bool TrySetTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetClass(byte slot, PlayerClass playerClass) => true;

        public bool TrySetGameplayLoadout(byte slot, string loadoutId) => true;

        public bool TrySetGameplaySecondaryItem(byte slot, string? itemId) => true;

        public bool TrySetGameplayAcquiredItem(byte slot, string? itemId) => true;

        public bool TryGrantGameplayItem(byte slot, string itemId) => true;

        public bool TryRevokeGameplayItem(byte slot, string itemId) => true;

        public bool TrySetGameplayEquippedSlot(byte slot, GameplayEquipmentSlot equippedSlot) => true;

        public bool TryForceKill(byte slot) => true;

        public bool TryIgnitePlayer(byte slot, float durationSeconds) => true;

        public bool TrySetPlayerScale(byte slot, float scale) => true;

        public bool TrySetPlayerMovementSpeedScale(byte slot, float scale) => true;

        public bool TryClearPlayerMovementSpeedScale(byte slot) => true;

        public bool TrySetPlayerGravityScale(byte slot, float scale) => true;

        public bool TryClearPlayerGravityScale(byte slot) => true;

        public bool TrySetTimeLimit(int timeLimitMinutes) => true;

        public bool TrySetCapLimit(int capLimit) => true;

        public bool TrySetRespawnSeconds(int respawnSeconds) => true;

        public bool TryChangeMap(string levelName, int mapAreaIndex = 1, bool preservePlayerStats = false) => true;

        public bool TrySetNextRoundMap(string levelName, int mapAreaIndex = 1) => true;

        public bool TryAddBot(byte slot, PlayerTeam team, PlayerClass playerClass, string displayName) => true;

        public bool TryRemoveBot(byte slot) => true;

        public bool TrySetBotTeam(byte slot, PlayerTeam team) => true;

        public bool TrySetBotClass(byte slot, PlayerClass playerClass) => true;

        public int TryFillBots(int targetPerTeam, PlayerClass? requestedClass) => 0;

        public int TryFillBotTeam(PlayerTeam team, int targetCount, PlayerClass? requestedClass) => 0;

        public IReadOnlyList<OpenGarrisonServerBotSlotInfo> GetBotSlots() => [];

        public int TryClearAllBots() => 0;

        public string GetDemoRecordingStatus() => "[server] demo | status=idle";

        public OpenGarrisonServerDemoRecordingResult TryStartDemoRecording(string? requestedPath) => new(true, "[server] demo recording started: test.ogdemo", string.Empty);

        public OpenGarrisonServerDemoRecordingResult TryStopDemoRecording() => new(true, "[server] demo recording stopped: test.ogdemo", string.Empty);
    }

    private sealed class FakeServerCvarRegistry : IOpenGarrisonServerCvarRegistry
    {
        private readonly Dictionary<string, OpenGarrisonServerCvarInfo> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _runtimeProtectedNames = new(StringComparer.OrdinalIgnoreCase);

        public void Add(OpenGarrisonServerCvarInfo cvar)
        {
            _entries[cvar.Name] = cvar;
        }

        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll(bool includeProtectedValues)
        {
            return _entries.Values
                .Select(cvar => includeProtectedValues ? MarkProtected(cvar) : MaskProtectedValue(cvar))
                .ToArray();
        }

        public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll()
        {
            return GetAll(includeProtectedValues: false);
        }

        public bool TryGet(string name, bool includeProtectedValue, out OpenGarrisonServerCvarInfo cvar)
        {
            if (!_entries.TryGetValue(name, out cvar))
            {
                return false;
            }

            cvar = includeProtectedValue ? MarkProtected(cvar) : MaskProtectedValue(cvar);
            return true;
        }

        public bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
        {
            return TryGet(name, includeProtectedValue: false, out cvar);
        }

        public bool TrySet(string name, string value, bool allowProtectedMutation, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            if (!_entries.TryGetValue(name, out cvar))
            {
                errorMessage = "unsupported";
                return false;
            }

            if (IsProtected(cvar.Name) && !allowProtectedMutation)
            {
                errorMessage = "protected";
                cvar = MaskProtectedValue(cvar);
                return false;
            }

            errorMessage = string.Empty;
            cvar = cvar with { CurrentValue = value };
            _entries[name] = cvar;
            cvar = allowProtectedMutation ? MarkProtected(cvar) : MaskProtectedValue(cvar);
            return true;
        }

        public bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            return TrySet(name, value, allowProtectedMutation: false, out cvar, out errorMessage);
        }

        public bool TryProtect(string name, out OpenGarrisonServerCvarInfo cvar, out string errorMessage)
        {
            if (!_entries.TryGetValue(name, out cvar))
            {
                errorMessage = "unsupported";
                return false;
            }

            _runtimeProtectedNames.Add(cvar.Name);
            errorMessage = string.Empty;
            cvar = MaskProtectedValue(cvar);
            return true;
        }

        private bool IsProtected(string name)
        {
            return _runtimeProtectedNames.Contains(name)
                || (_entries.TryGetValue(name, out var cvar) && cvar.IsProtected);
        }

        private OpenGarrisonServerCvarInfo MarkProtected(OpenGarrisonServerCvarInfo cvar)
        {
            return IsProtected(cvar.Name)
                ? cvar with { IsProtected = true }
                : cvar;
        }

        private OpenGarrisonServerCvarInfo MaskProtectedValue(OpenGarrisonServerCvarInfo cvar)
        {
            cvar = MarkProtected(cvar);
            return cvar.IsProtected
                ? cvar with { CurrentValue = "<protected>" }
                : cvar;
        }
    }

    private sealed class FakeServerScheduler : IOpenGarrisonServerScheduler
    {
        public TimeSpan Uptime => TimeSpan.Zero;

        public Guid ScheduleOnce(TimeSpan delay, Action callback, string? description = null) => Guid.NewGuid();

        public Guid ScheduleRepeating(TimeSpan interval, Action callback, string? description = null, bool runImmediately = false) => Guid.NewGuid();

        public bool Cancel(Guid timerId) => false;

        public bool IsScheduled(Guid timerId) => false;

        public IReadOnlyList<OpenGarrisonServerScheduledTaskInfo> GetScheduledTasks() => [];
    }
}

public sealed class AdminCommandCapturePlugin : IOpenGarrisonServerPlugin, IOpenGarrisonServerChatCommandHooks
{
    public static List<(string Text, OpenGarrisonServerAdminIdentity Identity)> HandledCommands { get; } = [];

    public string Id => "tests.server.admin-capture";

    public string DisplayName => "Admin Command Capture";

    public Version Version => new(1, 0, 0);

    public static void Reset()
    {
        HandledCommands.Clear();
    }

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
    }

    public bool TryHandleChatMessage(OpenGarrisonServerChatMessageContext context, ChatReceivedEvent e)
    {
        if (!e.Text.StartsWith("!gt_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        HandledCommands.Add((e.Text, context.Identity));
        return true;
    }

    public void Shutdown()
    {
    }
}
