using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    internal const int GeneratorMaxHealth = 4000;
    internal const float GeneratorExplosionBlastRadius = 400f;
    internal const float GeneratorExplosionPlayerKnockback = 15f;
    internal const float GeneratorExplosionDeadBodyKnockback = 10f;
    internal const float GeneratorExplosionGibKnockback = 15f;

    public IReadOnlyList<GeneratorState> Generators => _host.WorldObjects.Generators;

    public GeneratorState? GetGenerator(PlayerTeam team)
    {
        for (var index = 0; index < _host.WorldObjects.Generators.Count; index += 1)
        {
            if (_host.WorldObjects.Generators[index].Team == team)
            {
                return _host.WorldObjects.Generators[index];
            }
        }

        return null;
    }

    internal void ResetGeneratorStateForNewRound()
    {
        _host.WorldObjects.Generators.Clear();

        var generatorMarkers = _host.Level.GetRoomObjects(RoomObjectType.Generator);
        for (var index = 0; index < generatorMarkers.Count; index += 1)
        {
            var marker = generatorMarkers[index];
            if (!marker.Team.HasValue)
            {
                continue;
            }

            _host.WorldObjects.Generators.Add(new GeneratorState(marker.Team.Value, marker, GeneratorMaxHealth));
        }
    }

    internal static void UpdateGeneratorState()
    {
        // Generator objectives are passive; combat systems drive state changes.
    }


    internal bool TryDamageGenerator(PlayerTeam targetTeam, float damage, PlayerEntity? attacker = null)
    {
        if (_host.CompetitiveObjectivesLocked)
        {
            return false;
        }

        var generator = GetGenerator(targetTeam);
        if (generator is null || generator.IsDestroyed)
        {
            return false;
        }

        var destroyed = _host.Combat.ApplyGeneratorDamage(generator, damage, attacker);
        if (!destroyed)
        {
            return false;
        }

        HandleGeneratorDestroyed(generator);
        return true;
    }

    internal void HandleGeneratorDestroyed(GeneratorState generator)
    {
        if (_host.MatchState.IsEnded)
        {
            return;
        }

        var winner = _host.GetOpposingTeam(generator.Team);
        if (!_host.Decisions.TryAwardTeamScore(winner, 1, "generator_destroyed"))
        {
            return;
        }

        _host.WorldEffects.RegisterWorldSoundEvent("ExplosionSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        _host.WorldEffects.RegisterWorldSoundEvent("RevolverSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        _host.WorldEffects.RegisterWorldSoundEvent("CPBeginCapSnd", generator.Marker.CenterX, generator.Marker.CenterY);
        _host.WorldEffects.RegisterVisualEffect("Explosion", generator.Marker.CenterX, generator.Marker.CenterY, count: 2);
        _host.KillFeedRules.RecordGeneratorDestroyedObjectiveLog(winner);
        ApplyGeneratorExplosion(generator);

        _host.Decisions.TryEndRound(winner, "generator_destroyed");
    }

    internal void ApplyGeneratorExplosion(GeneratorState generator)
    {
        var centerX = generator.Marker.CenterX;
        var centerY = generator.Marker.CenterY;

        var playersToKill = new List<PlayerEntity>();
        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = SimulationMath.DistanceBetween(centerX, centerY, player.X, player.Y);
            if (distance >= GeneratorExplosionBlastRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / GeneratorExplosionBlastRadius);
            _host.ApplyExplosionImpulse(player, centerX, centerY, GeneratorExplosionPlayerKnockback * distanceFactor * LegacyMovementModel.SourceTicksPerSecond);
            playersToKill.Add(player);
        }

        for (var index = 0; index < playersToKill.Count; index += 1)
        {
            var player = playersToKill[index];
            if (player.IsAlive)
            {
                _host.PlayerDeaths.KillPlayer(player, gibbed: true, weaponSpriteName: "ExplodeKL");
            }
        }

        var sentryIdsToDestroy = new List<int>();
        for (var sentryIndex = 0; sentryIndex < _host.WorldObjects.Sentries.Count; sentryIndex += 1)
        {
            var sentry = _host.WorldObjects.Sentries[sentryIndex];
            if (SimulationMath.DistanceBetween(centerX, centerY, sentry.X, sentry.Y) < GeneratorExplosionBlastRadius)
            {
                sentryIdsToDestroy.Add(sentry.Id);
            }
        }

        for (var index = 0; index < sentryIdsToDestroy.Count; index += 1)
        {
            for (var sentryIndex = _host.WorldObjects.Sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
            {
                if (_host.WorldObjects.Sentries[sentryIndex].Id == sentryIdsToDestroy[index])
                {
                    _host.Structures.DestroySentry(_host.WorldObjects.Sentries[sentryIndex]);
                    break;
                }
            }
        }

        var rocketIdsToExplode = new List<int>();
        for (var rocketIndex = 0; rocketIndex < _host.Rockets.Count; rocketIndex += 1)
        {
            if (SimulationMath.DistanceBetween(centerX, centerY, _host.Rockets[rocketIndex].X, _host.Rockets[rocketIndex].Y) < GeneratorExplosionBlastRadius)
            {
                rocketIdsToExplode.Add(_host.Rockets[rocketIndex].Id);
            }
        }

        for (var index = 0; index < rocketIdsToExplode.Count; index += 1)
        {
            for (var rocketIndex = _host.Rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
            {
                if (_host.Rockets[rocketIndex].Id == rocketIdsToExplode[index])
                {
                    _host.ExplosionRules.ExplodeRocket(_host.Rockets[rocketIndex], directHitPlayer: null, directHitSentry: null, directHitGenerator: null);
                    break;
                }
            }
        }

        _host.ExplosionRules.ApplyDeadBodyExplosionImpulse(centerX, centerY, GeneratorExplosionBlastRadius, GeneratorExplosionDeadBodyKnockback);

        var mineIdsToExplode = new List<int>();
        for (var mineIndex = 0; mineIndex < _host.Mines.Count; mineIndex += 1)
        {
            if (SimulationMath.DistanceBetween(centerX, centerY, _host.Mines[mineIndex].X, _host.Mines[mineIndex].Y) < GeneratorExplosionBlastRadius)
            {
                mineIdsToExplode.Add(_host.Mines[mineIndex].Id);
            }
        }

        for (var index = 0; index < mineIdsToExplode.Count; index += 1)
        {
            var mine = _host.ExplosionRules.FindMineById(mineIdsToExplode[index]);
            if (mine is not null)
            {
                _host.ExplosionRules.ExplodeMine(mine);
            }
        }

        _host.ExplosionRules.ApplyPlayerGibExplosionImpulse(centerX, centerY, GeneratorExplosionBlastRadius, GeneratorExplosionGibKnockback);

        for (var bubbleIndex = _host.Bubbles.Count - 1; bubbleIndex >= 0; bubbleIndex -= 1)
        {
            if (SimulationMath.DistanceBetween(centerX, centerY, _host.Bubbles[bubbleIndex].X, _host.Bubbles[bubbleIndex].Y) < GeneratorExplosionBlastRadius)
            {
                _host.RemoveBubbleAt(bubbleIndex);
            }
        }
    }

    internal void ApplySnapshotGenerators(SnapshotMessage snapshot)
    {
        if ((GameModeKind)snapshot.GameMode != GameModeKind.Generator)
        {
            _host.WorldObjects.Generators.Clear();
            return;
        }

        ResetGeneratorStateForNewRound();
        if (_host.WorldObjects.Generators.Count == 0 || snapshot.Generators.Count == 0)
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
}
