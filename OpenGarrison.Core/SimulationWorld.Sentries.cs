namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float SentryBuildCost = 100f;
    private const float SentryBuildProximityRadius = 50f;
    private const float SentryDestroyBlastRadius = 65f;
    private const float SentryDestroyKnockbackPerTick = 4f;
    private readonly record struct SentryTarget(PlayerEntity? Player, GeneratorState? Generator, float X, float Y, int? PlayerId);

    public bool TryBuildLocalSentry()
    {
        return TryBuildSentry(LocalPlayer);
    }

    public bool TryDestroyLocalSentry()
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            if (sentry.OwnerPlayerId != LocalPlayer.Id)
            {
                continue;
            }

            DestroySentry(sentry);
            return true;
        }

        return false;
    }

    private void AdvanceSentries()
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var owner = FindPlayerById(sentry.OwnerPlayerId);
            if (owner is null || owner.ClassId != PlayerClass.Engineer || owner.Team != sentry.Team)
            {
                DestroySentry(sentry);
                continue;
            }

            var wasLanded = sentry.HasLanded;
            sentry.Advance(Level, Bounds);
            if (!wasLanded && sentry.HasLanded)
            {
                RegisterWorldSoundEvent("SentryFloorSnd", sentry.X, sentry.Y);
                RegisterWorldSoundEvent("SentryBuildSnd", sentry.X, sentry.Y);
            }
            if (!sentry.IsBuilt)
            {
                continue;
            }

            var target = AcquireSentryTarget(sentry);
            var previousTargetId = sentry.CurrentTargetPlayerId;
            sentry.SetTarget(target?.PlayerId, target?.X ?? sentry.X + sentry.FacingDirectionX, target.HasValue);
            if (!target.HasValue)
            {
                continue;
            }

            if (previousTargetId != target.Value.PlayerId && sentry.BeginTargetAlert())
            {
                RegisterWorldSoundEvent("SentryAlert", sentry.X, sentry.Y);
                continue;
            }

            if (!sentry.CanFire())
            {
                continue;
            }

            sentry.FireAt(target.Value.X, target.Value.Y);
            RegisterWorldSoundEvent("ShotgunSnd", sentry.X, sentry.Y);
            var ownerPlayer = FindPlayerById(sentry.OwnerPlayerId);
            var distance = DistanceBetween(sentry.X, sentry.Y, target.Value.X, target.Value.Y);
            RegisterCombatTrace(
                sentry.X,
                sentry.Y,
                (target.Value.X - sentry.X) / distance,
                (target.Value.Y - sentry.Y) / distance,
                distance,
                target.Value.Player is not null);
            if (target.Value.Player is not null)
            {
                RegisterBloodEffect(target.Value.Player.X, target.Value.Player.Y, sentry.AimDirectionDegrees - 180f, 2);
                if (ApplyPlayerDamage(target.Value.Player, SentryEntity.HitDamage, ownerPlayer, PlayerEntity.SpyDamageRevealAlpha))
                {
                    KillPlayer(target.Value.Player, killer: ownerPlayer, weaponSpriteName: "TurretKL", deathCamMessage: "You were killed by the autogun of", deathCamSentry: sentry);
                }
            }
            else if (target.Value.Generator is not null)
            {
                TryDamageGenerator(target.Value.Generator.Team, SentryEntity.HitDamage, ownerPlayer);
            }
        }
    }

    private void AdvanceSentryGibs()
    {
        for (var gibIndex = _sentryGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _sentryGibs[gibIndex];
            gib.AdvanceOneTick();
            var pickedUp = false;
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive || player.ClassId != PlayerClass.Engineer || player.Metal >= player.MaxMetal)
                {
                    continue;
                }

                if (!player.IntersectsMarker(gib.X, gib.Y, SentryGibEntity.PickupRadius, SentryGibEntity.PickupRadius))
                {
                    continue;
                }

                player.AddMetal(SentryGibEntity.MetalValue);
                pickedUp = true;
                break;
            }

            if (!pickedUp && !gib.IsExpired)
            {
                continue;
            }

            _entities.Remove(gib.Id);
            _sentryGibs.RemoveAt(gibIndex);
        }
    }

    private SentryTarget? AcquireSentryTarget(SentryEntity sentry)
    {
        SentryTarget? nearestTarget = null;
        var nearestDistance = float.MaxValue;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team == sentry.Team)
            {
                continue;
            }

            var distance = DistanceBetween(sentry.X, sentry.Y, player.X, player.Y);
            if (distance > SentryEntity.TargetRange || distance >= nearestDistance)
            {
                continue;
            }

            var targetAngle = PointDirectionDegrees(sentry.X, sentry.Y, player.X, player.Y);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc && !player.IntersectsMarker(sentry.X, sentry.Y, SentryEntity.Width, SentryEntity.Height))
            {
                continue;
            }

            if (!HasSentryLineOfSight(sentry, player))
            {
                continue;
            }

            nearestTarget = new SentryTarget(player, null, player.X, player.Y, player.Id);
            nearestDistance = distance;
        }

        for (var index = 0; index < _generators.Count; index += 1)
        {
            var generator = _generators[index];
            if (generator.Team == sentry.Team || generator.IsDestroyed)
            {
                continue;
            }

            var distance = DistanceBetween(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance > SentryEntity.TargetRange || distance >= nearestDistance)
            {
                continue;
            }

            var targetAngle = PointDirectionDegrees(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc)
            {
                continue;
            }

            if (!HasObstacleLineOfSight(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY))
            {
                continue;
            }

            nearestTarget = new SentryTarget(null, generator, generator.Marker.CenterX, generator.Marker.CenterY, null);
            nearestDistance = distance;
        }

        return nearestTarget;
    }

    private void DestroySentry(SentryEntity sentry)
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (!ReferenceEquals(_sentries[sentryIndex], sentry))
            {
                continue;
            }

            ReleaseMinesFromSentry(sentry);
            ApplySentryDestroyBlastToOwner(sentry);
            _entities.Remove(sentry.Id);
            _sentries.RemoveAt(sentryIndex);
            RegisterWorldSoundEvent("ExplosionSnd", sentry.X, sentry.Y);
            RegisterVisualEffect("Explosion", sentry.X, sentry.Y);
            SpawnSentryGibs(sentry.Team, sentry.X, sentry.Y);
            break;
        }
    }

    private void ReleaseMinesFromSentry(SentryEntity sentry)
    {
        var left = sentry.X - (SentryEntity.Width / 2f);
        var right = sentry.X + (SentryEntity.Width / 2f);
        var top = sentry.Y - (SentryEntity.Height / 2f);
        var bottom = sentry.Y + (SentryEntity.Height / 2f);

        foreach (var mine in _mines)
        {
            if (!mine.IsStickied)
            {
                continue;
            }

            if (mine.X < left || mine.X > right || mine.Y < top || mine.Y > bottom)
            {
                continue;
            }

            mine.Unstick();
        }
    }

    private void ApplySentryDestroyBlastToOwner(SentryEntity sentry)
    {
        var owner = FindPlayerById(sentry.OwnerPlayerId);
        if (owner is null
            || !owner.IsAlive
            || !owner.CanOccupy(Level, owner.Team, owner.X, owner.Y + 1f))
        {
            return;
        }

        var distance = DistanceBetween(sentry.X, sentry.Y, owner.X, owner.Y);
        if (distance >= SentryDestroyBlastRadius)
        {
            return;
        }

        var distanceFactor = 1f - (distance / SentryDestroyBlastRadius);
        if (distanceFactor <= RocketProjectileEntity.SplashThresholdFactor)
        {
            return;
        }

        var impulse = GetExplosionImpulseMagnitude(
            owner,
            sentry.X,
            sentry.Y,
            SentryDestroyKnockbackPerTick,
            distanceFactor,
            useMineVectorProfile: false);
        ApplyExplosionImpulse(owner, sentry.X, sentry.Y, impulse);
    }

    private void SpawnSentryGibs(PlayerTeam team, float x, float y)
    {
        var gib = new SentryGibEntity(AllocateEntityId(), team, x, y);
        _sentryGibs.Add(gib);
        _entities.Add(gib.Id, gib);
    }

    private bool TryBuildSentry(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Engineer
            || !player.CanAffordSentry()
            || player.IsInSpawnRoom)
        {
            return false;
        }

        foreach (var sentry in _sentries)
        {
            if (sentry.OwnerPlayerId == player.Id)
            {
                return false;
            }

            if (sentry.IsNear(player.X, player.Y, SentryBuildProximityRadius))
            {
                return false;
            }
        }

        if (!player.SpendMetal(SentryBuildCost))
        {
            return false;
        }

        var sentryEntity = new SentryEntity(
            AllocateEntityId(),
            player.Id,
            player.Team,
            player.X,
            player.Y,
            player.FacingDirectionX);
        _sentries.Add(sentryEntity);
        _entities.Add(sentryEntity.Id, sentryEntity);
        return true;
    }

    private bool TryDestroySentry(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return false;
        }

        var hadSentry = false;
        for (var index = _sentries.Count - 1; index >= 0; index -= 1)
        {
            if (_sentries[index].OwnerPlayerId != player.Id)
            {
                continue;
            }

            hadSentry = true;
            DestroySentry(_sentries[index]);
        }

        return hadSentry;
    }
}

