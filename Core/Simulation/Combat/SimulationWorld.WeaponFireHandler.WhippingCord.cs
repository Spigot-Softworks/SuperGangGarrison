using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    internal GameplayPrimaryWeaponResult ExecuteWhippingCordPrimaryWeapon(GameplayPrimaryWeaponContext context)
    {
        WeaponHandler.StartWhippingCordSwing(context.Player, context.AimWorldX, context.AimWorldY);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    private sealed partial class WeaponFireHandler
    {
        public void StartWhippingCordSwing(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var recoilTicks = WhippingCordCatalog.ResolveRecoilTicks(attacker);
            var windupTicks = WhippingCordCatalog.ResolveWindupTicks(recoilTicks);
            var swingTicks = WhippingCordCatalog.ResolveSwingTicks(recoilTicks);
            var backswingTicks = WhippingCordCatalog.ResolveBackswingTicks(recoilTicks);
            attacker.BeginWhippingCordWindup(windupTicks, swingTicks, backswingTicks);
            AdvanceWhippingCordSwing(attacker, aimWorldX, aimWorldY, fireHeld: true);
        }

        public void AdvanceWhippingCordSwing(PlayerEntity attacker, float aimWorldX, float aimWorldY, bool fireHeld)
        {
            if (attacker.IsWhippingCordLatched || !attacker.IsWhippingCordAttackActive)
            {
                return;
            }

            if (attacker.TryEnterWhippingCordDamageWindow())
            {
                // First attack frame (after wind-up): punchy hit cue.
                if (attacker.TryMarkWhippingCordAttackSound())
                {
                    RegisterSoundEvent(attacker, WhippingCordCatalog.AttackSoundName);
                }

                ProcessWhippingCordSwingTick(attacker, aimWorldX, aimWorldY, fireHeld);
                attacker.AdvanceWhippingCordSwingTimer();
            }
            else if (attacker.IsWhippingCordBackswingActive)
            {
                ProcessWhippingCordBackswingTick(attacker);
                attacker.AdvanceWhippingCordBackswingTimer();
            }
        }

        private void ProcessWhippingCordSwingTick(PlayerEntity attacker, float aimWorldX, float aimWorldY, bool fireHeld)
        {
            var geometryScale = MathF.Max(0.1f, attacker.LastToDieUniversalModifiers.MeleeScale);
            var maskScale = geometryScale * attacker.PlayerScale;
            var aimDeltaX = aimWorldX - attacker.X;
            var aimDeltaY = aimWorldY - attacker.Y;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = attacker.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                return;
            }

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var aimRadians = attacker.AimDirectionDegrees * (MathF.PI / 180f);
            var facingLeft = DeterministicMath.Cos(aimRadians) < 0f;
            var hitboxSpriteName = ResolveWhippingCordMeleeHitboxSpriteName(attacker);
            var hitboxMask = MeleeHitboxMaskCatalog.GetOrLoad(
                hitboxSpriteName, WhippingCordCatalog.ExtendedWhipFrameIndex);
            if (hitboxMask is null)
            {
                return;
            }

            var rotateWithAim = ResolveWhippingCordRotateHitbox(attacker);
            ResolveWhippingCordDrawOffset(attacker, facingLeft, out var offsetX, out var offsetY);
            // Companion-offset weapons pin at body origin + weaponOffset (same as live draw).
            // Do not add torso sit-down — that is for full torso replacements only.
            var anchorX = attacker.X + offsetX;
            var anchorY = attacker.Y + offsetY;

            if (TryProcessWhippingCordFriendlyBuildingHit(
                    attacker,
                    hitboxMask,
                    anchorX,
                    anchorY,
                    facingLeft,
                    aimRadians,
                    maskScale,
                    rotateWithAim))
            {
                return;
            }

            var result = ResolveWhippingCordMeleeAreaHit(
                attacker,
                hitboxMask,
                anchorX,
                anchorY,
                facingLeft,
                aimRadians,
                maskScale,
                rotateWithAim);
            RegisterCombatTrace(
                anchorX,
                anchorY,
                directionX,
                directionY,
                result.Distance,
                result.HitPlayer is not null,
                attacker.Team);

            var damage = attacker.GetWhippingCordDamage();
            if (damage <= 0)
            {
                return;
            }

            if (result.HitPlayer is not null)
            {
                var targetWasGrounded = result.HitPlayer.IsGrounded;
                RegisterBloodEffect(
                    result.HitPlayer.X,
                    result.HitPlayer.Y,
                    PointDirectionDegrees(anchorX, anchorY, result.HitPlayer.X, result.HitPlayer.Y) - 180f,
                    count: 1);
                if (!result.HitPlayer.IsUbered)
                {
                    result.HitPlayer.AddImpulse(
                        directionX * 2.5f * LegacyMovementModel.SourceTicksPerSecond,
                        -1.5f * LegacyMovementModel.SourceTicksPerSecond);
                }

                if (ApplyPlayerDamage(
                        result.HitPlayer,
                        damage,
                        attacker,
                        PlayerEntity.SpyDamageRevealAlpha,
                        targetWasGrounded: targetWasGrounded))
                {
                    KillPlayer(
                        result.HitPlayer,
                        killer: attacker,
                        weaponSpriteName: WhippingCordCatalog.KillFeedSpriteName,
                        deadBodyAnimationKind: DeadBodyAnimationKind.Default);
                }

                return;
            }

            if (result.HitSentry is not null)
            {
                if (ApplySentryDamage(result.HitSentry, damage, attacker))
                {
                    DestroySentry(result.HitSentry, attacker);
                }

                return;
            }

            if (result.HitGenerator is not null)
            {
                TryDamageGenerator(result.HitGenerator.Team, damage, attacker);
                return;
            }

            float terrainX;
            float terrainY;
            if (fireHeld)
            {
                if (!_world.TryLatchWhippingCordToTerrain(attacker, aimWorldX, aimWorldY))
                {
                    return;
                }

                terrainX = attacker.WhippingCordAnchorX;
                terrainY = attacker.WhippingCordAnchorY;
            }
            else if (!_world.TryFindWhippingCordTerrainContact(
                         attacker, aimWorldX, aimWorldY, out terrainX, out terrainY))
            {
                return;
            }

            if (attacker.TryMarkWhippingCordSwingImpact())
            {
                RegisterImpactEffect(
                    terrainX,
                    terrainY,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
        }

        private void ProcessWhippingCordBackswingTick(PlayerEntity attacker)
        {
            if (attacker.PendingWhippingCordBackswingTargetId < 0)
            {
                var firstTouched = FindWhippingCordBackswingTarget(attacker);
                if (firstTouched is not null)
                {
                    _ = attacker.TryQueueWhippingCordBackswingTarget(firstTouched.Id);
                }
            }

            // Hold the first touched enemy until the whip has visibly begun its
            // return. Applying the impulse on the very first backswing tick
            // moved players while the extended strike frame was still shown.
            if (attacker.WhippingCordBackswingProgress < WhippingCordCatalog.BackswingPullStartProgress)
            {
                return;
            }

            PlayerEntity? target = null;
            foreach (var player in _world.EnumerateSimulatedPlayers())
            {
                if (player.Id == attacker.PendingWhippingCordBackswingTargetId)
                {
                    target = player;
                    break;
                }
            }

            if (target is null || !target.IsAlive
                || !_world.CanTeamDamagePlayer(attacker.Team, attacker.Id, target))
            {
                attacker.ClearPendingWhippingCordBackswingTarget();
                return;
            }

            if (!attacker.TryClaimWhippingCordBackswingTarget())
            {
                return;
            }

            var pullX = attacker.X - target.X;
            var pullY = attacker.Y - target.Y;
            var pullDistance = MathF.Sqrt(pullX * pullX + pullY * pullY);
            if (pullDistance > 0.0001f)
            {
                var pullSpeed = WhippingCordCatalog.BackswingPullSpeedPerTick
                    * LegacyMovementModel.SourceTicksPerSecond;
                target.AddImpulse(pullX / pullDistance * pullSpeed, pullY / pullDistance * pullSpeed);
            }
        }

        private PlayerEntity? FindWhippingCordBackswingTarget(PlayerEntity attacker)
        {
            var hitboxMask = MeleeHitboxMaskCatalog.GetOrLoad(
                ResolveWhippingCordMeleeHitboxSpriteName(attacker),
                WhippingCordCatalog.ExtendedWhipFrameIndex);
            if (hitboxMask is null)
            {
                return null;
            }

            var aimRadians = attacker.AimDirectionDegrees * MathF.PI / 180f;
            var facingLeft = DeterministicMath.Cos(aimRadians) < 0f;
            ResolveWhippingCordDrawOffset(attacker, facingLeft, out var offsetX, out var offsetY);
            var originX = attacker.X + offsetX;
            var originY = attacker.Y + offsetY;
            var scale = MathF.Max(0.1f, attacker.LastToDieUniversalModifiers.MeleeScale)
                * attacker.WhippingCordBackswingReachScale * attacker.PlayerScale;
            var rotateWithAim = ResolveWhippingCordRotateHitbox(attacker);
            var maxDistance = hitboxMask.MaxReachFromOrigin * scale;
            PlayerEntity? nearest = null;
            var nearestDistance = maxDistance;
            foreach (var target in _world.EnumerateSimulatedPlayers())
            {
                if (!target.IsAlive || target.Id == attacker.Id
                    || !_world.CanTeamDamagePlayer(attacker.Team, attacker.Id, target))
                {
                    continue;
                }

                _world.GetCachedPlayerPresentationHitBounds(target, out var left, out var top, out var right, out var bottom);
                if (!hitboxMask.OverlapsRectangle(
                        left, top, right, bottom, originX, originY, facingLeft, scale,
                        rotateWithAim ? aimRadians : null))
                {
                    continue;
                }

                var distance = DistanceBetween(originX, originY, target.X, target.Y);
                if (distance < nearestDistance)
                {
                    var rayX = (target.X - originX) / MathF.Max(distance, 0.0001f);
                    var rayY = (target.Y - originY) / MathF.Max(distance, 0.0001f);
                    var firstHit = ResolveRifleHit(attacker, originX, originY, rayX, rayY, distance);
                    if (ReferenceEquals(firstHit.HitPlayer, target))
                    {
                        nearest = target;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private bool TryProcessWhippingCordFriendlyBuildingHit(
            PlayerEntity attacker,
            MeleeHitboxMask mask,
            float anchorX,
            float anchorY,
            bool facingLeft,
            float aimRadians,
            float maskScale,
            bool rotateWithAim)
        {
            for (var sentryIndex = 0; sentryIndex < _world._sentries.Count; sentryIndex += 1)
            {
                var sentry = _world._sentries[sentryIndex];
                if (sentry.Team != attacker.Team
                    || attacker.HasWhippingCordHitSentry(sentry.Id))
                {
                    continue;
                }

                var left = sentry.X - (SentryEntity.Width / 2f);
                var top = sentry.Y - (SentryEntity.Height / 2f);
                var right = sentry.X + (SentryEntity.Width / 2f);
                var bottom = sentry.Y + (SentryEntity.Height / 2f);
                if (!mask.OverlapsRectangle(
                        left,
                        top,
                        right,
                        bottom,
                        anchorX,
                        anchorY,
                        facingLeft,
                        maskScale,
                        rotateWithAim ? aimRadians : null))
                {
                    continue;
                }

                _ = attacker.TryMarkWhippingCordHitSentry(sentry.Id);
                ApplyWhippingCordBuildingSupport(attacker, sentry);
                return true;
            }

            for (var padIndex = 0; padIndex < _world._jumpPads.Count; padIndex += 1)
            {
                var pad = _world._jumpPads[padIndex];
                if (pad.IsNeutral
                    || pad.Team != attacker.Team
                    || attacker.HasWhippingCordHitJumpPad(pad.Id))
                {
                    continue;
                }

                var left = pad.X - (JumpPadEntity.Width / 2f);
                var top = pad.Y - (JumpPadEntity.Height / 2f);
                var right = pad.X + (JumpPadEntity.Width / 2f);
                var bottom = pad.Y + (JumpPadEntity.Height / 2f);
                if (!mask.OverlapsRectangle(
                        left,
                        top,
                        right,
                        bottom,
                        anchorX,
                        anchorY,
                        facingLeft,
                        maskScale,
                        rotateWithAim ? aimRadians : null))
                {
                    continue;
                }

                _ = attacker.TryMarkWhippingCordHitJumpPad(pad.Id);
                ApplyWhippingCordJumpPadSupport(attacker, pad);
                return true;
            }

            return false;
        }

        private static void ApplyWhippingCordBuildingSupport(PlayerEntity attacker, SentryEntity sentry)
        {
            if (!sentry.IsBuilt)
            {
                sentry.GrantConstructionBoost(WhippingCordCatalog.ConstructionSpeedMultiplier);
                return;
            }

            if (sentry.Health < sentry.MaxHealth)
            {
                TryRepairWhippingCordStructure(
                    attacker,
                    sentry.MaxHealth,
                    sentry.Health,
                    sentry.IsDispenser
                        ? WhippingCordCatalog.DispenserFullRepairMetalCost
                        : WhippingCordCatalog.AutogunFullRepairMetalCost,
                    healAmount => sentry.Heal(healAmount));
                return;
            }

            if (!sentry.IsDispenser)
            {
                TryGrantWhippingCordAutogunOverdrive(attacker, sentry);
            }
        }

        private static void ApplyWhippingCordJumpPadSupport(PlayerEntity attacker, JumpPadEntity pad)
        {
            if (!pad.IsBuilt)
            {
                pad.GrantConstructionBoost(WhippingCordCatalog.ConstructionSpeedMultiplier);
                return;
            }

            if (pad.Health < JumpPadEntity.MaxHealth)
            {
                TryRepairWhippingCordStructure(
                    attacker,
                    JumpPadEntity.MaxHealth,
                    pad.Health,
                    WhippingCordCatalog.JumpPadFullRepairMetalCost,
                    healAmount => pad.Heal(healAmount));
            }
        }

        private static void TryRepairWhippingCordStructure(
            PlayerEntity attacker,
            int maxHealth,
            int currentHealth,
            int fullRepairMetalCost,
            Action<int> heal)
        {
            var missing = maxHealth - currentHealth;
            if (missing <= 0 || maxHealth <= 0)
            {
                return;
            }

            var fullCost = Math.Max(1, fullRepairMetalCost);
            var affordableMissing = missing;
            var metalAvailable = attacker.Metal;
            if (metalAvailable <= 0f)
            {
                return;
            }

            var costPerHp = fullCost / (float)maxHealth;
            var maxAffordableHp = (int)MathF.Floor(metalAvailable / costPerHp);
            if (maxAffordableHp <= 0)
            {
                return;
            }

            affordableMissing = Math.Min(missing, maxAffordableHp);
            var metalCost = affordableMissing * costPerHp;
            if (!attacker.SpendMetal(metalCost))
            {
                return;
            }

            heal(affordableMissing);
        }

        private static void TryGrantWhippingCordAutogunOverdrive(PlayerEntity attacker, SentryEntity sentry)
        {
            if (sentry.IsOverdriveActive)
            {
                return;
            }

            if (!attacker.SpendMetal(WhippingCordCatalog.AutogunOverdriveMetalCost))
            {
                return;
            }

            sentry.GrantOverdrive(WhippingCordCatalog.AutogunOverdriveDurationSourceTicks);
        }

        private RifleHitResult ResolveWhippingCordMeleeAreaHit(
            PlayerEntity attacker,
            MeleeHitboxMask mask,
            float anchorX,
            float anchorY,
            bool facingLeft,
            float aimRadians,
            float maskScale,
            bool rotateWithAim)
        {
            float? rotation = rotateWithAim ? aimRadians : null;
            var maxDistance = mask.MaxReachFromOrigin * maskScale;
            PlayerEntity? nearestPlayer = null;
            var nearestPlayerDistance = maxDistance;
            foreach (var player in _world.EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive
                    || player.Id == attacker.Id
                    || attacker.HasWhippingCordHitPlayer(player.Id)
                    || !_world.CanTeamDamagePlayer(attacker.Team, attacker.Id, player))
                {
                    continue;
                }

                _world.GetCachedPlayerPresentationHitBounds(player, out var left, out var top, out var right, out var bottom);
                if (!mask.OverlapsRectangle(left, top, right, bottom, anchorX, anchorY, facingLeft, maskScale, rotation))
                {
                    continue;
                }

                var hitDistance = DistanceBetween(anchorX, anchorY, player.X, player.Y);
                if (hitDistance < nearestPlayerDistance)
                {
                    nearestPlayerDistance = hitDistance;
                    nearestPlayer = player;
                }
            }

            if (nearestPlayer is not null)
            {
                _ = attacker.TryMarkWhippingCordHitPlayer(nearestPlayer.Id);
                return new RifleHitResult(nearestPlayerDistance, nearestPlayer, HitSentry: null, HitGenerator: null);
            }

            SentryEntity? nearestSentry = null;
            var nearestSentryDistance = maxDistance;
            for (var sentryIndex = 0; sentryIndex < _world._sentries.Count; sentryIndex += 1)
            {
                var sentry = _world._sentries[sentryIndex];
                if (sentry.Team == attacker.Team
                    || attacker.HasWhippingCordHitSentry(sentry.Id))
                {
                    continue;
                }

                var left = sentry.X - (SentryEntity.Width / 2f);
                var top = sentry.Y - (SentryEntity.Height / 2f);
                var right = sentry.X + (SentryEntity.Width / 2f);
                var bottom = sentry.Y + (SentryEntity.Height / 2f);
                if (!mask.OverlapsRectangle(left, top, right, bottom, anchorX, anchorY, facingLeft, maskScale, rotation))
                {
                    continue;
                }

                var hitDistance = DistanceBetween(anchorX, anchorY, sentry.X, sentry.Y);
                if (hitDistance < nearestSentryDistance)
                {
                    nearestSentryDistance = hitDistance;
                    nearestSentry = sentry;
                }
            }

            if (nearestSentry is not null)
            {
                _ = attacker.TryMarkWhippingCordHitSentry(nearestSentry.Id);
                return new RifleHitResult(nearestSentryDistance, HitPlayer: null, nearestSentry, HitGenerator: null);
            }

            GeneratorState? nearestGenerator = null;
            var nearestGeneratorDistance = maxDistance;
            for (var generatorIndex = 0; generatorIndex < _world._generators.Count; generatorIndex += 1)
            {
                var generator = _world._generators[generatorIndex];
                if (generator.Team == attacker.Team
                    || generator.IsDestroyed
                    || attacker.HasWhippingCordHitGenerator(generator.Team))
                {
                    continue;
                }

                if (!mask.OverlapsRectangle(
                        generator.Marker.Left,
                        generator.Marker.Top,
                        generator.Marker.Right,
                        generator.Marker.Bottom,
                        anchorX,
                        anchorY,
                        facingLeft,
                        maskScale,
                        rotation))
                {
                    continue;
                }

                var hitDistance = DistanceBetween(
                    anchorX,
                    anchorY,
                    (generator.Marker.Left + generator.Marker.Right) * 0.5f,
                    (generator.Marker.Top + generator.Marker.Bottom) * 0.5f);
                if (hitDistance < nearestGeneratorDistance)
                {
                    nearestGeneratorDistance = hitDistance;
                    nearestGenerator = generator;
                }
            }

            if (nearestGenerator is not null)
            {
                _ = attacker.TryMarkWhippingCordHitGenerator(nearestGenerator.Team);
                return new RifleHitResult(nearestGeneratorDistance, HitPlayer: null, HitSentry: null, nearestGenerator);
            }

            return new RifleHitResult(maxDistance, HitPlayer: null, HitSentry: null, HitGenerator: null);
        }

        private static string ResolveWhippingCordMeleeHitboxSpriteName(PlayerEntity attacker)
        {
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (!string.IsNullOrWhiteSpace(itemId)
                && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item)
                && !string.IsNullOrWhiteSpace(item.Presentation.RecoilSpriteName))
            {
                return item.Presentation.RecoilSpriteName!;
            }

            return WhippingCordCatalog.WhipRecoilSpriteName;
        }

        private static bool ResolveWhippingCordRotateHitbox(PlayerEntity attacker)
        {
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (!string.IsNullOrWhiteSpace(itemId)
                && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
            {
                return item.Presentation.RotateMeleeHitboxWithAim;
            }

            return true;
        }

        private static void ResolveWhippingCordDrawOffset(
            PlayerEntity attacker,
            bool facingLeft,
            out float offsetX,
            out float offsetY)
        {
            offsetX = 0f;
            offsetY = 0f;
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (string.IsNullOrWhiteSpace(itemId)
                || !CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
            {
                return;
            }

            var facingScale = (facingLeft ? -1f : 1f) * attacker.PlayerScale;
            offsetX = item.Presentation.WeaponOffsetX * facingScale;
            offsetY = item.Presentation.WeaponOffsetY * attacker.PlayerScale;
        }
    }
}
