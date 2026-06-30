#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

internal readonly record struct ImmediateNetworkDeadBodyPresentationState(
    int SourcePlayerId,
    PlayerClass ClassId,
    PlayerTeam Team,
    DeadBodyAnimationKind AnimationKind,
    float X,
    float Y,
    float Width,
    float Height,
    bool FacingLeft,
    int TicksRemaining);

internal static class ImmediateNetworkDeathPresentationPlanner
{
    private const int FreshAuthoritativeDeadBodyMaxElapsedTicks = 90;
    private const float FreshAuthoritativeDeadBodyMaxDistance = 96f;

    internal static ImmediateNetworkDeadBodyPresentationState? TryCreate(
        SnapshotMessage resolvedSnapshot,
        SnapshotDamageEvent damageEvent,
        PlayerEntity? targetPlayer,
        int lifetimeTicks)
    {
        if (!damageEvent.WasFatal
            || damageEvent.TargetKind != (byte)OpenGarrison.Core.DamageTargetKind.Player)
        {
            return null;
        }

        if (((DamageEventFlags)damageEvent.Flags).HasFlag(DamageEventFlags.Gibbed))
        {
            return null;
        }

        if (TryGetFreshAuthoritativeDeadBodyForPlayer(resolvedSnapshot, damageEvent, targetPlayer, out var authoritativeDeadBody))
        {
            return new ImmediateNetworkDeadBodyPresentationState(
                damageEvent.TargetEntityId,
                (PlayerClass)authoritativeDeadBody.ClassId,
                (PlayerTeam)authoritativeDeadBody.Team,
                (DeadBodyAnimationKind)authoritativeDeadBody.AnimationKind,
                authoritativeDeadBody.X,
                authoritativeDeadBody.Y,
                authoritativeDeadBody.Width,
                authoritativeDeadBody.Height,
                authoritativeDeadBody.FacingLeft,
                lifetimeTicks);
        }

        if (targetPlayer is null)
        {
            return null;
        }

        return new ImmediateNetworkDeadBodyPresentationState(
            damageEvent.TargetEntityId,
            targetPlayer.ClassId,
            targetPlayer.Team,
            DeadBodyAnimationKind.Default,
            targetPlayer.X,
            targetPlayer.Y,
            targetPlayer.Width,
            targetPlayer.Height,
            MathF.Cos(targetPlayer.AimDirectionDegrees * (MathF.PI / 180f)) < 0f,
            lifetimeTicks);
    }

    internal static bool TryGetFreshAuthoritativeDeadBodyForPlayer(
        SnapshotMessage resolvedSnapshot,
        SnapshotDamageEvent damageEvent,
        PlayerEntity? targetPlayer,
        out SnapshotDeadBodyState deadBody)
    {
        var sourcePlayerId = damageEvent.TargetEntityId;
        var referenceX = targetPlayer?.X ?? damageEvent.X;
        var referenceY = targetPlayer?.Y ?? damageEvent.Y;
        var maxDistanceSquared = FreshAuthoritativeDeadBodyMaxDistance * FreshAuthoritativeDeadBodyMaxDistance;
        var bestTicksRemaining = int.MinValue;
        SnapshotDeadBodyState bestDeadBody = default!;
        var found = false;
        for (var index = 0; index < resolvedSnapshot.DeadBodies.Count; index += 1)
        {
            var candidate = resolvedSnapshot.DeadBodies[index];
            if (candidate.SourcePlayerId != sourcePlayerId)
            {
                continue;
            }

            var elapsedTicks = DeadBodyEntity.LifetimeTicks - candidate.TicksRemaining;
            if (elapsedTicks < 0 || elapsedTicks > FreshAuthoritativeDeadBodyMaxElapsedTicks)
            {
                continue;
            }

            var distanceX = candidate.X - referenceX;
            var distanceY = candidate.Y - referenceY;
            if ((distanceX * distanceX) + (distanceY * distanceY) > maxDistanceSquared)
            {
                continue;
            }

            if (!found || candidate.TicksRemaining > bestTicksRemaining)
            {
                found = true;
                bestTicksRemaining = candidate.TicksRemaining;
                bestDeadBody = candidate;
            }
        }

        deadBody = bestDeadBody;
        return found;
    }
}

