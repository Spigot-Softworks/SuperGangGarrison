using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using OpenGarrison.Server.Plugins;
using System.Text.Json;
using static ServerHelpers;

namespace OpenGarrison.Server;

internal sealed class ServerReadOnlyStateView(
    Func<string> serverNameGetter,
    Func<SimulationWorld> worldGetter,
    Func<IReadOnlyDictionary<byte, ClientSession>> clientsGetter) : IOpenGarrisonServerReadOnlyState
{
    public string ServerName => serverNameGetter();

    public string LevelName => worldGetter().Level.Name;

    public int MapAreaIndex => worldGetter().Level.MapAreaIndex;

    public int MapAreaCount => worldGetter().Level.MapAreaCount;

    public float MapScale => worldGetter().Level.MapScale;

    public GameModeKind GameMode => worldGetter().MatchRules.Mode;

    public MatchPhase MatchPhase => worldGetter().MatchState.Phase;

    public int RedCaps => worldGetter().RedCaps;

    public int BlueCaps => worldGetter().BlueCaps;

    public IReadOnlyList<OpenGarrisonServerPlayerInfo> GetPlayers()
    {
        var world = worldGetter();
        var clients = clientsGetter().Values.ToList();
        clients.Sort(static (left, right) => left.Slot.CompareTo(right.Slot));

        var players = new OpenGarrisonServerPlayerInfo[clients.Count];
        for (var index = 0; index < clients.Count; index += 1)
        {
            var client = clients[index];
            var isSpectator = IsSpectatorSlot(client.Slot);
            PlayerTeam? team = null;
            PlayerClass? playerClass = null;
            PlayerEntity? player = null;
            if (!isSpectator && world.TryGetNetworkPlayer(client.Slot, out var networkPlayer))
            {
                player = networkPlayer;
                team = networkPlayer.Team;
                playerClass = networkPlayer.ClassId;
            }

            players[index] = new OpenGarrisonServerPlayerInfo(
                Slot: client.Slot,
                UserId: client.UserId,
                Name: client.Name,
                IsSpectator: isSpectator,
                IsAuthorized: client.IsAuthorized,
                IsGagged: client.IsGagged,
                IsAlive: player?.IsAlive ?? false,
                PlayerId: player?.Id,
                Team: team,
                PlayerClass: playerClass,
                PlayerScale: player?.PlayerScale ?? 1f,
                MovementSpeedScale: player?.ServerMovementSpeedScale ?? (!isSpectator ? world.GetNetworkPlayerMovementSpeedScale(client.Slot) : 1f),
                HasMovementSpeedScaleOverride: !isSpectator && world.HasNetworkPlayerMovementSpeedScaleOverride(client.Slot),
                GravityScale: player?.ServerGravityScale ?? (!isSpectator ? world.GetNetworkPlayerGravityScale(client.Slot) : 1f),
                HasGravityScaleOverride: !isSpectator && world.HasNetworkPlayerGravityScaleOverride(client.Slot),
                EndPoint: client.RemoteDescription,
                GameplayLoadoutId: player?.GameplayLoadoutState.LoadoutId ?? string.Empty,
                GameplaySecondaryItemId: player?.GameplayLoadoutState.SecondaryItemId ?? string.Empty,
                GameplayAcquiredItemId: player?.GameplayLoadoutState.AcquiredItemId ?? string.Empty,
                GameplayEquippedSlot: player?.GameplayLoadoutState.EquippedSlot ?? GameplayEquipmentSlot.Primary,
                GameplayEquippedItemId: player?.GameplayLoadoutState.EquippedItemId ?? string.Empty,
                WorldX: player?.X,
                WorldY: player?.Y,
                HorizontalSpeed: player?.HorizontalSpeed,
                VerticalSpeed: player?.VerticalSpeed,
                Health: player?.Health,
                MaxHealth: player?.MaxHealth,
                CurrentAmmo: player?.CurrentShells,
                MaxAmmo: player?.MaxShells,
                Kills: player?.Kills,
                Deaths: player?.Deaths,
                Assists: player?.Assists,
                Caps: player?.Caps,
                Points: player?.Points,
                IsCarryingIntel: player?.IsCarryingIntel ?? false,
                IsInSpawnRoom: player?.IsInSpawnRoom ?? false);
        }

        return players;
    }

    public OpenGarrisonServerObjectiveStateInfo GetObjectives()
    {
        var world = worldGetter();
        return new OpenGarrisonServerObjectiveStateInfo(
            world.ControlPoints.Select(static point => new OpenGarrisonServerControlPointInfo(
                point.Index,
                point.Marker.CenterX,
                point.Marker.CenterY,
                point.Marker.Width,
                point.Marker.Height,
                point.Team,
                point.CappingTeam,
                point.CappingTicks,
                point.CapTimeTicks,
                point.RedCappers,
                point.BlueCappers,
                point.Cappers,
                point.IsLocked,
                point.HasHealingAura)).ToArray(),
            world.Generators.Select(static generator => new OpenGarrisonServerGeneratorInfo(
                generator.Team,
                generator.Marker.CenterX,
                generator.Marker.CenterY,
                generator.Marker.Width,
                generator.Marker.Height,
                generator.Health,
                generator.MaxHealth,
                generator.IsDestroyed,
                generator.HealthFraction,
                generator.DamageStage)).ToArray(),
            new[]
            {
                ToIntelligenceInfo(world.RedIntel),
                ToIntelligenceInfo(world.BlueIntel),
            });
    }

    public IReadOnlyList<OpenGarrisonServerBuildableInfo> GetBuildables()
    {
        var world = worldGetter();
        return world.Sentries
            .Select(static sentry => new OpenGarrisonServerBuildableInfo(
                "sentry",
                sentry.Id,
                sentry.OwnerPlayerId,
                sentry.Team,
                sentry.X,
                sentry.Y,
                sentry.Health,
                sentry.MaxHealth,
                sentry.IsBuilt,
                sentry.Health <= 0,
                sentry.HasLanded,
                sentry.HasActiveTarget))
            .Concat(world.JumpPads.Select(static pad => new OpenGarrisonServerBuildableInfo(
                "jump_pad",
                pad.Id,
                pad.OwnerPlayerId,
                pad.Team,
                pad.X,
                pad.Y,
                pad.Health,
                JumpPadEntity.MaxHealth,
                IsBuilt: true,
                pad.IsDead,
                pad.HasLanded,
                HasActiveTarget: false)))
            .Concat(world.CivilDefenseTurrets.Select(static turret => new OpenGarrisonServerBuildableInfo(
                "civil_defense_turret",
                turret.Id,
                turret.OwnerPlayerId,
                turret.Team,
                turret.X,
                turret.Y,
                turret.Health,
                CivilDefenseTurretEntity.MaxHealth,
                turret.IsBuilt,
                turret.IsDead,
                turret.HasLanded,
                HasActiveTarget: false)))
            .OrderBy(static buildable => buildable.Kind, StringComparer.Ordinal)
            .ThenBy(static buildable => buildable.Id)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerProjectileInfo> GetProjectiles()
    {
        var world = worldGetter();
        return world.Shots.Select(static shot => CreateLinearProjectileInfo("shot", shot.Id, shot.OwnerId, shot.Team, shot.X, shot.Y, shot.PreviousX, shot.PreviousY, shot.VelocityX, shot.VelocityY, shot.TicksRemaining, shot.IsCritical, shot.IsExpired))
            .Concat(world.Bubbles.Select(static bubble => CreateLinearProjectileInfo("bubble", bubble.Id, bubble.OwnerId, bubble.Team, bubble.X, bubble.Y, bubble.PreviousX, bubble.PreviousY, bubble.VelocityX, bubble.VelocityY, bubble.TicksRemaining, false, bubble.IsExpired)))
            .Concat(world.Blades.Select(static blade => CreateLinearProjectileInfo("blade", blade.Id, blade.OwnerId, blade.Team, blade.X, blade.Y, blade.PreviousX, blade.PreviousY, blade.VelocityX, blade.VelocityY, blade.TicksRemaining, blade.IsCritical, blade.IsExpired)))
            .Concat(world.Needles.Select(static needle => CreateLinearProjectileInfo("needle", needle.Id, needle.OwnerId, needle.Team, needle.X, needle.Y, needle.PreviousX, needle.PreviousY, needle.VelocityX, needle.VelocityY, needle.TicksRemaining, needle.IsCritical, needle.IsExpired)))
            .Concat(world.RevolverShots.Select(static shot => CreateLinearProjectileInfo("revolver_shot", shot.Id, shot.OwnerId, shot.Team, shot.X, shot.Y, shot.PreviousX, shot.PreviousY, shot.VelocityX, shot.VelocityY, shot.TicksRemaining, shot.IsCritical, shot.IsExpired)))
            .Concat(world.Flames.Select(static flame => CreateLinearProjectileInfo("flame", flame.Id, flame.OwnerId, flame.Team, flame.X, flame.Y, flame.PreviousX, flame.PreviousY, flame.VelocityX, flame.VelocityY, flame.TicksRemaining, flame.IsCritical, flame.IsExpired)))
            .Concat(world.Flares.Select(static flare => CreateLinearProjectileInfo("flare", flare.Id, flare.OwnerId, flare.Team, flare.X, flare.Y, flare.PreviousX, flare.PreviousY, flare.VelocityX, flare.VelocityY, flare.TicksRemaining, flare.IsCritical, flare.IsExpired)))
            .Concat(world.Rockets.Select(static rocket => new OpenGarrisonServerProjectileInfo(
                "rocket",
                rocket.Id,
                rocket.OwnerId,
                rocket.Team,
                rocket.X,
                rocket.Y,
                rocket.PreviousX,
                rocket.PreviousY,
                null,
                null,
                rocket.DirectionRadians,
                rocket.Speed,
                rocket.TicksRemaining,
                rocket.IsCritical,
                rocket.IsExpired || rocket.ExplodeImmediately)))
            .Concat(world.Mines.Select(static mine => CreateLinearProjectileInfo("mine", mine.Id, mine.OwnerId, mine.Team, mine.X, mine.Y, mine.PreviousX, mine.PreviousY, mine.VelocityX, mine.VelocityY, null, mine.IsCritical, mine.IsDestroyed)))
            .Concat(world.Grenades.Select(static grenade => CreateLinearProjectileInfo("grenade", grenade.Id, grenade.OwnerId, grenade.Team, grenade.X, grenade.Y, grenade.PreviousX, grenade.PreviousY, grenade.VelocityX, grenade.VelocityY, grenade.FuseTicksLeft, grenade.IsCritical, grenade.IsDestroyed)))
            .OrderBy(static projectile => projectile.Kind, StringComparer.Ordinal)
            .ThenBy(static projectile => projectile.Id)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerRecentEventInfo> GetRecentEvents()
    {
        var world = worldGetter();
        return world.PendingSoundEvents.Select(static e => new OpenGarrisonServerRecentEventInfo(
                "sound", e.SoundName, e.EventId, e.SourceFrame, e.X, e.Y, Amount: null, TargetEntityId: null, TargetPlayerId: null, AttackerPlayerId: null, WasFatal: false))
            .Concat(world.PendingVisualEvents.Select(static e => new OpenGarrisonServerRecentEventInfo(
                "visual", e.EffectName, e.EventId, SourceFrame: 0, WorldX: e.X, WorldY: e.Y, Amount: e.Count, TargetEntityId: null, TargetPlayerId: null, AttackerPlayerId: null, WasFatal: false)))
            .Concat(world.PendingDamageEvents.Select(static e => new OpenGarrisonServerRecentEventInfo(
                "damage", e.TargetKind.ToString(), e.EventId, e.SourceFrame, e.X, e.Y, e.Amount, e.TargetEntityId, TargetPlayerId: null, AttackerPlayerId: e.AttackerPlayerId, WasFatal: e.WasFatal)))
            .Concat(world.PendingHealingEvents.Select(static e => new OpenGarrisonServerRecentEventInfo(
                "healing", "healing", EventId: 0, SourceFrame: e.SourceFrame, WorldX: null, WorldY: null, Amount: e.Amount, TargetEntityId: null, TargetPlayerId: e.TargetPlayerId, AttackerPlayerId: null, WasFatal: false)))
            .Concat(world.PendingRocketSpawnEvents.Select(static e => new OpenGarrisonServerRecentEventInfo(
                "rocket_spawn", "rocket_spawn", EventId: 0, SourceFrame: 0, WorldX: e.X, WorldY: e.Y, Amount: null, TargetEntityId: e.Id, TargetPlayerId: null, AttackerPlayerId: e.OwnerId, WasFatal: false)))
            .ToArray();
    }

    public OpenGarrisonServerMapRegionInfo GetMapRegion(float centerX, float centerY, float radius, int limit = 128)
    {
        var world = worldGetter();
        radius = Math.Clamp(radius, 1f, 2048f);
        limit = Math.Clamp(limit, 1, 256);
        var left = centerX - radius;
        var top = centerY - radius;
        var right = centerX + radius;
        var bottom = centerY + radius;
        var solids = world.Level.Solids
            .Select(static (solid, index) => (Solid: solid, Index: index))
            .Where(entry => RectanglesIntersect(entry.Solid.Left, entry.Solid.Top, entry.Solid.Right, entry.Solid.Bottom, left, top, right, bottom))
            .OrderBy(entry => DistanceSquaredToCenter(entry.Solid.X + (entry.Solid.Width / 2f), entry.Solid.Y + (entry.Solid.Height / 2f), centerX, centerY))
            .Take(limit + 1)
            .ToArray();
        var remainingLimit = Math.Max(0, limit - Math.Min(solids.Length, limit));
        var roomObjects = world.Level.RoomObjects
            .Select(static (roomObject, index) => (RoomObject: roomObject, Index: index))
            .Where(entry => RectanglesIntersect(entry.RoomObject.Left, entry.RoomObject.Top, entry.RoomObject.Right, entry.RoomObject.Bottom, left, top, right, bottom))
            .OrderBy(entry => DistanceSquaredToCenter(entry.RoomObject.CenterX, entry.RoomObject.CenterY, centerX, centerY))
            .Take(remainingLimit + 1)
            .ToArray();
        var isTruncated = solids.Length > limit || roomObjects.Length > remainingLimit;
        return new OpenGarrisonServerMapRegionInfo(
            new OpenGarrisonServerMapBoundsInfo(world.Bounds.Width, world.Bounds.Height),
            centerX,
            centerY,
            radius,
            solids.Take(limit)
                .Select(static entry => new OpenGarrisonServerMapSolidInfo(
                    entry.Index,
                    entry.Solid.X,
                    entry.Solid.Y,
                    entry.Solid.Width,
                    entry.Solid.Height))
                .ToArray(),
            roomObjects.Take(remainingLimit)
                .Select(static entry => new OpenGarrisonServerMapRoomObjectInfo(
                    entry.Index,
                    entry.RoomObject.Type.ToString(),
                    entry.RoomObject.X,
                    entry.RoomObject.Y,
                    entry.RoomObject.Width,
                    entry.RoomObject.Height,
                    entry.RoomObject.Team,
                    entry.RoomObject.SourceName,
                    entry.RoomObject.Value))
                .ToArray(),
            isTruncated);
    }

    public OpenGarrisonServerVisibilityInfo HasLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam? team = null)
    {
        return new OpenGarrisonServerVisibilityInfo(
            originX,
            originY,
            targetX,
            targetY,
            team,
            worldGetter().QueryHasLineOfSight(originX, originY, targetX, targetY, team));
    }

    public IReadOnlyList<OpenGarrisonServerGameplayModPackInfo> GetGameplayModPacks()
    {
        return CharacterClassCatalog.RuntimeRegistry.ModPacks
            .OrderBy(pack => pack.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pack => pack.Id, StringComparer.Ordinal)
            .Select(pack => new OpenGarrisonServerGameplayModPackInfo(
                pack.Id,
                pack.DisplayName,
                pack.Version.ToString(),
                pack.Items.Count,
                pack.Classes.Count,
                string.Equals(pack.Id, StockGameplayModCatalog.Definition.Id, StringComparison.Ordinal)))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayClassInfo> GetGameplayClasses(string? modPackId = null)
    {
        return GetFilteredGameplayModPacks(modPackId)
            .SelectMany(static pack => pack.Classes.Values.Select(gameplayClass => new OpenGarrisonServerGameplayClassInfo(
                pack.Id,
                gameplayClass.Id,
                gameplayClass.DisplayName,
                gameplayClass.DefaultLoadoutId,
                gameplayClass.Loadouts.Count)))
            .OrderBy(gameplayClass => gameplayClass.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(gameplayClass => gameplayClass.ModPackId, StringComparer.Ordinal)
            .ThenBy(gameplayClass => gameplayClass.ClassId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetGameplayItems(string? modPackId = null)
    {
        return GetFilteredGameplayModPacks(modPackId)
            .SelectMany(static pack => pack.Items.Values.Select(item => new OpenGarrisonServerGameplayItemInfo(
                pack.Id,
                item.Id,
                item.DisplayName,
                item.Slot,
                item.BehaviorId,
                item.Ownership?.TrackOwnership ?? false,
                item.Ownership?.DefaultGranted ?? true,
                item.Ownership?.GrantOnAcquire ?? false,
                item.Ownership?.GrantKey)))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ModPackId, StringComparer.Ordinal)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetGameplayAbilities()
    {
        return CharacterClassCatalog.RuntimeRegistry.Items
            .Where(static item => item.Ability is not null)
            .Select(ToGameplayAbilityInfo)
            .OrderBy(ability => ability.Category, StringComparer.Ordinal)
            .ThenBy(ability => ability.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(ability => ability.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayAbilityInfo> GetPlayerGameplayAbilities(int playerId)
    {
        var world = worldGetter();
        if (!TryGetNetworkPlayerByPlayerId(world, playerId, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplayAbilityInfo>();
        }

        return EnumeratePlayerGameplayAbilityItems(player)
            .Select(ToGameplayAbilityInfo)
            .OrderBy(ability => ability.Category, StringComparer.Ordinal)
            .ThenBy(ability => ability.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGetPlayerGameplayAbility(int playerId, string category, out OpenGarrisonServerGameplayAbilityInfo ability)
    {
        ability = default;
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        ability = GetPlayerGameplayAbilities(playerId)
            .FirstOrDefault(info => string.Equals(info.Category, category.Trim(), StringComparison.Ordinal));
        return !string.IsNullOrEmpty(ability.ItemId);
    }

    public IReadOnlyList<OpenGarrisonServerGameplayItemInfo> GetOwnedGameplayItems(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplayItemInfo>();
        }

        return player.GetOwnedGameplayItemIds()
            .Select(itemId => CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId))
            .Select(item => new OpenGarrisonServerGameplayItemInfo(
                GetOwningModPackId(item.Id),
                item.Id,
                item.DisplayName,
                item.Slot,
                item.BehaviorId,
                item.Ownership?.TrackOwnership ?? false,
                item.Ownership?.DefaultGranted ?? true,
                item.Ownership?.GrantOnAcquire ?? false,
                item.Ownership?.GrantKey))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetGameplayLoadoutsForClass(string classId)
    {
        if (string.IsNullOrWhiteSpace(classId))
        {
            return Array.Empty<OpenGarrisonServerGameplayLoadoutInfo>();
        }

        var gameplayClass = CharacterClassCatalog.RuntimeRegistry.GetRequiredClass(classId);
        return gameplayClass.Loadouts.Values
            .OrderBy(loadout => loadout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(loadout => loadout.Id, StringComparer.Ordinal)
            .Select(static loadout => new OpenGarrisonServerGameplayLoadoutInfo(
                loadout.Id,
                loadout.DisplayName,
                loadout.PrimaryItemId,
                loadout.SecondaryItemId,
                loadout.UtilityItemId,
                IsSelected: false,
                IsAvailableToPlayer: false))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplaySecondaryItems(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplaySelectableItemInfo>();
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var classDefinition = runtimeRegistry.GetClassDefinition(player.GameplayClassId);
        var loadout = runtimeRegistry.TryGetLoadout(player.GameplayClassId, player.GameplayLoadoutState.LoadoutId, out var selectedLoadout)
            ? selectedLoadout
            : classDefinition.Loadouts[classDefinition.DefaultLoadoutId];
        var items = new List<OpenGarrisonServerGameplaySelectableItemInfo>();
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);

        AddSelectableItem(player, items, seenItemIds, loadout.SecondaryItemId, player.GameplayLoadoutState.SecondaryItemId);
        if (runtimeRegistry.SupportsExperimentalAcquiredWeapon(player.GameplayClassId))
        {
            foreach (var modPack in runtimeRegistry.ModPacks.OrderBy(pack => pack.Id, StringComparer.Ordinal))
            {
                foreach (var item in modPack.Items.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    if (item.Slot != GameplayEquipmentSlot.Primary)
                    {
                        continue;
                    }

                    AddSelectableItem(player, items, seenItemIds, item.Id, player.GameplayLoadoutState.SecondaryItemId);
                }
            }
        }

        return items;
    }

    public IReadOnlyList<OpenGarrisonServerGameplaySelectableItemInfo> GetAvailableGameplayAcquiredItems(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplaySelectableItemInfo>();
        }

        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        if (!runtimeRegistry.SupportsExperimentalAcquiredWeapon(player.GameplayClassId))
        {
            return Array.Empty<OpenGarrisonServerGameplaySelectableItemInfo>();
        }

        var items = new List<OpenGarrisonServerGameplaySelectableItemInfo>();
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var modPack in runtimeRegistry.ModPacks.OrderBy(pack => pack.Id, StringComparer.Ordinal))
        {
            foreach (var item in modPack.Items.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (item.Slot != GameplayEquipmentSlot.Primary)
                {
                    continue;
                }

                AddSelectableItem(player, items, seenItemIds, item.Id, player.GameplayLoadoutState.AcquiredItemId);
            }
        }

        return items;
    }

    public IReadOnlyList<OpenGarrisonServerGameplayLoadoutInfo> GetAvailableGameplayLoadouts(byte slot)
    {
        var world = worldGetter();
        if (!world.TryGetNetworkPlayer(slot, out var player))
        {
            return Array.Empty<OpenGarrisonServerGameplayLoadoutInfo>();
        }

        var gameplayClass = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(player.GameplayClassId);
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        return gameplayClass.Loadouts
            .Values
            .OrderBy(loadout => loadout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(loadout => loadout.Id, StringComparer.Ordinal)
            .Select(loadout => new OpenGarrisonServerGameplayLoadoutInfo(
                loadout.Id,
                loadout.DisplayName,
                loadout.PrimaryItemId,
                loadout.SecondaryItemId,
                loadout.UtilityItemId,
                string.Equals(loadout.Id, player.GameplayLoadoutState.LoadoutId, StringComparison.Ordinal),
                GameplayRuntimeRegistry.LoadoutItemsAreOwned(loadout, player.OwnsGameplayItem)))
            .ToArray();
    }

    public bool TryGetPlayerReplicatedStateInt(byte slot, string ownerPluginId, string stateKey, out int value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return TryGetPlayerReplicatedStateInt(player, ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateFloat(byte slot, string ownerPluginId, string stateKey, out float value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return TryGetPlayerReplicatedStateFloat(player, ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateBool(byte slot, string ownerPluginId, string stateKey, out bool value)
    {
        if (worldGetter().TryGetNetworkPlayer(slot, out var player))
        {
            return TryGetPlayerReplicatedStateBool(player, ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateInt(int playerId, string ownerPluginId, string stateKey, out int value)
    {
        if (TryGetNetworkPlayerByPlayerId(worldGetter(), playerId, out var player))
        {
            return TryGetPlayerReplicatedStateInt(player, ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateFloat(int playerId, string ownerPluginId, string stateKey, out float value)
    {
        if (TryGetNetworkPlayerByPlayerId(worldGetter(), playerId, out var player))
        {
            return TryGetPlayerReplicatedStateFloat(player, ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    public bool TryGetPlayerReplicatedStateBool(int playerId, string ownerPluginId, string stateKey, out bool value)
    {
        if (TryGetNetworkPlayerByPlayerId(worldGetter(), playerId, out var player))
        {
            return TryGetPlayerReplicatedStateBool(player, ownerPluginId, stateKey, out value);
        }

        value = default;
        return false;
    }

    private static GameplayModPackDefinition[] GetFilteredGameplayModPacks(string? modPackId)
    {
        var modPacks = CharacterClassCatalog.RuntimeRegistry.ModPacks;
        if (string.IsNullOrWhiteSpace(modPackId))
        {
            return modPacks
                .OrderBy(pack => pack.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pack => pack.Id, StringComparer.Ordinal)
                .ToArray();
        }

        return modPacks
            .Where(pack => string.Equals(pack.Id, modPackId, StringComparison.Ordinal))
            .ToArray();
    }

    private static string GetOwningModPackId(string itemId)
    {
        return CharacterClassCatalog.RuntimeRegistry.ModPacks
            .First(pack => pack.Items.ContainsKey(itemId))
            .Id;
    }

    private static IEnumerable<GameplayItemDefinition> EnumeratePlayerGameplayAbilityItems(PlayerEntity player)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var itemId in new[]
        {
            player.GameplayLoadoutState.PrimaryItemId,
            player.GameplayLoadoutState.SecondaryItemId,
            player.GameplayLoadoutState.UtilityItemId,
            player.GameplayLoadoutState.AcquiredItemId,
            player.GameplayLoadoutState.EquippedItemId,
        })
        {
            if (string.IsNullOrWhiteSpace(itemId) || !seenItemIds.Add(itemId))
            {
                continue;
            }

            if (runtimeRegistry.TryGetGameplayAbilityDefinition(itemId, out var item, out _))
            {
                yield return item;
            }
        }

        if (!runtimeRegistry.TryGetLoadout(player.GameplayClassId, player.SelectedGameplayLoadoutId, out var selectedLoadout)
            || selectedLoadout.AbilityItemIds is null)
        {
            yield break;
        }

        foreach (var itemId in selectedLoadout.AbilityItemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !seenItemIds.Add(itemId))
            {
                continue;
            }

            if (runtimeRegistry.TryGetGameplayAbilityDefinition(itemId, out var item, out _))
            {
                yield return item;
            }
        }
    }

    private static OpenGarrisonServerGameplayAbilityInfo ToGameplayAbilityInfo(GameplayItemDefinition item)
    {
        var ability = item.Ability!;
        return new OpenGarrisonServerGameplayAbilityInfo(
            GetOwningModPackId(item.Id),
            item.Id,
            item.DisplayName,
            item.Slot,
            item.BehaviorId,
            ability.Category,
            ability.Activation,
            ability.ExecutorId,
            ability.Tags.ToArray(),
            ToAbilityParameterDictionary(ability.Parameters));
    }

    private static OpenGarrisonServerIntelligenceInfo ToIntelligenceInfo(TeamIntelligenceState intel)
    {
        return new OpenGarrisonServerIntelligenceInfo(
            intel.Team,
            intel.X,
            intel.Y,
            intel.HomeX,
            intel.HomeY,
            intel.IsAtBase,
            intel.IsDropped,
            intel.IsCarried,
            intel.ReturnTicksRemaining);
    }

    private static OpenGarrisonServerProjectileInfo CreateLinearProjectileInfo(
        string kind,
        int id,
        int ownerPlayerId,
        PlayerTeam team,
        float worldX,
        float worldY,
        float previousWorldX,
        float previousWorldY,
        float velocityX,
        float velocityY,
        int? ticksRemaining,
        bool isCritical,
        bool isDestroyed)
    {
        return new OpenGarrisonServerProjectileInfo(
            kind,
            id,
            ownerPlayerId,
            team,
            worldX,
            worldY,
            previousWorldX,
            previousWorldY,
            velocityX,
            velocityY,
            null,
            null,
            ticksRemaining,
            isCritical,
            isDestroyed);
    }

    private static bool RectanglesIntersect(
        float left,
        float top,
        float right,
        float bottom,
        float otherLeft,
        float otherTop,
        float otherRight,
        float otherBottom)
    {
        return left < otherRight && right > otherLeft && top < otherBottom && bottom > otherTop;
    }

    private static float DistanceSquaredToCenter(float x, float y, float centerX, float centerY)
    {
        var dx = x - centerX;
        var dy = y - centerY;
        return (dx * dx) + (dy * dy);
    }

    private static IReadOnlyDictionary<string, string> ToAbilityParameterDictionary(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (parameters.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return parameters.ToDictionary(
            static pair => pair.Key,
            static pair => FormatAbilityParameter(pair.Value),
            StringComparer.Ordinal);
    }

    private static string FormatAbilityParameter(JsonElement parameter)
    {
        return parameter.ValueKind == JsonValueKind.String
            ? parameter.GetString() ?? string.Empty
            : parameter.GetRawText();
    }

    private static bool TryGetNetworkPlayerByPlayerId(SimulationWorld world, int playerId, out PlayerEntity player)
    {
        foreach (var (_, networkPlayer) in world.EnumerateActiveNetworkPlayers())
        {
            if (networkPlayer.Id == playerId)
            {
                player = networkPlayer;
                return true;
            }
        }

        player = null!;
        return false;
    }

    private static bool TryGetPlayerReplicatedStateInt(PlayerEntity player, string ownerPluginId, string stateKey, out int value)
    {
        if (string.Equals(ownerPluginId, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal)
            && GameplayAbilityReplicatedState.TryGetInt(player, stateKey, out value))
        {
            return true;
        }

        return player.TryGetReplicatedStateInt(ownerPluginId, stateKey, out value);
    }

    private static bool TryGetPlayerReplicatedStateFloat(PlayerEntity player, string ownerPluginId, string stateKey, out float value)
    {
        if (string.Equals(ownerPluginId, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal)
            && GameplayAbilityReplicatedState.TryGetFloat(player, stateKey, out value))
        {
            return true;
        }

        return player.TryGetReplicatedStateFloat(ownerPluginId, stateKey, out value);
    }

    private static bool TryGetPlayerReplicatedStateBool(PlayerEntity player, string ownerPluginId, string stateKey, out bool value)
    {
        if (string.Equals(ownerPluginId, GameplayAbilityConstants.CoreAbilityReplicatedStateOwnerId, StringComparison.Ordinal)
            && GameplayAbilityReplicatedState.TryGetBool(player, stateKey, out value))
        {
            return true;
        }

        return player.TryGetReplicatedStateBool(ownerPluginId, stateKey, out value);
    }

    private static void AddSelectableItem(
        PlayerEntity player,
        List<OpenGarrisonServerGameplaySelectableItemInfo> items,
        HashSet<string> seenItemIds,
        string? itemId,
        string? selectedItemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !seenItemIds.Add(itemId))
        {
            return;
        }

        var item = CharacterClassCatalog.RuntimeRegistry.GetRequiredItem(itemId);
        items.Add(new OpenGarrisonServerGameplaySelectableItemInfo(
            item.Id,
            item.DisplayName,
            item.Slot,
            item.BehaviorId,
            string.Equals(item.Id, selectedItemId, StringComparison.Ordinal),
            player.OwnsGameplayItem(item.Id),
            item.Ownership?.TrackOwnership ?? false,
            item.Ownership?.DefaultGranted ?? true,
            item.Ownership?.GrantOnAcquire ?? false,
            item.Ownership?.GrantKey));
    }
}
