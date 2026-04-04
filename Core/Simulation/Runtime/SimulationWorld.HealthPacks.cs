namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceHealthPacks()
    {
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

                if (ApplyHealingWithFeedback(
                        player,
                        healthPack.GetHealAmount(player),
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
    }

    private void RemoveHealthPackAt(int index)
    {
        _entities.Remove(_healthPacks[index].Id);
        _healthPacks.RemoveAt(index);
    }
}
