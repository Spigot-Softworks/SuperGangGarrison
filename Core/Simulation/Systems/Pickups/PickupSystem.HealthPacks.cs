namespace OpenGarrison.Core;

internal sealed partial class PickupSystem
{
    internal void AdvanceHealthPacks()
    {
        AdvanceHealthPackSpawnTimers();

        for (var packIndex = _host.WorldObjects.HealthPacks.Count - 1; packIndex >= 0; packIndex -= 1)
        {
            var healthPack = _host.WorldObjects.HealthPacks[packIndex];
            healthPack.Advance(_host.Level, _host.Bounds);

            var pickedUp = false;
            foreach (var player in _host.EnumerateSimulatedPlayers())
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

                if (_host.Decisions.ShouldCancelPickup(
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
                if (_host.DamageRules.ApplyHealingWithFeedback(
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

    internal void SpawnHealthPack(float x, float y, HealthPackSize size)
    {
        var clampedX = _host.Bounds.ClampX(x, HealthPackEntity.Width);
        var clampedY = ResolveHealthPackSpawnY(clampedX, y);
        var horizontalSpeed = (_host.Randoms.Gameplay.NextSingle() * 2f - 1f) * 1.35f;
        var verticalSpeed = -2.25f - (_host.Randoms.Gameplay.NextSingle() * 1.5f);
        var healthPack = new HealthPackEntity(
            _host.AllocateEntityId(),
            clampedX,
            clampedY,
            size,
            horizontalSpeed,
            verticalSpeed);
        _host.WorldObjects.HealthPacks.Add(healthPack);
        _host.EntityStore.Add(healthPack);
    }

    internal float ResolveHealthPackSpawnY(float x, float y)
    {
        var resolvedY = _host.Bounds.ClampY(y, HealthPackEntity.Height);
        var halfWidth = HealthPackEntity.Width / 2f;
        var halfHeight = HealthPackEntity.Height / 2f;

        // Map-authored and death-drop positions can land inside a solid. Start
        // the pack above the overlapping surface so it remains visible and
        // its pickup bounds overlap a player standing on that surface.
        for (var pass = 0; pass < _host.Level.Solids.Count; pass += 1)
        {
            var movedAboveSolid = false;
            foreach (var solid in _host.Level.Solids)
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

                var surfaceY = _host.Bounds.ClampY(solid.Top - halfHeight, HealthPackEntity.Height);
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

    internal void SpawnMapHealthPack(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= _host.Level.HealthPackSpawns.Count)
        {
            return;
        }

        var marker = _host.Level.HealthPackSpawns[spawnIndex];
        var x = _host.Bounds.ClampX(marker.X, HealthPackEntity.Width);
        var healthPack = new HealthPackEntity(
            _host.AllocateEntityId(),
            x,
            ResolveHealthPackSpawnY(x, marker.Y),
            marker.Size,
            horizontalSpeed: 0f,
            verticalSpeed: 0f,
            sourceSpawnIndex: spawnIndex);
        _host.WorldObjects.HealthPacks.Add(healthPack);
        _host.EntityStore.Add(healthPack);
    }

    internal void AdvanceHealthPackSpawnTimers()
    {
        for (var spawnIndex = 0; spawnIndex < _host.WorldObjects.HealthPackSpawnRespawnTicks.Count; spawnIndex += 1)
        {
            if (_host.WorldObjects.HealthPackSpawnRespawnTicks[spawnIndex] <= 0)
            {
                continue;
            }

            _host.WorldObjects.HealthPackSpawnRespawnTicks[spawnIndex] -= 1;
            if (_host.WorldObjects.HealthPackSpawnRespawnTicks[spawnIndex] <= 0)
            {
                SpawnMapHealthPack(spawnIndex);
            }
        }
    }

    public int GetHealthPackSpawnRespawnTicksRemaining(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= _host.WorldObjects.HealthPackSpawnRespawnTicks.Count)
        {
            return 0;
        }

        return _host.WorldObjects.HealthPackSpawnRespawnTicks[spawnIndex];
    }

    internal void ResetHealthPackSpawnsForLevel()
    {
        _host.WorldObjects.HealthPackSpawnRespawnTicks.Clear();
        _host.WorldObjects.RemoveAll(_host.WorldObjects.HealthPacks);
        for (var spawnIndex = 0; spawnIndex < _host.Level.HealthPackSpawns.Count; spawnIndex += 1)
        {
            _host.WorldObjects.HealthPackSpawnRespawnTicks.Add(0);
            SpawnMapHealthPack(spawnIndex);
        }
    }

    internal void TrySpawnExperimentalEnemyHealthPackDrop(PlayerEntity victim, PlayerEntity? killer)
    {
        if (_host.LastToDieState.StageNumber > 0)
        {
            // Every enemy death is eligible, including environmental deaths.
            // Survivor deaths must never create a kit for the enemy team.
            if (victim.Team == _host.LocalPlayerTeam || victim.HasLastToDieSurvivorBuff
                || _host.Randoms.Gameplay.NextSingle() >= LastToDie.LastToDieSurvivorRules.GetHealthPackDropChance(_host.LastToDieState.StageNumber))
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

        var settings = _host.LastToDieRules.GetLastToDieGameplaySettings(killer);
        var dropChance = settings.EnemyHealthPackDropChance;
        if (!settings.EnableEnemyHealthPackDrops
            || dropChance <= 0f
            || ReferenceEquals(killer, victim)
            || killer.Team == victim.Team
            || victim.Team == _host.LocalPlayerTeam
            || (dropChance < 1f && _host.Randoms.Gameplay.NextSingle() > dropChance))
        {
            return;
        }

        SpawnRandomEnemyHealthPack(victim);
    }

    internal void SpawnRandomEnemyHealthPack(PlayerEntity victim)
    {
        var size = _host.Randoms.Gameplay.NextSingle() < global::OpenGarrison.Core.ExperimentalGameplaySettings.EnemyHealthPackLargeChance
            ? HealthPackSize.Large
            : HealthPackSize.Small;
        SpawnHealthPack(victim.X, victim.Bottom - 16f, size);
    }


    internal void ClearTemporaryHealthPacks()
    {
        for (var index = _host.WorldObjects.HealthPacks.Count - 1; index >= 0; index -= 1)
        {
            if (_host.WorldObjects.HealthPacks[index].IsMapSpawned)
            {
                continue;
            }

            RemoveHealthPackAt(index);
        }
    }

    internal void RemoveHealthPackAt(int index)
    {
        var healthPack = _host.WorldObjects.HealthPacks[index];
        _host.EntityStore.Remove(healthPack.Id);
        _host.WorldObjects.HealthPacks.RemoveAt(index);
        if (healthPack.SourceSpawnIndex >= 0
            && healthPack.SourceSpawnIndex < _host.Level.HealthPackSpawns.Count
            && healthPack.SourceSpawnIndex < _host.WorldObjects.HealthPackSpawnRespawnTicks.Count)
        {
            _host.WorldObjects.HealthPackSpawnRespawnTicks[healthPack.SourceSpawnIndex] =
                Math.Max(1, _host.Level.HealthPackSpawns[healthPack.SourceSpawnIndex].RespawnTicks);
        }
    }
}