public partial class Game1
{
    private sealed class GameplayDeadBodyRenderController
    {
        private const int ImmediateNetworkDeadBodyLifetimeTicks = 90;
        private readonly Game1 _game;

        public GameplayDeadBodyRenderController(Game1 game)
        {
            _game = game;
        }

        public void DrawDeadBody(DeadBodyEntity deadBody, Vector2 cameraPosition)
        {
            var renderPosition = _game.GetRenderPosition(deadBody.Id, deadBody.X, deadBody.Y);
            DrawDeadBodyVisual(deadBody.Id, deadBody.SourcePlayerId, deadBody.ClassId, deadBody.Team, deadBody.AnimationKind, renderPosition.X, renderPosition.Y, deadBody.Width, deadBody.Height, deadBody.FacingLeft, deadBody.TicksRemaining, cameraPosition);
        }

        public void DrawRetainedDeadBodies(Vector2 cameraPosition, int? skippedDeadBodySourcePlayerId = null)
        {
            for (var index = 0; index < _game._retainedDeadBodies.Count; index += 1)
            {
                var deadBody = _game._retainedDeadBodies[index];
                if (skippedDeadBodySourcePlayerId.HasValue && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
                {
                    continue;
                }

                DrawDeadBodyVisual(deadBody.Id, deadBody.SourcePlayerId, deadBody.ClassId, deadBody.Team, deadBody.AnimationKind, deadBody.X, deadBody.Y, deadBody.Width, deadBody.Height, deadBody.FacingLeft, deadBody.TicksRemaining, cameraPosition);
            }
        }

        public void DrawImmediateNetworkDeadBodies(Vector2 cameraPosition, int? skippedDeadBodySourcePlayerId = null)
        {
            foreach (var entry in _game._immediateNetworkDeadBodies)
            {
                var deadBody = entry.Value;
                if (skippedDeadBodySourcePlayerId.HasValue && deadBody.SourcePlayerId == skippedDeadBodySourcePlayerId.Value)
                {
                    continue;
                }

                DrawDeadBodyVisual(
                    id: -Math.Abs(deadBody.SourcePlayerId),
                    deadBody.SourcePlayerId,
                    deadBody.ClassId,
                    deadBody.Team,
                    deadBody.AnimationKind,
                    deadBody.X,
                    deadBody.Y,
                    deadBody.Width,
                    deadBody.Height,
                    deadBody.FacingLeft,
                    deadBody.TicksRemaining,
                    cameraPosition);
            }
        }

        public void SyncRetainedDeadBodies()
        {
            if (_game._corpseDurationMode != ClientSettings.CorpseDurationInfinite)
            {
                ResetRetainedDeadBodies();
                return;
            }

            _game._staleTrackedDeadBodyIds.Clear();
            foreach (var trackedId in _game._trackedDeadBodyVisuals.Keys)
            {
                _game._staleTrackedDeadBodyIds.Add(trackedId);
            }

            foreach (var deadBody in _game._world.DeadBodies)
            {
                _game._trackedDeadBodyVisuals[deadBody.Id] = new RetainedDeadBodyVisual(deadBody.Id, deadBody.SourcePlayerId, deadBody.ClassId, deadBody.Team, deadBody.AnimationKind, deadBody.X, deadBody.Y, deadBody.Width, deadBody.Height, deadBody.FacingLeft, deadBody.TicksRemaining);
                _game._staleTrackedDeadBodyIds.Remove(deadBody.Id);
                RemoveRetainedDeadBody(deadBody.Id);
            }

            for (var index = 0; index < _game._staleTrackedDeadBodyIds.Count; index += 1)
            {
                var deadBodyId = _game._staleTrackedDeadBodyIds[index];
                if (_game._trackedDeadBodyVisuals.TryGetValue(deadBodyId, out var retainedDeadBody))
                {
                    if (retainedDeadBody.TicksRemaining <= 15)
                    {
                        _game._retainedDeadBodies.Add(retainedDeadBody);
                    }

                    _game._trackedDeadBodyVisuals.Remove(deadBodyId);
                }
            }
        }

        public void SyncImmediateNetworkDeadBodies()
        {
            _game._staleImmediateNetworkDeadBodyPlayerIds.Clear();
            foreach (var sourcePlayerId in _game._immediateNetworkDeadBodies.Keys)
            {
                _game._staleImmediateNetworkDeadBodyPlayerIds.Add(sourcePlayerId);
            }

            foreach (var deadBody in _game._world.DeadBodies)
            {
                _game._staleImmediateNetworkDeadBodyPlayerIds.Remove(deadBody.SourcePlayerId);
                _game._immediateNetworkDeadBodies.Remove(deadBody.SourcePlayerId);
            }

            for (var index = _game._staleImmediateNetworkDeadBodyPlayerIds.Count - 1; index >= 0; index -= 1)
            {
                var sourcePlayerId = _game._staleImmediateNetworkDeadBodyPlayerIds[index];
                var player = _game.FindPlayerById(sourcePlayerId);
                if (player is not null && !player.IsAlive)
                {
                    continue;
                }

                _game._immediateNetworkDeadBodies.Remove(sourcePlayerId);
            }
        }

        private void RemoveRetainedDeadBody(int deadBodyId)
        {
            for (var index = _game._retainedDeadBodies.Count - 1; index >= 0; index -= 1)
            {
                if (_game._retainedDeadBodies[index].Id == deadBodyId)
                {
                    _game._retainedDeadBodies.RemoveAt(index);
                }
            }
        }

        public void ResetRetainedDeadBodies()
        {
            _game._trackedDeadBodyVisuals.Clear();
            _game._retainedDeadBodies.Clear();
            _game._staleTrackedDeadBodyIds.Clear();
        }

        public void ResetImmediateNetworkDeadBodies()
        {
            _game._immediateNetworkDeadBodies.Clear();
            _game._staleImmediateNetworkDeadBodyPlayerIds.Clear();
        }

        public void AdvanceImmediateNetworkDeadBodies()
        {
            _game._staleImmediateNetworkDeadBodyPlayerIds.Clear();
            foreach (var entry in _game._immediateNetworkDeadBodies)
            {
                _game._staleImmediateNetworkDeadBodyPlayerIds.Add(entry.Key);
            }

            for (var index = 0; index < _game._staleImmediateNetworkDeadBodyPlayerIds.Count; index += 1)
            {
                var sourcePlayerId = _game._staleImmediateNetworkDeadBodyPlayerIds[index];
                if (!_game._immediateNetworkDeadBodies.TryGetValue(sourcePlayerId, out var deadBody))
                {
                    continue;
                }

                var nextTicksRemaining = deadBody.TicksRemaining - 1;
                if (nextTicksRemaining <= 0)
                {
                    _game._immediateNetworkDeadBodies.Remove(sourcePlayerId);
                    continue;
                }

                _game._immediateNetworkDeadBodies[sourcePlayerId] = deadBody with { TicksRemaining = nextTicksRemaining };
            }

            _game._staleImmediateNetworkDeadBodyPlayerIds.Clear();
        }

        public void QueueImmediateNetworkDeathPresentation(SnapshotMessage resolvedSnapshot, SnapshotDamageEvent damageEvent)
        {
            if (!damageEvent.WasFatal || damageEvent.TargetKind != (byte)OpenGarrison.Core.DamageTargetKind.Player)
            {
                return;
            }

            _game._gameplayGoreEffectsController.SpawnImmediateFatalDamageVisuals(damageEvent.X, damageEvent.Y, damageEvent.Amount);

            var targetPlayer = _game.FindPlayerById(damageEvent.TargetEntityId);
            if (targetPlayer is not null
                && TryQueueImmediateNetworkGibPresentation(resolvedSnapshot, damageEvent, targetPlayer))
            {
                return;
            }

            if (((DamageEventFlags)damageEvent.Flags).HasFlag(DamageEventFlags.Gibbed))
            {
                TryQueueImmediateNetworkGibPresentationFromSnapshot(resolvedSnapshot, damageEvent, targetPlayer);
                return;
            }

            var plannedDeadBody = ImmediateNetworkDeathPresentationPlanner.TryCreate(
                resolvedSnapshot,
                damageEvent,
                targetPlayer,
                ImmediateNetworkDeadBodyLifetimeTicks);
            if (!plannedDeadBody.HasValue)
            {
                return;
            }

            var deadBody = plannedDeadBody.Value;
            _game._immediateNetworkDeadBodies[deadBody.SourcePlayerId] = new ImmediateNetworkDeadBodyVisual(
                deadBody.SourcePlayerId,
                deadBody.ClassId,
                deadBody.Team,
                deadBody.AnimationKind,
                deadBody.X,
                deadBody.Y,
                deadBody.Width,
                deadBody.Height,
                deadBody.FacingLeft,
                deadBody.TicksRemaining);
        }

        private bool TryQueueImmediateNetworkGibPresentation(SnapshotMessage resolvedSnapshot, SnapshotDamageEvent damageEvent, PlayerEntity targetPlayer)
        {
            for (var index = 0; index < resolvedSnapshot.Players.Count; index += 1)
            {
                var snapshotPlayer = resolvedSnapshot.Players[index];
                if (snapshotPlayer.PlayerId != damageEvent.TargetEntityId)
                {
                    continue;
                }

                if (!snapshotPlayer.IsAlive
                    && snapshotPlayer.GibDeaths > targetPlayer.GibDeaths
                    && _game._world.TryPresentNetworkGibDeath(damageEvent.TargetEntityId, snapshotPlayer.GibDeaths, damageEvent.X, damageEvent.Y))
                {
                    _game.PlayPredictedGibSound(damageEvent.X, damageEvent.Y);
                    return true;
                }

                return false;
            }

            return false;
        }

        private bool TryQueueImmediateNetworkGibPresentationFromSnapshot(SnapshotMessage resolvedSnapshot, SnapshotDamageEvent damageEvent, PlayerEntity? targetPlayer)
        {
            for (var index = 0; index < resolvedSnapshot.Players.Count; index += 1)
            {
                var snapshotPlayer = resolvedSnapshot.Players[index];
                if (snapshotPlayer.PlayerId != damageEvent.TargetEntityId)
                {
                    continue;
                }

                if (targetPlayer is not null
                    && snapshotPlayer.GibDeaths > targetPlayer.GibDeaths
                    && _game._world.TryPresentNetworkGibDeath(damageEvent.TargetEntityId, snapshotPlayer.GibDeaths, damageEvent.X, damageEvent.Y))
                {
                    _game.PlayPredictedGibSound(damageEvent.X, damageEvent.Y);
                    return true;
                }

                var presentationPlayer = CreateSnapshotGibPresentationPlayer(snapshotPlayer, targetPlayer);
                _game._world.SpawnClientPlayerGibsFromNetworkDeath(presentationPlayer, damageEvent.X, damageEvent.Y);
                _game.PlayPredictedGibSound(damageEvent.X, damageEvent.Y);
                return true;
            }

            return false;
        }

        private static PlayerEntity CreateSnapshotGibPresentationPlayer(SnapshotPlayerState snapshotPlayer, PlayerEntity? targetPlayer)
        {
            var classId = (PlayerClass)snapshotPlayer.ClassId;
            var player = new PlayerEntity(
                snapshotPlayer.PlayerId,
                CharacterClassCatalog.GetDefinition(classId),
                snapshotPlayer.Name);
            player.ApplyNetworkState(
                (PlayerTeam)snapshotPlayer.Team,
                targetPlayer?.ClassDefinition ?? CharacterClassCatalog.GetDefinition(classId),
                snapshotPlayer.IsAlive,
                snapshotPlayer.X,
                snapshotPlayer.Y,
                snapshotPlayer.HorizontalSpeed,
                snapshotPlayer.VerticalSpeed,
                snapshotPlayer.Health,
                snapshotPlayer.Ammo,
                snapshotPlayer.Kills,
                snapshotPlayer.Deaths,
                snapshotPlayer.Caps,
                snapshotPlayer.Points,
                snapshotPlayer.HealPoints,
                snapshotPlayer.ActiveDominationCount,
                snapshotPlayer.IsDominatingLocalViewer,
                snapshotPlayer.IsDominatedByLocalViewer,
                snapshotPlayer.Metal,
                snapshotPlayer.IsGrounded,
                snapshotPlayer.RemainingAirJumps,
                snapshotPlayer.IsCarryingIntel,
                snapshotPlayer.IntelRechargeTicks,
                snapshotPlayer.IsSpyCloaked,
                snapshotPlayer.SpyCloakAlpha,
                snapshotPlayer.IsSpySuperjumping,
                snapshotPlayer.SpySuperjumpHorizontalVelocity,
                snapshotPlayer.SpySuperjumpCooldownTicksRemaining,
                snapshotPlayer.SpyBackstabVisualTicksRemaining,
                snapshotPlayer.IsUbered,
                snapshotPlayer.IsKritzCritBoosted,
                snapshotPlayer.IsHeavyEating,
                snapshotPlayer.HeavyEatTicksRemaining,
                snapshotPlayer.IsSniperScoped,
                0,
                snapshotPlayer.IsUsingBinoculars,
                snapshotPlayer.BinocularsFocusX,
                snapshotPlayer.BinocularsFocusY,
                snapshotPlayer.FacingDirectionX,
                snapshotPlayer.AimDirectionDegrees,
                snapshotPlayer.AimWorldX,
                snapshotPlayer.AimWorldY,
                snapshotPlayer.IsTaunting,
                0f,
                snapshotPlayer.IsChatBubbleVisible,
                snapshotPlayer.ChatBubbleFrameIndex,
                snapshotPlayer.ChatBubbleAlpha,
                snapshotPlayer.BurnIntensity,
                snapshotPlayer.BurnDurationSourceTicks,
                snapshotPlayer.BurnDecayDelaySourceTicksRemaining,
                snapshotPlayer.BurnIntensityDecayPerSourceTick,
                snapshotPlayer.BurnedByPlayerId,
                snapshotPlayer.MovementState,
                snapshotPlayer.PrimaryCooldownTicks,
                snapshotPlayer.ReloadTicksUntilNextShell,
                snapshotPlayer.MedicNeedleCooldownTicks,
                snapshotPlayer.MedicNeedleRefillTicks,
                snapshotPlayer.PyroAirblastCooldownTicks,
                snapshotPlayer.PyroFlareCooldownTicks,
                snapshotPlayer.PyroPrimaryFuelScaled,
                snapshotPlayer.IsPyroPrimaryRefilling,
                snapshotPlayer.PyroFlameLoopTicksRemaining,
                snapshotPlayer.PyroPrimaryRequiresReleaseAfterEmpty,
                snapshotPlayer.HeavyEatCooldownTicksRemaining,
                snapshotPlayer.Assists,
                snapshotPlayer.BadgeMask,
                snapshotPlayer.IsMedicHealing,
                snapshotPlayer.MedicHealTargetId,
                snapshotPlayer.MedicUberCharge,
                snapshotPlayer.IsMedicUberReady,
                snapshotPlayer.GameplayModPackId,
                snapshotPlayer.GameplayLoadoutId,
                snapshotPlayer.GameplayPrimaryItemId,
                snapshotPlayer.GameplaySecondaryItemId,
                snapshotPlayer.GameplayUtilityItemId,
                snapshotPlayer.GameplayEquippedSlot,
                snapshotPlayer.GameplayEquippedItemId,
                snapshotPlayer.GameplayAcquiredItemId,
                snapshotPlayer.OwnedGameplayItemIds,
                replicatedStateEntries: null,
                snapshotPlayer.PlayerScale,
                offhandCooldownTicks: snapshotPlayer.OffhandCooldownTicks,
                offhandReloadTicks: snapshotPlayer.OffhandReloadTicks,
                gibDeaths: snapshotPlayer.GibDeaths,
                isTypingChatMessage: snapshotPlayer.IsTypingChatMessage);
            return player;
        }

        public ClientDeadBodyAnimationKind ResolveClientPluginDeadBodyAnimationKind(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind)
        {
            if (TryGetForcedLastToDieDeadBodyAnimationKind(sourcePlayerId, classId, team, animationKind, out var forcedAnimationKind))
            {
                return forcedAnimationKind;
            }

            return ToClientDeadBodyAnimationKind(animationKind);
        }

        public void DrawDeadBodyVisual(int id, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float x, float y, float width, float height, bool facingLeft, int ticksRemaining, Vector2 cameraPosition)
        {
            DrawDeadBodyVisualCore(id, sourcePlayerId, classId, team, animationKind, x, y, width, height, facingLeft, ticksRemaining, cameraPosition);
        }

        public bool TryGetForcedLastToDieDeadBodyAnimationKind(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind deadBodyAnimationKind, out ClientDeadBodyAnimationKind forcedAnimationKind)
        {
            return TryGetForcedLastToDieDeadBodyAnimationKindCore(sourcePlayerId, classId, team, deadBodyAnimationKind, out forcedAnimationKind);
        }

        private void DrawDeadBodyVisualCore(int id, int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind animationKind, float x, float y, float width, float height, bool facingLeft, int ticksRemaining, Vector2 cameraPosition)
        {
            var renderPosition = new Vector2(x, y);
            var pluginAnimationKind = ResolveClientPluginDeadBodyAnimationKind(sourcePlayerId, classId, team, animationKind);
            if (_game.TryDrawClientPluginDeadBody(cameraPosition, new ClientDeadBodyRenderState(id, ToClientPluginClass(classId), ToClientPluginTeam(team), renderPosition, width, height, facingLeft, ticksRemaining, pluginAnimationKind)))
            {
                return;
            }

            var spriteName = Game1.GetDeadBodySpriteName(classId, team, animationKind);
            if (spriteName is not null)
            {
                var sprite = _game.GetResolvedSprite(spriteName);
                if (sprite is not null && sprite.Frames.Count > 0)
                {
                    var roundedOrigin = GetRoundedPlayerSpriteOrigin(renderPosition);
                    var frame = sprite.Frames[0];
                    var corpseOrigin = new Vector2(frame.Width * 0.5f, frame.Height - (height * 0.5f));
                    var drawPosition = new Vector2(roundedOrigin.X - cameraPosition.X, roundedOrigin.Y - cameraPosition.Y);

                    _game.DrawSpriteFrameWithOptionalShadow(frame, drawPosition, Color.White, 0f, corpseOrigin, new Vector2(facingLeft ? -1f : 1f, 1f));
                    return;
                }
            }

            var rectangle = new Rectangle((int)(renderPosition.X - (width / 2f) - cameraPosition.X), (int)(renderPosition.Y - (height / 2f) - cameraPosition.Y), (int)width, (int)height);
            _game._spriteBatch.Draw(_game._pixel, rectangle, team == PlayerTeam.Blue ? new Color(24, 45, 80) : new Color(90, 30, 30));
        }

        private bool TryGetForcedLastToDieDeadBodyAnimationKindCore(int sourcePlayerId, PlayerClass classId, PlayerTeam team, DeadBodyAnimationKind deadBodyAnimationKind, out ClientDeadBodyAnimationKind forcedAnimationKind)
        {
            if (!_game.IsLastToDieSessionActive || _game._networkClient.IsSpectator || sourcePlayerId != _game._world.LocalPlayer.Id || team != PlayerTeam.Red || deadBodyAnimationKind == DeadBodyAnimationKind.Decapitated)
            {
                forcedAnimationKind = default;
                return false;
            }

            forcedAnimationKind = classId switch
            {
                PlayerClass.Soldier => ClientDeadBodyAnimationKind.Rifle,
                PlayerClass.Demoman => ClientDeadBodyAnimationKind.Rifle,
                _ => default,
            };
            return forcedAnimationKind != default;
        }
    }
}
