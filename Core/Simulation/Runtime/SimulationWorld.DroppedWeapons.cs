namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceDroppedWeapons()
    {
        for (var weaponIndex = _droppedWeapons.Count - 1; weaponIndex >= 0; weaponIndex -= 1)
        {
            var droppedWeapon = _droppedWeapons[weaponIndex];
            droppedWeapon.Advance(Level, Bounds);
            if (!droppedWeapon.IsExpired)
            {
                continue;
            }

            RemoveDroppedWeaponAt(weaponIndex);
        }
    }

    private void TryHandleDroppedWeaponInteraction(PlayerEntity player)
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
            if (!runtimeRegistry.CanUseAcquiredItem(player.ClassId, pickedWeaponItemId))
            {
                return;
            }

            var previousWeaponClassId = player.AcquiredWeaponClassId;
            var pickedWeaponClassId = nearbyWeapon.WeaponClassId;

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
            RegisterWorldSoundEvent("PickupSnd", nearbyWeapon.X, nearbyWeapon.Y);
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

    private DroppedWeaponEntity? FindNearbyDroppedWeapon(PlayerEntity player, out int droppedWeaponIndex)
    {
        droppedWeaponIndex = -1;
        var bestDistanceSquared = float.MaxValue;
        DroppedWeaponEntity? bestWeapon = null;
        for (var index = 0; index < _droppedWeapons.Count; index += 1)
        {
            var candidate = _droppedWeapons[index];
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

    private bool CanUseExperimentalDroppedWeapons(PlayerEntity? player)
    {
        return ExperimentalGameplaySettings.EnableEnemyDroppedWeapons
            && IsExperimentalPracticePowerOwner(player)
            && player is not null
            && player.ClassId == PlayerClass.Soldier;
    }

    private void SpawnDroppedWeapon(float x, float y, PlayerClass weaponClassId, PlayerTeam team, float horizontalSpeed, float verticalSpeed)
    {
        if (!CharacterClassCatalog.SupportsExperimentalAcquiredWeapon(weaponClassId))
        {
            return;
        }

        var clampedX = Bounds.ClampX(x, DroppedWeaponEntity.Width);
        var clampedY = Bounds.ClampY(y, DroppedWeaponEntity.Height);
        var droppedWeapon = new DroppedWeaponEntity(
            AllocateEntityId(),
            weaponClassId,
            team,
            clampedX,
            clampedY,
            horizontalSpeed,
            verticalSpeed);
        _droppedWeapons.Add(droppedWeapon);
        _entities.Add(droppedWeapon.Id, droppedWeapon);
    }

    private void TrySpawnExperimentalEnemyDroppedWeapon(PlayerEntity victim, PlayerEntity? killer)
    {
        if (!ExperimentalGameplaySettings.EnableEnemyDroppedWeapons
            || killer is null
            || !CanUseExperimentalDroppedWeapons(killer)
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team
            || victim.Team == LocalPlayerTeam
            || !CharacterClassCatalog.SupportsExperimentalAcquiredWeapon(victim.ClassId)
            || _random.NextSingle() > ExperimentalGameplaySettings.EnemyDroppedWeaponChance)
        {
            return;
        }

        var horizontalSpeed = (_random.NextSingle() * 2f - 1f) * 1.6f;
        var verticalSpeed = -2.4f - (_random.NextSingle() * 1.4f);
        SpawnDroppedWeapon(victim.X, victim.Bottom - 18f, victim.ClassId, victim.Team, horizontalSpeed, verticalSpeed);
    }

    private void ClearDroppedWeapons()
    {
        RemoveEntities(_droppedWeapons);
    }

    private void RemoveDroppedWeaponAt(int index)
    {
        _entities.Remove(_droppedWeapons[index].Id);
        _droppedWeapons.RemoveAt(index);
    }
}
