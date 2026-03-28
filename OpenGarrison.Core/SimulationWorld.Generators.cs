using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const int GeneratorMaxHealth = 4000;
    private const float GeneratorExplosionBlastRadius = 400f;
    private const float GeneratorExplosionPlayerKnockback = 15f;
    private const float GeneratorExplosionDeadBodyKnockback = 10f;
    private const float GeneratorExplosionGibKnockback = 15f;

    public IReadOnlyList<GeneratorState> Generators => _generators;

    public GeneratorState? GetGenerator(PlayerTeam team)
    {
        for (var index = 0; index < _generators.Count; index += 1)
        {
            if (_generators[index].Team == team)
            {
                return _generators[index];
            }
        }

        return null;
    }

    private void ResetGeneratorStateForNewRound()
    {
        _generators.Clear();

        var generatorMarkers = Level.GetRoomObjects(RoomObjectType.Generator);
        for (var index = 0; index < generatorMarkers.Count; index += 1)
        {
            var marker = generatorMarkers[index];
            if (!marker.Team.HasValue)
            {
                continue;
            }

            _generators.Add(new GeneratorState(marker.Team.Value, marker, GeneratorMaxHealth));
        }
    }

    private void UpdateGeneratorState()
    {
        MatchObjectiveFlowSystem.UpdateGeneratorState(this);
    }

    private void AdvanceGeneratorMatchState()
    {
        MatchObjectiveFlowSystem.AdvanceGeneratorMatchState(this);
    }

    private bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker = null)
    {
        var generator = GetGenerator(targetTeam);
        if (generator is null || generator.IsDestroyed)
        {
            return false;
        }

        var destroyed = ApplyGeneratorDamage(generator, damage, attacker);
        if (!destroyed)
        {
            return false;
        }

        HandleGeneratorDestroyed(generator);
        return true;
    }

    private void HandleGeneratorDestroyed(GeneratorState generator)
    {
        if (MatchState.IsEnded)
        {
            return;
        }

        var winner = GetOpposingTeam(generator.Team);
        if (winner == PlayerTeam.Red)
        {
            RedCaps += 1;
        }
        else
        {
            BlueCaps += 1;
        }

        RegisterWorldSoundEvent("ExplosionSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        RegisterWorldSoundEvent("RevolverSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        RegisterWorldSoundEvent("CPBeginCapSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        RegisterVisualEffect("Explosion", generator.Marker.CenterX, generator.Marker.CenterY, count: 2);
        RecordGeneratorDestroyedObjectiveLog(winner);
        ApplyGeneratorExplosion(generator);

        MatchState = MatchState with { Phase = MatchPhase.Ended, WinnerTeam = winner };
        QueuePendingMapChange();
    }

    private void ApplyGeneratorExplosion(GeneratorState generator)
    {
        var centerX = generator.Marker.CenterX;
        var centerY = generator.Marker.CenterY;

        var playersToKill = new List<PlayerEntity>();
        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = DistanceBetween(centerX, centerY, player.X, player.Y);
            if (distance >= GeneratorExplosionBlastRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / GeneratorExplosionBlastRadius);
            ApplyExplosionImpulse(player, centerX, centerY, GeneratorExplosionPlayerKnockback * distanceFactor * LegacyMovementModel.SourceTicksPerSecond);
            playersToKill.Add(player);
        }

        for (var index = 0; index < playersToKill.Count; index += 1)
        {
            var player = playersToKill[index];
            if (player.IsAlive)
            {
                KillPlayer(player, gibbed: true, weaponSpriteName: "ExplodeKL");
            }
        }

        var sentryIdsToDestroy = new List<int>();
        for (var sentryIndex = 0; sentryIndex < _sentries.Count; sentryIndex += 1)
        {
            var sentry = _sentries[sentryIndex];
            if (DistanceBetween(centerX, centerY, sentry.X, sentry.Y) < GeneratorExplosionBlastRadius)
            {
                sentryIdsToDestroy.Add(sentry.Id);
            }
        }

        for (var index = 0; index < sentryIdsToDestroy.Count; index += 1)
        {
            for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
            {
                if (_sentries[sentryIndex].Id == sentryIdsToDestroy[index])
                {
                    DestroySentry(_sentries[sentryIndex]);
                    break;
                }
            }
        }

        var rocketIdsToExplode = new List<int>();
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            if (DistanceBetween(centerX, centerY, _rockets[rocketIndex].X, _rockets[rocketIndex].Y) < GeneratorExplosionBlastRadius)
            {
                rocketIdsToExplode.Add(_rockets[rocketIndex].Id);
            }
        }

        for (var index = 0; index < rocketIdsToExplode.Count; index += 1)
        {
            for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (_rockets[rocketIndex].Id == rocketIdsToExplode[index])
                {
                    ExplodeRocket(_rockets[rocketIndex], directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                    break;
                }
            }
        }

        ApplyDeadBodyExplosionImpulse(centerX, centerY, GeneratorExplosionBlastRadius, GeneratorExplosionDeadBodyKnockback);

        var mineIdsToExplode = new List<int>();
        for (var mineIndex = 0; mineIndex < _mines.Count; mineIndex += 1)
        {
            if (DistanceBetween(centerX, centerY, _mines[mineIndex].X, _mines[mineIndex].Y) < GeneratorExplosionBlastRadius)
            {
                mineIdsToExplode.Add(_mines[mineIndex].Id);
            }
        }

        for (var index = 0; index < mineIdsToExplode.Count; index += 1)
        {
            var mine = FindMineById(mineIdsToExplode[index]);
            if (mine is not null)
            {
                ExplodeMine(mine);
            }
        }

        ApplyPlayerGibExplosionImpulse(centerX, centerY, GeneratorExplosionBlastRadius, GeneratorExplosionGibKnockback);

        for (var bubbleIndex = _bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (DistanceBetween(centerX, centerY, _bubbles[bubbleIndex].X, _bubbles[bubbleIndex].Y) < GeneratorExplosionBlastRadius)
            {
                RemoveBubbleAt(bubbleIndex);
            }
        }
    }

    private void ApplySnapshotGenerators(SnapshotMessage snapshot)
    {
        if ((GameModeKind)snapshot.GameMode != GameModeKind.Generator)
        {
            _generators.Clear();
            return;
        }

        ResetGeneratorStateForNewRound();
        if (_generators.Count == 0 || snapshot.Generators.Count == 0)
        {
            return;
        }

        for (var index = 0; index < snapshot.Generators.Count; index += 1)
        {
            var generatorState = snapshot.Generators[index];
            var target = GetGenerator((PlayerTeam)generatorState.Team);
            target?.SetHealth(generatorState.Health);
        }
    }

    internal GeneratorState? CombatTestGetGenerator(PlayerTeam team)
        => GetGenerator(team);

    internal void CombatTestSetGeneratorHealth(PlayerTeam team, int health)
    {
        var generator = GetGenerator(team);
        generator?.SetHealth(health);
    }

    internal bool CombatTestDamageGenerator(PlayerTeam team, float damage)
        => TryDamageGenerator(team, damage);
}
