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
            var recoilTicks = ResolveWhippingCordRecoilTicks(attacker);
            var windupTicks = WhippingCordCatalog.ResolveWindupTicks(recoilTicks);
            var swingTicks = WhippingCordCatalog.ResolveSwingTicks(recoilTicks);
            attacker.BeginWhippingCordWindup(windupTicks, swingTicks);
            AdvanceWhippingCordSwing(attacker, aimWorldX, aimWorldY);
        }

        public void AdvanceWhippingCordSwing(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            if (!attacker.IsWhippingCordAttackActive)
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

                ProcessWhippingCordSwingTick(attacker, aimWorldX, aimWorldY);
                attacker.AdvanceWhippingCordSwingTimer();
            }
        }

        private void ProcessWhippingCordSwingTick(PlayerEntity attacker, float aimWorldX, float aimWorldY)
        {
            var geometryScale = MathF.Max(0.1f, attacker.LastToDieUniversalModifiers.MeleeScale);
            var maskScale = geometryScale;
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
            var facingLeft = MathF.Cos(aimRadians) < 0f;
            var hitboxSpriteName = ResolveWhippingCordMeleeHitboxSpriteName(attacker);
            var hitboxMask = MeleeHitboxMaskCatalog.GetOrLoad(hitboxSpriteName);
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

            if (!attacker.TryMarkWhippingCordSwingImpact())
            {
                return;
            }

            var wallHit = ResolveRifleHit(
                attacker,
                anchorX,
                anchorY,
                directionX,
                directionY,
                hitboxMask.MaxReachFromOrigin * maskScale);
            if (wallHit.HitPlayer is null
                && wallHit.HitSentry is null
                && wallHit.HitGenerator is null
                && wallHit.Distance < hitboxMask.MaxReachFromOrigin * maskScale)
            {
                RegisterImpactEffect(
                    anchorX + directionX * wallHit.Distance,
                    anchorY + directionY * wallHit.Distance,
                    PointDirectionDegrees(0f, 0f, directionX, directionY));
            }
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

        private void ApplyWhippingCordBuildingSupport(PlayerEntity attacker, SentryEntity sentry)
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

        private void ApplyWhippingCordJumpPadSupport(PlayerEntity attacker, JumpPadEntity pad)
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

        private void TryRepairWhippingCordStructure(
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

        private void TryGrantWhippingCordAutogunOverdrive(PlayerEntity attacker, SentryEntity sentry)
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

        private static int ResolveWhippingCordRecoilTicks(PlayerEntity attacker)
        {
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (!string.IsNullOrWhiteSpace(itemId)
                && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item))
            {
                return item.Presentation.RecoilDurationSourceTicks;
            }

            return WhippingCordCatalog.RecoilDurationSourceTicks;
        }

        private static string ResolveWhippingCordMeleeHitboxSpriteName(PlayerEntity attacker)
        {
            var itemId = attacker.GameplayLoadoutState.PrimaryItemId;
            if (!string.IsNullOrWhiteSpace(itemId)
                && CharacterClassCatalog.RuntimeRegistry.TryGetItem(itemId, out var item)
                && !string.IsNullOrWhiteSpace(item.Presentation.MeleeHitboxSpriteName))
            {
                return item.Presentation.MeleeHitboxSpriteName!;
            }

            return WhippingCordCatalog.MeleeHitboxSpriteName;
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

            var facingScale = facingLeft ? -1f : 1f;
            offsetX = item.Presentation.WeaponOffsetX * facingScale;
            offsetY = item.Presentation.WeaponOffsetY;
        }
    }
}
