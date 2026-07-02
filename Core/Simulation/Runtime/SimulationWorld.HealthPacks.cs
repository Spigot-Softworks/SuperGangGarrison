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
        var clampedY = Bounds.ClampY(y, HealthPackEntity.Height);
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

    private void SpawnMapHealthPack(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= Level.HealthPackSpawns.Count)
        {
            return;
        }

        var marker = Level.HealthPackSpawns[spawnIndex];
        var healthPack = new HealthPackEntity(
            AllocateEntityId(),
            Bounds.ClampX(marker.X, HealthPackEntity.Width),
            Bounds.ClampY(marker.Y, HealthPackEntity.Height),
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
        var dropChance = ExperimentalGameplaySettings.EnemyHealthPackDropChance;
        if (!ExperimentalGameplaySettings.EnableEnemyHealthPackDrops
            || dropChance <= 0f
            || killer is null
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team
            || victim.Team == LocalPlayerTeam
            || (dropChance < 1f && _random.NextSingle() > dropChance))
        {
            return;
        }

        var size = _random.NextSingle() < ExperimentalGameplaySettings.EnemyHealthPackLargeChance
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
