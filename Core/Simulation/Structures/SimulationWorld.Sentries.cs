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

            DestroySentry(sentry, attacker: null);
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
                DestroySentry(sentry, attacker: null);
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

            ApplyExperimentalEngineerSentryPassiveEffects(sentry, owner);

            var target = AcquireSentryTarget(sentry);
            var previousTargetId = sentry.CurrentTargetPlayerId;
            sentry.SetTarget(
                target?.PlayerId,
                target?.X ?? sentry.X + sentry.FacingDirectionX,
                target?.Y ?? sentry.Y,
                target.HasValue);
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

            var reloadTicks = GetExperimentalSentryReloadTicks(owner, sentry);
            var idleResetTicks = GetExperimentalSentryIdleResetTicks();
            RegisterWorldSoundEvent("ShotgunSnd", sentry.X, sentry.Y);
            FireExperimentalSentry(sentry, owner, target.Value, reloadTicks, idleResetTicks);
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
        var owner = FindPlayerById(sentry.OwnerPlayerId);
        SentryTarget? preferredTarget = null;
        var preferredDistance = float.MaxValue;
        SentryTarget? nearestTarget = null;
        var nearestDistance = float.MaxValue;
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team == sentry.Team)
            {
                continue;
            }

            // Cloaked spies should only be targetable while revealed.
            if (player.ClassId == PlayerClass.Spy
                && player.IsSpyCloaked
                && !player.IsSpyVisibleToEnemies)
            {
                continue;
            }

            var distance = DistanceBetween(sentry.X, sentry.Y, player.X, player.Y);
            if (distance > (owner is not null ? GetExperimentalSentryTargetRange(owner) : SentryEntity.TargetRange))
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

            var candidate = new SentryTarget(player, null, player.X, player.Y, player.Id);
            if (owner is not null
                && IsExperimentalEngineerPriorityTarget(owner, player)
                && distance < preferredDistance)
            {
                preferredTarget = candidate;
                preferredDistance = distance;
            }

            if (distance < nearestDistance)
            {
                nearestTarget = candidate;
                nearestDistance = distance;
            }
        }

        for (var index = 0; index < _generators.Count; index += 1)
        {
            var generator = _generators[index];
            if (generator.Team == sentry.Team || generator.IsDestroyed)
            {
                continue;
            }

            var distance = DistanceBetween(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            var range = owner is not null ? GetExperimentalSentryTargetRange(owner) : SentryEntity.TargetRange;
            if (distance > range || distance >= nearestDistance)
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

        return preferredTarget ?? nearestTarget;
    }

    private void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null)
    {
        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (!ReferenceEquals(_sentries[sentryIndex], sentry))
            {
                continue;
            }

            AwardSentryDestructionPoints(sentry, attacker);
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

        if (GetExperimentalOwnedSentryCount(player.Id) >= GetExperimentalMaxOwnedSentries(player))
        {
            return false;
        }

        foreach (var sentry in _sentries)
        {
            if (sentry.IsNear(player.X, player.Y, SentryBuildProximityRadius))
            {
                return false;
            }
        }

        if (!player.SpendMetal(SentryBuildCost))
        {
            return false;
        }

        var aimRadians = player.AimDirectionDegrees * (MathF.PI / 180f);
        var aimDirectionX = MathF.Cos(aimRadians);
        var startDirectionX = MathF.Abs(aimDirectionX) > 0.001f
            ? (aimDirectionX >= 0f ? 1f : -1f)
            : player.FacingDirectionX;
        var sentryEntity = new SentryEntity(
            AllocateEntityId(),
            player.Id,
            player.Team,
            player.X,
            player.Y,
            startDirectionX,
            GetExperimentalSentryMaxHealth(player));
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
            DestroySentry(_sentries[index], attacker: null);
        }

        return hadSentry;
    }
}

