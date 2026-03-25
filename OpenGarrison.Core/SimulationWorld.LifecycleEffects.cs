namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void SpawnPlayerGibs(PlayerEntity player)
    {
        if (!player.IsAlive || DefaultGibLevel <= 1)
        {
            SpawnDeadBody(player);
            return;
        }

        var inheritedVelocityX = player.HorizontalSpeed * (float)Config.FixedDeltaSeconds;
        var inheritedVelocityY = player.VerticalSpeed * (float)Config.FixedDeltaSeconds;
        SpawnPlayerGibSet(player, "GibS", DefaultGibLevel, randomFrameCount: 7, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 210, horizontalFriction: 0.4f, rotationFriction: 0.6f, bloodChance: 1.8f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);
        SpawnPlayerGibSet(player, player.Team == PlayerTeam.Blue ? "BlueClumpS" : "RedClumpS", DefaultGibLevel - 1, randomFrameCount: 4, velocityRangeX: 8f, velocityRangeY: 9f, rotationRange: 72f, lifetimeTicks: 250, horizontalFriction: 0.3f, rotationFriction: 0.4f, bloodChance: 2f, inheritedVelocityX: inheritedVelocityX, inheritedVelocityY: inheritedVelocityY);

        RegisterVisualEffect("GibBlood", player.X, player.Y, count: DefaultGibLevel);
        SpawnBloodDrops(player.X, player.Y, DefaultGibLevel * 14, 10f, 13f, spreadRadius: 11f);

        foreach (var gibPart in GetPlayerGibParts(player))
        {
            SpawnPlayerGibSet(
                player,
                gibPart.SpriteName,
                gibPart.Count,
                frameIndex: gibPart.FrameIndex,
                velocityRangeX: gibPart.VelocityRangeX,
                velocityRangeY: gibPart.VelocityRangeY,
                rotationRange: gibPart.RotationRange,
                lifetimeTicks: gibPart.LifetimeTicks,
                horizontalFriction: gibPart.HorizontalFriction,
                rotationFriction: gibPart.RotationFriction,
                bloodChance: gibPart.BloodChance,
                inheritedVelocityX: gibPart.InheritPlayerVelocity ? inheritedVelocityX : 0f,
                inheritedVelocityY: gibPart.InheritPlayerVelocity ? inheritedVelocityY : 0f);
        }
    }

    private void SpawnPlayerGibSet(
        PlayerEntity player,
        string spriteName,
        int count,
        int? frameIndex = null,
        int randomFrameCount = 0,
        float velocityRangeX = 8f,
        float velocityRangeY = 9f,
        float rotationRange = 52f,
        int lifetimeTicks = 250,
        float horizontalFriction = 0.4f,
        float rotationFriction = 0.5f,
        float bloodChance = PlayerGibEntity.DefaultBloodChance,
        float inheritedVelocityX = 0f,
        float inheritedVelocityY = 0f)
    {
        for (var index = 0; index < count; index += 1)
        {
            var resolvedFrameIndex = frameIndex ?? _random.Next(randomFrameCount);
            var velocityX = inheritedVelocityX + ((_random.NextSingle() * ((velocityRangeX * 2f) + 1f)) - velocityRangeX);
            var velocityY = inheritedVelocityY + ((_random.NextSingle() * ((velocityRangeY * 2f) + 1f)) - velocityRangeY);
            var rotationSpeed = (_random.NextSingle() * ((rotationRange * 2f) + 1f)) - rotationRange;
            var gib = new PlayerGibEntity(
                AllocateEntityId(),
                spriteName,
                resolvedFrameIndex,
                player.X,
                player.Y,
                velocityX,
                velocityY,
                rotationSpeed,
                horizontalFriction,
                rotationFriction,
                lifetimeTicks,
                bloodChance);
            _playerGibs.Add(gib);
            _entities.Add(gib.Id, gib);
        }
    }

    private static IEnumerable<PlayerGibPartDefinition> GetPlayerGibParts(PlayerEntity player)
    {
        switch (player.ClassId)
        {
            case PlayerClass.Scout:
                yield return new PlayerGibPartDefinition("HeadS", 6, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 0, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 1, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Pyro:
                yield return new PlayerGibPartDefinition("HeadS", 7, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("AccesoryS", 4, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                yield return new PlayerGibPartDefinition("FeetS", 1, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Soldier:
                yield return new PlayerGibPartDefinition("HeadS", 1, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 2, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 1, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                yield return new PlayerGibPartDefinition("AccesoryS", player.Team == PlayerTeam.Blue ? 2 : 1, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                break;
            case PlayerClass.Heavy:
                yield return new PlayerGibPartDefinition("HeadS", 2, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 3, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 1, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Demoman:
                yield return new PlayerGibPartDefinition("HeadS", 4, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 4, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Medic:
                yield return new PlayerGibPartDefinition("HeadS", 5, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 4, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", player.Team == PlayerTeam.Blue ? 3 : 2, 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Engineer:
                yield return new PlayerGibPartDefinition("HeadS", 8, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("AccesoryS", 3, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                yield return new PlayerGibPartDefinition("FeetS", 5, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Spy:
                yield return new PlayerGibPartDefinition("HeadS", 3, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("FeetS", 6, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
            case PlayerClass.Sniper:
                yield return new PlayerGibPartDefinition("HeadS", 0, 1, 8f, 9f, 52f, 250, 0.5f, 0.5f, BloodChance: 1.4f);
                yield return new PlayerGibPartDefinition("AccesoryS", 0, 1, 8f, 9f, 52f, 250, 0.4f, 0.2f, InheritPlayerVelocity: true, BloodChance: 28f);
                yield return new PlayerGibPartDefinition("FeetS", 6, DefaultGibLevel - 1, 2f, 0f, 6f, 250, 0.3f, 0.4f, BloodChance: 7f);
                yield return new PlayerGibPartDefinition("HandS", 0, DefaultGibLevel - 1, 8f, 9f, 52f, 250, 0.4f, 0.5f, InheritPlayerVelocity: true, BloodChance: 5f);
                break;
        }
    }

    private void AdvancePlayerGibs()
    {
        for (var gibIndex = _playerGibs.Count - 1; gibIndex >= 0; gibIndex -= 1)
        {
            var gib = _playerGibs[gibIndex];
            gib.Advance(Level, Bounds);
            TrySpawnBloodDropFromGib(gib);
            if (!gib.IsExpired)
            {
                continue;
            }

            _entities.Remove(gib.Id);
            _playerGibs.RemoveAt(gibIndex);
        }
    }

    private void AdvanceBloodDrops()
    {
        for (var dropIndex = _bloodDrops.Count - 1; dropIndex >= 0; dropIndex -= 1)
        {
            var bloodDrop = _bloodDrops[dropIndex];
            bloodDrop.Advance(Level, Bounds);
            if (!bloodDrop.IsExpired)
            {
                continue;
            }

            _entities.Remove(bloodDrop.Id);
            _bloodDrops.RemoveAt(dropIndex);
        }

        MergeBloodDrops();
    }

    private void TrySpawnBloodDropFromGib(PlayerGibEntity gib)
    {
        if (gib.IsExpired || gib.BloodChance <= 0f)
        {
            return;
        }

        var threshold = 16f / DefaultGibLevel;
        if (MathF.Abs(gib.Speed / gib.BloodChance) <= _random.NextSingle() * threshold)
        {
            return;
        }

        var angle = MathF.Atan2(gib.VelocityY, gib.VelocityX);
        var bloodDrop = new BloodDropEntity(
            AllocateEntityId(),
            gib.X,
            gib.Y - 1f,
            MathF.Cos(angle) * gib.Speed * 0.9f + (_random.NextSingle() * 3f) - 1f,
            MathF.Sin(angle) * gib.Speed * 0.9f + (_random.NextSingle() * 3f) - 1f);
        _bloodDrops.Add(bloodDrop);
        _entities.Add(bloodDrop.Id, bloodDrop);
    }

    private void SpawnBloodDrops(float x, float y, int count, float velocityRangeX, float velocityRangeY, float spreadRadius = 0f)
    {
        for (var index = 0; index < count; index += 1)
        {
            var offsetX = spreadRadius <= 0f ? 0f : (_random.NextSingle() * ((spreadRadius * 2f) + 1f)) - spreadRadius;
            var offsetY = spreadRadius <= 0f ? 0f : (_random.NextSingle() * ((spreadRadius * 2f) + 1f)) - spreadRadius;
            var velocityX = (_random.NextSingle() * ((velocityRangeX * 2f) + 1f)) - velocityRangeX;
            var velocityY = (_random.NextSingle() * ((velocityRangeY * 2f) + 1f)) - velocityRangeY;
            var bloodDrop = new BloodDropEntity(AllocateEntityId(), x + offsetX, y + offsetY, velocityX, velocityY);
            _bloodDrops.Add(bloodDrop);
            _entities.Add(bloodDrop.Id, bloodDrop);
        }
    }

    private void MergeBloodDrops()
    {
        if (_bloodDrops.Count < 2)
        {
            return;
        }

        for (var sourceIndex = _bloodDrops.Count - 1; sourceIndex >= 0; sourceIndex -= 1)
        {
            var source = _bloodDrops[sourceIndex];
            if (!source.IsMergeable)
            {
                continue;
            }

            for (var targetIndex = 0; targetIndex < sourceIndex; targetIndex += 1)
            {
                var target = _bloodDrops[targetIndex];
                if (!target.CanMergeWith(source) || !ShouldMergeBloodDrops(target, source))
                {
                    continue;
                }

                target.Absorb(source);
                _entities.Remove(source.Id);
                _bloodDrops.RemoveAt(sourceIndex);
                break;
            }
        }
    }

    private bool ShouldMergeBloodDrops(BloodDropEntity target, BloodDropEntity source)
    {
        return (((ulong)Frame + (ulong)target.Id + (ulong)source.Id) & 7UL) == 0UL;
    }

    private void AdvanceDeadBodies()
    {
        for (var deadBodyIndex = _deadBodies.Count - 1; deadBodyIndex >= 0; deadBodyIndex -= 1)
        {
            var deadBody = _deadBodies[deadBodyIndex];
            deadBody.Advance(Level, Bounds);
            if (!deadBody.IsExpired)
            {
                continue;
            }

            _entities.Remove(deadBody.Id);
            _deadBodies.RemoveAt(deadBodyIndex);
        }
    }
}
