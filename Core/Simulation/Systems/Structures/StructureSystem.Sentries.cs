namespace OpenGarrison.Core;

internal sealed partial class StructureSystem
{
    internal const float SentryBuildCost = 100f;
    internal const float DispenserBuildCost = 100f;
    internal const float SentryBuildProximityRadius = 50f;
    internal const float DispenserAuraRadius = 75f;
    internal const float StructurePlacementHorizontalAssistRadius = 16f;
    internal const float StructurePlacementHorizontalAssistStep = 1f;
    internal const float SentryDestroyBlastRadius = 65f;
    internal const float SentryDestroyKnockbackPerTick = 4f;

    public bool TryBuildLocalSentry()
    {
        return TryBuildSentry(_host.LocalPlayer);
    }

    public bool TryBuildLocalDispenser()
    {
        return TryBuildDispenser(_host.LocalPlayer);
    }

    /// <summary>
    /// Primary-weapon swaps are intentionally restricted to healing cabinets and
    /// allied dispensers. This is shared by authority and client prediction.
    /// </summary>
    public bool IsNearPrimaryWeaponSwapStation(PlayerEntity player)
    {
        for (var index = 0; index < _host.Level.RoomObjects.Count; index += 1)
        {
            if (!_host.Level.IsRoomObjectActive(index))
            {
                continue;
            }

            var marker = _host.Level.RoomObjects[index];
            if (marker.Type == RoomObjectType.HealingCabinet
                && player.IntersectsMarker(marker.CenterX, marker.CenterY, marker.Width, marker.Height))
            {
                return true;
            }
        }

        for (var index = 0; index < _host.WorldObjects.Sentries.Count; index += 1)
        {
            var structure = _host.WorldObjects.Sentries[index];
            if (structure.IsDispenser
                && structure.Team == player.Team
                && structure.IsBuilt
                && structure.IsNear(player.X, player.Y, DispenserAuraRadius))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryDestroyLocalSentry()
    {
        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            if (sentry.OwnerPlayerId != _host.LocalPlayer.Id || sentry.IsDispenser)
            {
                continue;
            }

            DestroySentry(sentry, attacker: null);
            return true;
        }

        return false;
    }

    public int LastToDieDroneSentryCount => _host.LastToDieState.DroneSentryIds.Count;

    public bool IsLastToDieDroneSentry(SentryEntity sentry)
    {
        return _host.LastToDieState.DroneSentryIds.Contains(sentry.Id);
    }

    public SentryEntity SpawnLastToDieDroneSentry(PlayerTeam team, float x, float y, float startDirectionX, int maxHealth = SentryEntity.DefaultMaxHealth)
    {
        var sentry = new SentryEntity(
            _host.AllocateEntityId(),
            ownerPlayerId: 0,
            team,
            x,
            y,
            startDirectionX,
            maxHealth);
        sentry.ForceBuilt();
        _host.WorldObjects.Sentries.Add(sentry);
        _host.EntityStore.Add(sentry);
        _host.LastToDieState.DroneSentryIds.Add(sentry.Id);
        return sentry;
    }

    public void ClearLastToDieDroneSentries()
    {
        if (_host.LastToDieState.DroneSentryIds.Count == 0)
        {
            return;
        }

        for (var index = _host.WorldObjects.Sentries.Count - 1; index >= 0; index -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[index];
            if (!_host.LastToDieState.DroneSentryIds.Contains(sentry.Id))
            {
                continue;
            }

            _host.EntityStore.Remove(sentry.Id);
            _host.WorldObjects.Sentries.RemoveAt(index);
        }

        _host.LastToDieState.DroneSentryIds.Clear();
    }

    internal void AdvanceSentries()
    {
        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            if (IsLastToDieDroneSentry(sentry))
            {
                AdvanceLastToDieDroneSentry(sentry);
                continue;
            }

            var owner = _host.FindPlayerById(sentry.OwnerPlayerId);
            if (owner is null || owner.ClassId != PlayerClass.Engineer || owner.Team != sentry.Team)
            {
                DestroySentry(sentry, attacker: null);
                continue;
            }

            var wasLanded = sentry.HasLanded;
            sentry.Advance(_host.Level, _host.Bounds);
            if (!wasLanded && sentry.HasLanded)
            {
                _host.RegisterWorldSoundEvent("SentryFloorSnd", sentry.X, sentry.Y);
                _host.RegisterWorldSoundEvent("SentryBuildSnd", sentry.X, sentry.Y);
            }
            if (!sentry.IsBuilt)
            {
                continue;
            }

            if (IsSentryDisabledByHumiliation(sentry))
            {
                sentry.SetTarget(
                    null,
                    sentry.X + sentry.FacingDirectionX,
                    sentry.Y,
                    hasTarget: false);
                continue;
            }

            if (sentry.IsDispenser)
            {
                AdvanceDispenser(sentry);
                continue;
            }

            _host.ApplyExperimentalEngineerSentryPassiveEffects(sentry, owner);

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
                _host.RegisterWorldSoundEvent("SentryAlert", sentry.X, sentry.Y);
                continue;
            }

            if (!sentry.CanFire())
            {
                continue;
            }

            var reloadTicks = _host.GetExperimentalSentryReloadTicks(owner, sentry);
            var idleResetTicks = _host.GetExperimentalSentryIdleResetTicks();
            _host.RegisterWorldSoundEvent("ShotgunSnd", sentry.X, sentry.Y);
            _host.FireExperimentalSentry(sentry, owner, target.Value, reloadTicks, idleResetTicks);
        }
    }

    internal void AdvanceDispenser(SentryEntity dispenser)
    {
        var ticksPerSecond = Math.Max(1, _host.Config.TicksPerSecond);
        var speedMultiplier = dispenser.GetDispenserAttackReloadSpeedMultiplier(ticksPerSecond);
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive
                || player.Team != dispenser.Team
                || !dispenser.IsNear(player.X, player.Y, DispenserAuraRadius))
            {
                continue;
            }

            player.SetDispenserBuffed(true, speedMultiplier);
            var appliedHealing = player.ApplyContinuousHealingAndGetAmount(
                dispenser.GetDispenserHealingPerSecond(ticksPerSecond) / ticksPerSecond);
            dispenser.AccumulateDispenserHealingFeedback(player.Id, appliedHealing);
        }

        if (dispenser.LifetimeTicks % ticksPerSecond == 0)
        {
            foreach (var feedback in dispenser.PendingDispenserHealingFeedback)
            {
                var player = _host.FindPlayerById(feedback.Key);
                if (player is not null && player.IsAlive && feedback.Value > 0)
                {
                    _host.RegisterHealingEvent(player, feedback.Value);
                }
            }

            if (dispenser.PendingDispenserHealingFeedback.Count > 0)
            {
                _host.RegisterWorldSoundEvent("MedigunSnd", dispenser.X, dispenser.Y);
            }

            dispenser.ClearDispenserHealingFeedback();
        }
    }

    internal void UpdateDispenserAuras()
    {
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            player.SetDispenserBuffed(false);
        }

        for (var index = 0; index < _host.WorldObjects.Sentries.Count; index += 1)
        {
            var dispenser = _host.WorldObjects.Sentries[index];
            if (!dispenser.IsDispenser
                || !dispenser.IsBuilt
                || IsSentryDisabledByHumiliation(dispenser))
            {
                continue;
            }

            foreach (var player in _host.EnumerateSimulatedPlayers())
            {
                if (player.IsAlive
                    && player.Team == dispenser.Team
                    && dispenser.IsNear(player.X, player.Y, DispenserAuraRadius))
                {
                    player.SetDispenserBuffed(
                        true,
                        dispenser.GetDispenserAttackReloadSpeedMultiplier(Math.Max(1, _host.Config.TicksPerSecond)));
                }
            }
        }
    }

    internal bool IsSentryDisabledByHumiliation(SentryEntity sentry)
    {
        if (!_host.MatchState.IsEnded)
        {
            return false;
        }

        return !_host.MatchState.WinnerTeam.HasValue || sentry.Team != _host.MatchState.WinnerTeam.Value;
    }

    internal void AdvanceLastToDieDroneSentry(SentryEntity sentry)
    {
        sentry.AdvanceFloatingRuntime();
        if (!_host.LocalPlayer.IsAlive || _host.LocalPlayer.Team == sentry.Team || _host.LocalPlayer.IsSpyBackstabAnimating)
        {
            sentry.SetTarget(null, sentry.X + sentry.FacingDirectionX, sentry.Y, hasTarget: false);
            return;
        }

        const float desiredDistance = 150f;
        var desiredX = _host.LocalPlayer.X;
        var desiredY = _host.LocalPlayer.Y - global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAutonomousPhaseEngineHoverHeight;
        var deltaX = desiredX - sentry.X;
        var deltaY = desiredY - sentry.Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance > desiredDistance && distance > 0.001f)
        {
            var moveDistance = MathF.Min(
                distance - desiredDistance,
                global::OpenGarrison.Core.ExperimentalGameplaySettings.DefaultEngineerAutonomousPhaseEngineFollowSpeedPerTick);
            sentry.MoveTo(
                sentry.X + (deltaX / distance) * moveDistance,
                sentry.Y + (deltaY / distance) * moveDistance);
        }

        if (distance > SentryEntity.TargetRange || !_host.HasSentryLineOfSight(sentry, _host.LocalPlayer))
        {
            sentry.SetTarget(null, _host.LocalPlayer.X, _host.LocalPlayer.Y, hasTarget: false);
            return;
        }

        var previousTargetId = sentry.CurrentTargetPlayerId;
        sentry.SetTarget(_host.LocalPlayer.Id, _host.LocalPlayer.X, _host.LocalPlayer.Y);
        if (previousTargetId != _host.LocalPlayer.Id && sentry.BeginTargetAlert())
        {
            _host.RegisterWorldSoundEvent("SentryAlert", sentry.X, sentry.Y);
            return;
        }

        if (!sentry.CanFire())
        {
            return;
        }

        sentry.FireAt(_host.LocalPlayer.X, _host.LocalPlayer.Y);
        _host.RegisterWorldSoundEvent("ShotgunSnd", sentry.X, sentry.Y);
        if (distance > 0f)
        {
            _host.RegisterCombatTrace(
                sentry.X,
                sentry.Y,
                (_host.LocalPlayer.X - sentry.X) / distance,
                (_host.LocalPlayer.Y - sentry.Y) / distance,
                distance,
                true,
                sentry.Team);
        }

        if (_host.ApplyPlayerDamage(_host.LocalPlayer, SentryEntity.HitDamage, null, PlayerEntity.SpyDamageRevealAlpha))
        {
            _host.KillPlayer(
                _host.LocalPlayer,
                killer: _host.FindPlayerById(sentry.OwnerPlayerId),
                weaponSpriteName: "TurretKL",
                deathCamSentry: sentry);
        }
    }

    internal void AdvanceSentryGibs()
    {
        for (var gibIndex = _host.WorldObjects.SentryGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _host.WorldObjects.SentryGibs[gibIndex];
            gib.AdvanceOneTick();
            var pickedUp = false;
            foreach (var player in _host.EnumerateSimulatedPlayers())
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

            _host.EntityStore.Remove(gib.Id);
            _host.WorldObjects.SentryGibs.RemoveAt(gibIndex);
        }
    }

    internal SentryTarget? AcquireSentryTarget(SentryEntity sentry)
    {
        var owner = _host.FindPlayerById(sentry.OwnerPlayerId);
        SentryTarget? preferredTarget = null;
        var preferredDistance = float.MaxValue;
        SentryTarget? nearestTarget = null;
        var nearestDistance = float.MaxValue;
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive || player.Team == sentry.Team)
            {
                continue;
            }

            // Cloaked spies should only be targetable while revealed.
            if (player.ClassId == PlayerClass.Spy
                && player.IsSpyCloaked
                && (!player.IsSpyVisibleToEnemies || player.SpyCloakAlpha <= 0.0001f))
            {
                continue;
            }

            // Spy performing a backstab should not trigger sentry fire.
            if (player.IsSpyBackstabAnimating)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, player.X, player.Y);
            if (distance > (owner is not null ? _host.GetExperimentalSentryTargetRange(owner) : SentryEntity.TargetRange))
            {
                continue;
            }

            var targetAngle = SimulationMath.PointDirectionDegrees(sentry.X, sentry.Y, player.X, player.Y);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc && !player.IntersectsMarker(sentry.X, sentry.Y, SentryEntity.Width, SentryEntity.Height))
            {
                continue;
            }

            if (!_host.HasSentryLineOfSight(sentry, player))
            {
                continue;
            }

            var candidate = new SentryTarget(player, null, null, null, null, player.X, player.Y, player.Id);
            if (owner is not null
                && _host.IsExperimentalEngineerPriorityTarget(owner, player)
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

        for (var index = 0; index < _host.WorldObjects.Generators.Count; index += 1)
        {
            var generator = _host.WorldObjects.Generators[index];
            if (generator.Team == sentry.Team || generator.IsDestroyed)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            var range = owner is not null ? _host.GetExperimentalSentryTargetRange(owner) : SentryEntity.TargetRange;
            if (distance > range || distance >= nearestDistance)
            {
                continue;
            }

            var targetAngle = SimulationMath.PointDirectionDegrees(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc)
            {
                continue;
            }

            if (!_host.HasObstacleLineOfSight(sentry.X, sentry.Y, generator.Marker.CenterX, generator.Marker.CenterY))
            {
                continue;
            }

            nearestTarget = new SentryTarget(null, generator, null, null, null, generator.Marker.CenterX, generator.Marker.CenterY, null);
            nearestDistance = distance;
        }

        for (var index = 0; index < _host.WorldObjects.Sentries.Count; index += 1)
        {
            var targetSentry = _host.WorldObjects.Sentries[index];
            if (ReferenceEquals(targetSentry, sentry) || targetSentry.Team == sentry.Team)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, targetSentry.X, targetSentry.Y);
            if (distance > SentryEntity.TargetRange || distance >= nearestDistance)
            {
                continue;
            }

            var targetAngle = SimulationMath.PointDirectionDegrees(sentry.X, sentry.Y, targetSentry.X, targetSentry.Y);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc)
            {
                continue;
            }

            if (!_host.HasObstacleLineOfSight(sentry.X, sentry.Y, targetSentry.X, targetSentry.Y))
            {
                continue;
            }

            nearestTarget = new SentryTarget(null, null, targetSentry, null, null, targetSentry.X, targetSentry.Y, null);
            nearestDistance = distance;
        }

        for (var index = 0; index < _host.WorldObjects.JumpPads.Count; index += 1)
        {
            var targetPad = _host.WorldObjects.JumpPads[index];
            if (targetPad.IsNeutral || targetPad.Team == sentry.Team || !targetPad.IsBuilt || targetPad.IsDead)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, targetPad.X, targetPad.Y);
            if (distance > SentryEntity.TargetRange || distance >= nearestDistance)
            {
                continue;
            }

            var targetAngle = SimulationMath.PointDirectionDegrees(sentry.X, sentry.Y, targetPad.X, targetPad.Y);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc)
            {
                continue;
            }

            if (!_host.HasObstacleLineOfSight(sentry.X, sentry.Y, targetPad.X, targetPad.Y))
            {
                continue;
            }

            nearestTarget = new SentryTarget(null, null, null, targetPad, null, targetPad.X, targetPad.Y, null);
            nearestDistance = distance;
        }

        for (var index = 0; index < _host.Level.RoomObjects.Count; index += 1)
        {
            if (!_host.Level.IsRoomObjectActive(index))
            {
                continue;
            }

            var marker = _host.Level.RoomObjects[index];
            if (marker.Type != RoomObjectType.DamageableZone
                || !DamageableMetadata.IsSentryTarget(marker.DamageableZone, _host.GetDamageableZoneHealth(index)))
            {
                continue;
            }

            var targetX = marker.CenterX;
            var targetY = marker.CenterY;
            var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, targetX, targetY);
            var range = owner is not null ? _host.GetExperimentalSentryTargetRange(owner) : SentryEntity.TargetRange;
            if (distance > range || distance >= nearestDistance)
            {
                continue;
            }

            var targetAngle = SimulationMath.PointDirectionDegrees(sentry.X, sentry.Y, targetX, targetY);
            var withinAllowedArc = targetAngle <= 45f
                || targetAngle >= 315f
                || (targetAngle >= 135f && targetAngle <= 225f);
            if (!withinAllowedArc)
            {
                continue;
            }

            if (!_host.HasObstacleLineOfSight(sentry.X, sentry.Y, targetX, targetY))
            {
                continue;
            }

            nearestTarget = new SentryTarget(null, null, null, null, index, targetX, targetY, null);
            nearestDistance = distance;
        }

        return preferredTarget ?? nearestTarget;
    }

    internal void DestroySentry(SentryEntity sentry, PlayerEntity? attacker = null)
    {
        for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            if (!ReferenceEquals(_host.WorldObjects.Sentries[sentryIndex], sentry))
            {
                continue;
            }

            _host.AwardSentryDestructionPoints(sentry, attacker);
            ReleaseMinesFromSentry(sentry);
            ApplySentryDestroyBlastToOwner(sentry);
            _host.EntityStore.Remove(sentry.Id);
            _host.WorldObjects.Sentries.RemoveAt(sentryIndex);
            _host.LastToDieState.DroneSentryIds.Remove(sentry.Id);
            _host.RegisterWorldSoundEvent("ExplosionSnd", sentry.X, sentry.Y);
            _host.RegisterVisualEffect("Explosion", sentry.X, sentry.Y);
            SpawnSentryGibs(sentry.Team, sentry.X, sentry.Y, sentry.IsDispenser);
            break;
        }
    }

    internal void ReleaseMinesFromSentry(SentryEntity sentry)
    {
        var left = sentry.X - (SentryEntity.Width / 2f);
        var right = sentry.X + (SentryEntity.Width / 2f);
        var top = sentry.Y - (SentryEntity.Height / 2f);
        var bottom = sentry.Y + (SentryEntity.Height / 2f);

        foreach (var mine in _host.Mines)
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

    internal void ApplySentryDestroyBlastToOwner(SentryEntity sentry)
    {
        var owner = _host.FindPlayerById(sentry.OwnerPlayerId);
        if (owner is null
            || !owner.IsAlive
            || !owner.CanOccupy(_host.Level, owner.Team, owner.X, owner.Y + 1f))
        {
            return;
        }

        var distance = SimulationMath.DistanceBetween(sentry.X, sentry.Y, owner.X, owner.Y);
        if (distance >= SentryDestroyBlastRadius)
        {
            return;
        }

        var distanceFactor = 1f - (distance / SentryDestroyBlastRadius);
        if (distanceFactor <= RocketProjectileEntity.SplashThresholdFactor)
        {
            return;
        }

        var impulse = _host.GetExplosionImpulseMagnitude(
            owner,
            sentry.X,
            sentry.Y,
            SentryDestroyKnockbackPerTick,
            distanceFactor,
            useMineVectorProfile: false);
        _host.ApplyExplosionImpulse(owner, sentry.X, sentry.Y, impulse);
    }

    internal void SpawnSentryGibs(PlayerTeam team, float x, float y, bool isDispenser)
    {
        var gib = new SentryGibEntity(_host.AllocateEntityId(), team, x, y, isDispenser);
        _host.WorldObjects.SentryGibs.Add(gib);
        _host.EntityStore.Add(gib);
    }

    internal bool TryBuildSentry(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Engineer
            || !player.CanAffordSentry()
            || player.IsInSpawnRoom)
        {
            return false;
        }

        if (_host.GetExperimentalOwnedSentryCount(player.Id) >= _host.GetExperimentalMaxOwnedSentries(player))
        {
            return false;
        }

        if (!TryResolveStructurePlacement(player.X, player.Y, SentryEntity.Width, SentryEntity.Height, out var placementX, out var placementY))
        {
            return false;
        }

        foreach (var sentry in _host.WorldObjects.Sentries)
        {
            if (sentry.IsNear(placementX, placementY, SentryBuildProximityRadius))
            {
                return false;
            }
        }

        if (!player.SpendMetal(SentryBuildCost))
        {
            return false;
        }

        var aimRadians = player.AimDirectionDegrees * (MathF.PI / 180f);
        var aimDirectionX = DeterministicMath.Cos(aimRadians);
        var startDirectionX = MathF.Abs(aimDirectionX) > 0.001f
            ? (aimDirectionX >= 0f ? 1f : -1f)
            : player.FacingDirectionX;
        var sentryEntity = new SentryEntity(
            _host.AllocateEntityId(),
            player.Id,
            player.Team,
            placementX,
            placementY,
            startDirectionX,
            _host.GetExperimentalSentryMaxHealth(player));
        _host.WorldObjects.Sentries.Add(sentryEntity);
        _host.EntityStore.Add(sentryEntity);
        return true;
    }

    internal bool TryBuildDispenser(PlayerEntity player)
    {
        if (!player.IsAlive
            || player.ClassId != PlayerClass.Engineer
            || player.Metal < DispenserBuildCost
            || player.IsInSpawnRoom)
        {
            return false;
        }

        var ownedDispenserCount = 0;
        foreach (var structure in _host.WorldObjects.Sentries)
        {
            if (structure.OwnerPlayerId == player.Id && structure.IsDispenser)
            {
                ownedDispenserCount += 1;
            }
        }

        if (ownedDispenserCount > 0
            || !TryResolveStructurePlacement(player.X, player.Y, SentryEntity.Width, SentryEntity.Height, out var placementX, out var placementY))
        {
            return false;
        }

        foreach (var structure in _host.WorldObjects.Sentries)
        {
            if (structure.IsNear(placementX, placementY, SentryBuildProximityRadius))
            {
                return false;
            }
        }

        if (!player.SpendMetal(DispenserBuildCost))
        {
            return false;
        }

        var aimRadians = player.AimDirectionDegrees * (MathF.PI / 180f);
        var aimDirectionX = DeterministicMath.Cos(aimRadians);
        var startDirectionX = MathF.Abs(aimDirectionX) > 0.001f
            ? (aimDirectionX >= 0f ? 1f : -1f)
            : player.FacingDirectionX;
        var dispenser = new SentryEntity(
            _host.AllocateEntityId(),
            player.Id,
            player.Team,
            placementX,
            placementY,
            startDirectionX,
            SentryEntity.DispenserMaxHealth,
            isDispenser: true);
        _host.WorldObjects.Sentries.Add(dispenser);
        _host.EntityStore.Add(dispenser);
        return true;
    }

    internal bool TryResolveStructurePlacement(float x, float y, float width, float height, out float placementX, out float placementY)
    {
        placementX = x;
        placementY = y;
        if (CanPlaceStructureAt(x, y, width, height))
        {
            return true;
        }

        for (var offset = StructurePlacementHorizontalAssistStep;
             offset <= StructurePlacementHorizontalAssistRadius;
             offset += StructurePlacementHorizontalAssistStep)
        {
            var leftX = x - offset;
            if (CanPlaceStructureAt(leftX, y, width, height))
            {
                placementX = leftX;
                return true;
            }

            var rightX = x + offset;
            if (CanPlaceStructureAt(rightX, y, width, height))
            {
                placementX = rightX;
                return true;
            }
        }

        return false;
    }

    internal bool CanPlaceStructureAt(float x, float y, float width, float height)
    {
        var left = x - (width / 2f);
        var right = x + (width / 2f);
        var top = y - (height / 2f);
        var bottom = y + (height / 2f);
        return left >= 0f
            && right <= _host.Bounds.Width
            && top >= 0f
            && bottom <= _host.Bounds.Height
            && !_host.Level.IntersectsSolid(left, top, right, bottom);
    }

    internal bool TryDestroySentry(PlayerEntity player)
    {
        return TryDestroyOwnedStructureForOwnerCommand(player, isDispenser: false) == OwnedSentryDestroyResult.Destroyed;
    }

    internal bool TryDestroyDispenser(PlayerEntity player)
    {
        return TryDestroyOwnedStructureForOwnerCommand(player, isDispenser: true) == OwnedSentryDestroyResult.Destroyed;
    }

    internal OwnedSentryDestroyResult TryDestroySentryForOwnerCommand(PlayerEntity player)
    {
        return TryDestroyOwnedStructureForOwnerCommand(player, isDispenser: false);
    }

    internal OwnedSentryDestroyResult TryDestroyOwnedStructureForOwnerCommand(PlayerEntity player, bool? isDispenser)
    {
        if (!player.IsAlive)
        {
            return OwnedSentryDestroyResult.NoOwnedSentry;
        }

        var destroyedSentry = false;
        for (var index = _host.WorldObjects.Sentries.Count - 1; index >= 0; index -= 1)
        {
            var sentry = _host.WorldObjects.Sentries[index];
            if (sentry.OwnerPlayerId != player.Id)
            {
                continue;
            }

            if (isDispenser.HasValue && sentry.IsDispenser != isDispenser.Value)
            {
                continue;
            }

            DestroySentry(sentry, attacker: null);
            destroyedSentry = true;
        }

        if (destroyedSentry)
        {
            return OwnedSentryDestroyResult.Destroyed;
        }

        return OwnedSentryDestroyResult.NoOwnedSentry;
    }
}
