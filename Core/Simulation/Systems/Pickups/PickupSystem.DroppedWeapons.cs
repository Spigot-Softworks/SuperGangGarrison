namespace OpenGarrison.Core;

internal sealed partial class PickupSystem
{
    internal void AdvanceDroppedWeapons()
    {
        for (var weaponIndex = _host.WorldObjects.DroppedWeapons.Count - 1; weaponIndex >= 0; weaponIndex -= 1)
        {
            var droppedWeapon = _host.WorldObjects.DroppedWeapons[weaponIndex];
            droppedWeapon.Advance(_host.Level, _host.Bounds);
            if (!droppedWeapon.IsExpired)
            {
                continue;
            }

            RemoveDroppedWeaponAt(weaponIndex);
        }
    }

    internal void TryHandleDroppedWeaponInteraction(PlayerEntity player)
    {
        if (!CanUseExperimentalDroppedWeapons(player) || !player.IsAlive)
        {
            return;
        }

        var nearbyWeapon = FindNearbyDroppedWeapon(player, out var nearbyIndex);
        if (nearbyWeapon is not null)
        {
            var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
            var pickedWeaponItemId = runtimeRegistry.GetPrimaryItem(nearbyWeapon.WeaponClassId).Id;
            if (!runtimeRegistry.CanUseAcquiredItem(player.GameplayClassId, pickedWeaponItemId))
            {
                return;
            }

            var previousWeaponClassId = player.AcquiredWeaponClassId;
            var pickedWeaponClassId = nearbyWeapon.WeaponClassId;
            if (_host.DecisionGate.ShouldCancelPickup(
                    WorldPickupKind.DroppedWeapon,
                    player,
                    nearbyWeapon.Id,
                    pickedWeaponClassId.ToString(),
                    nearbyWeapon.X,
                    nearbyWeapon.Y))
            {
                return;
            }

            if (previousWeaponClassId.HasValue && previousWeaponClassId.Value != pickedWeaponClassId)
            {
                SpawnDroppedWeapon(
                    player.X,
                    player.Y - 6f,
                    previousWeaponClassId.Value,
                    player.Team,
                    player.FacingDirectionX * 1.4f,
                    -1.8f);
            }

            if (previousWeaponClassId.HasValue && previousWeaponClassId.Value == pickedWeaponClassId)
            {
                player.SetAcquiredWeapon(null);
            }

            player.SetAcquiredWeapon(pickedWeaponClassId);
            player.EquipAcquiredWeapon();
            _host.WorldEffects.RegisterWorldSoundEvent("PickupSnd", nearbyWeapon.X, nearbyWeapon.Y);
            RemoveDroppedWeaponAt(nearbyIndex);
            return;
        }

        if (!player.HasAcquiredWeapon)
        {
            return;
        }

        if (player.IsAcquiredWeaponEquipped)
        {
            player.StowAcquiredWeapon();
        }
        else
        {
            player.EquipAcquiredWeapon();
        }
    }

    internal DroppedWeaponEntity? FindNearbyDroppedWeapon(PlayerEntity player, out int droppedWeaponIndex)
    {
        droppedWeaponIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        DroppedWeaponEntity? bestWeapon = null;
        for (var index = 0; index < _host.WorldObjects.DroppedWeapons.Count; index += 1)
        {
            var candidate = _host.WorldObjects.DroppedWeapons[index];
            if (!player.IntersectsMarker(
                    candidate.X,
                    candidate.Y,
                    DroppedWeaponEntity.PickupWidth,
                    DroppedWeaponEntity.PickupHeight))
            {
                continue;
            }

            var deltaX = candidate.X - player.X;
            var deltaY = candidate.Y - player.Y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            bestWeapon = candidate;
            droppedWeaponIndex = index;
        }

        return bestWeapon;
    }

    internal bool CanUseExperimentalDroppedWeapons(PlayerEntity? player)
    {
        return player is not null
            && _host.LastToDieRules.GetLastToDieGameplaySettings(player).EnableEnemyDroppedWeapons
            && _host.ExperimentalRules.IsExperimentalPracticePowerOwner(player)
            && player.ClassId == PlayerClass.Soldier;
    }

    internal void SpawnDroppedWeapon(float x, float y, PlayerClass weaponClassId, PlayerTeam team, float horizontalSpeed, float verticalSpeed)
    {
        if (!CharacterClassCatalog.SupportsExperimentalAcquiredWeapon(weaponClassId))
        {
            return;
        }

        var clampedX = _host.Bounds.ClampX(x, DroppedWeaponEntity.Width);
        var clampedY = _host.Bounds.ClampY(y, DroppedWeaponEntity.Height);
        var droppedWeapon = new DroppedWeaponEntity(
            _host.AllocateEntityId(),
            weaponClassId,
            team,
            clampedX,
            clampedY,
            horizontalSpeed,
            verticalSpeed);
        _host.WorldObjects.DroppedWeapons.Add(droppedWeapon);
        _host.EntityStore.Add(droppedWeapon);
    }

    internal void TrySpawnExperimentalEnemyDroppedWeapon(PlayerEntity victim, PlayerEntity? killer)
    {
        if (killer is null
            || !CanUseExperimentalDroppedWeapons(killer)
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team
            || !CharacterClassCatalog.SupportsExperimentalAcquiredWeapon(victim.ClassId)
            || _host.Randoms.Gameplay.NextSingle() > global::OpenGarrison.Core.ExperimentalGameplaySettings.EnemyDroppedWeaponChance)
        {
            return;
        }

        var horizontalSpeed = (_host.Randoms.Gameplay.NextSingle() * 2f - 1f) * 1.6f;
        var verticalSpeed = -2.4f - (_host.Randoms.Gameplay.NextSingle() * 1.4f);
        SpawnDroppedWeapon(victim.X, victim.Bottom - 18f, victim.ClassId, victim.Team, horizontalSpeed, verticalSpeed);
    }

    internal void ClearDroppedWeapons()
    {
        _host.WorldObjects.RemoveAll(_host.WorldObjects.DroppedWeapons);
    }

    internal void RemoveDroppedWeaponAt(int index)
    {
        _host.EntityStore.Remove(_host.WorldObjects.DroppedWeapons[index].Id);
        _host.WorldObjects.DroppedWeapons.RemoveAt(index);
    }
}
