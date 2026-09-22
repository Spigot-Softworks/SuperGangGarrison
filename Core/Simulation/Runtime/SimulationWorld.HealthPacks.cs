namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceHealthPacks()
    {
        AdvanceHealthPackSpawnTimers();

        for (var packIndex = _healthPacks.Count - 1; packIndex >= 0; packIndex -= 1)
        {
            var healthPack = _healthPacks[packIndex];
            healthPack.Advance(Level, Bounds);

            var pickedUp = false;
            foreach (var player in EnumerateSimulatedPlayers())
            {
                if (!player.IsAlive
                    || player.Health >= player.MaxHealth
                    || !player.IntersectsMarker(
                        healthPack.X,
                        healthPack.Y,
                        HealthPackEntity.PickupWidth,
                        HealthPackEntity.PickupHeight))
                {
                    continue;
                }

                if (ShouldCancelPickup(
                        WorldPickupKind.HealthPack,
                        player,
                        healthPack.Id,
                        healthPack.Size.ToString(),
                        healthPack.X,
                        healthPack.Y))
                {
                    continue;
                }

                var healAmount = healthPack.GetHealAmount(player) * player.ExperimentalHealthPackHealingMultiplier;
                if (ApplyHealingWithFeedback(
                        player,
                        healAmount,
                        soundName: "CbntHealSnd",
                        soundX: player.X,
                        soundY: player.Y) <= 0)
                {
                    continue;
                }

                pickedUp = true;
                break;
            }

            if (!pickedUp && !healthPack.IsExpired)
            {
                continue;
            }

            RemoveHealthPackAt(packIndex);
        }
    }

    private void SpawnHealthPack(float x, float y, HealthPackSize size)
    {
        var clampedX = Bounds.ClampX(x, HealthPackEntity.Width);
        var clampedY = ResolveHealthPackSpawnY(clampedX, y);
        var horizontalSpeed = (_random.NextSingle() * 2f - 1f) * 1.35f;
        var verticalSpeed = -2.25f - (_random.NextSingle() * 1.5f);
        var healthPack = new HealthPackEntity(
            AllocateEntityId(),
            clampedX,
            clampedY,
            size,
            horizontalSpeed,
            verticalSpeed);
        _healthPacks.Add(healthPack);
        _entities.Add(healthPack.Id, healthPack);
    }

    private float ResolveHealthPackSpawnY(float x, float y)
    {
        var resolvedY = Bounds.ClampY(y, HealthPackEntity.Height);
        var halfWidth = HealthPackEntity.Width / 2f;
        var halfHeight = HealthPackEntity.Height / 2f;

        // Map-authored and death-drop positions can land inside a solid. Start
        // the pack above the overlapping surface so it remains visible and
        // its pickup bounds overlap a player standing on that surface.
        for (var pass = 0; pass < Level.Solids.Count; pass += 1)
        {
            var movedAboveSolid = false;
            foreach (var solid in Level.Solids)
            {
                var left = x - halfWidth;
                var right = x + halfWidth;
                var top = resolvedY - halfHeight;
                var bottom = resolvedY + halfHeight;
                if (left >= solid.Right
                    || right <= solid.Left
                    || top >= solid.Bottom
                    || bottom <= solid.Top)
                {
                    continue;
                }

                var surfaceY = Bounds.ClampY(solid.Top - halfHeight, HealthPackEntity.Height);
                if (surfaceY >= resolvedY)
                {
                    return resolvedY;
                }

                resolvedY = surfaceY;
                movedAboveSolid = true;
                break;
            }

            if (!movedAboveSolid)
            {
                break;
            }
        }

        return resolvedY;
    }

    private void SpawnMapHealthPack(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= Level.HealthPackSpawns.Count)
        {
            return;
        }

        var marker = Level.HealthPackSpawns[spawnIndex];
        var x = Bounds.ClampX(marker.X, HealthPackEntity.Width);
        var healthPack = new HealthPackEntity(
            AllocateEntityId(),
            x,
            ResolveHealthPackSpawnY(x, marker.Y),
            marker.Size,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            sourceSpawnIndex: spawnIndex);
        _healthPacks.Add(healthPack);
        _entities.Add(healthPack.Id, healthPack);
    }

    private void AdvanceHealthPackSpawnTimers()
    {
        for (var spawnIndex = 0; spawnIndex < _healthPackSpawnRespawnTicks.Count; spawnIndex += 1)
        {
            if (_healthPackSpawnRespawnTicks[spawnIndex] <= 0)
            {
                continue;
            }

            _healthPackSpawnRespawnTicks[spawnIndex] -= 1;
            if (_healthPackSpawnRespawnTicks[spawnIndex] <= 0)
            {
                SpawnMapHealthPack(spawnIndex);
            }
        }
    }

    public int GetHealthPackSpawnRespawnTicksRemaining(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= _healthPackSpawnRespawnTicks.Count)
        {
            return 0;
        }

        return _healthPackSpawnRespawnTicks[spawnIndex];
    }

    private void ResetHealthPackSpawnsForLevel()
    {
        _healthPackSpawnRespawnTicks.Clear();
        RemoveEntities(_healthPacks);
        for (var spawnIndex = 0; spawnIndex < Level.HealthPackSpawns.Count; spawnIndex += 1)
        {
            _healthPackSpawnRespawnTicks.Add(0);
            SpawnMapHealthPack(spawnIndex);
        }
    }

    private void TrySpawnExperimentalEnemyHealthPackDrop(PlayerEntity victim, PlayerEntity? killer)
    {
        if (_lastToDieStageNumber > 0)
        {
            // Every enemy death is eligible, including environmental deaths.
            // Survivor deaths must never create a kit for the enemy team.
            if (victim.Team == LocalPlayerTeam || victim.HasLastToDieSurvivorBuff
                || _random.NextSingle() >= LastToDie.LastToDieSurvivorRules.GetHealthPackDropChance(_lastToDieStageNumber))
            {
                return;
            }
            SpawnRandomEnemyHealthPack(victim);
            return;
        }

        if (killer is null)
        {
            return;
        }

        var settings = GetLastToDieGameplaySettings(killer);
        var dropChance = settings.EnemyHealthPackDropChance;
        if (!settings.EnableEnemyHealthPackDrops
            || dropChance <= 0f
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team
            || victim.Team == LocalPlayerTeam
            || (dropChance < 1f && _random.NextSingle() > dropChance))
        {
            return;
        }

        SpawnRandomEnemyHealthPack(victim);
    }

    private void SpawnRandomEnemyHealthPack(PlayerEntity victim)
    {
        var size = _random.NextSingle() < global::OpenGarrison.Core.ExperimentalGameplaySettings.EnemyHealthPackLargeChance
            ? HealthPackSize.Large
            : HealthPackSize.Small;
        SpawnHealthPack(victim.X, victim.Bottom - 16f, size);
    }

    private void ClearHealthPacks()
    {
        RemoveEntities(_healthPacks);
        _healthPackSpawnRespawnTicks.Clear();
    }

    private void ClearTemporaryHealthPacks()
    {
        for (var index = _healthPacks.Count - 1; index >= 0; index -= 1)
        {
            if (_healthPacks[index].IsMapSpawned)
            {
                continue;
            }

            RemoveHealthPackAt(index);
        }
    }

    private void RemoveHealthPackAt(int index)
    {
        var healthPack = _healthPacks[index];
        _entities.Remove(healthPack.Id);
        _healthPacks.RemoveAt(index);
        if (healthPack.SourceSpawnIndex >= 0
            && healthPack.SourceSpawnIndex < Level.HealthPackSpawns.Count
            && healthPack.SourceSpawnIndex < _healthPackSpawnRespawnTicks.Count)
        {
            _healthPackSpawnRespawnTicks[healthPack.SourceSpawnIndex] =
                Math.Max(1, Level.HealthPackSpawns[healthPack.SourceSpawnIndex].RespawnTicks);
        }
    }
}
