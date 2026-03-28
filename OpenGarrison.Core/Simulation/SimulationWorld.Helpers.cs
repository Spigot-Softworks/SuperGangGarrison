namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void AdvanceCombatTraces()
    {
        for (var traceIndex = _combatTraces.Count - 1; traceIndex >= 0; traceIndex -= 1)
        {
            var trace = _combatTraces[traceIndex];
            if (trace.TicksRemaining <= 1)
            {
                _combatTraces.RemoveAt(traceIndex);
                continue;
            }

            _combatTraces[traceIndex] = trace with { TicksRemaining = trace.TicksRemaining - 1 };
        }
    }

    private void RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team = PlayerTeam.Red, bool isSniperTracer = false)
    {
        _combatTraces.Add(new CombatTrace(
            originX,
            originY,
            originX + directionX * distance,
            originY + directionY * distance,
            CombatTraceLifetimeTicks,
            hitCharacter,
            team,
            isSniperTracer));
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }

    private static float NormalizeAngleDegrees(float degrees)
    {
        while (degrees < 0f)
        {
            degrees += 360f;
        }

        while (degrees >= 360f)
        {
            degrees -= 360f;
        }

        return degrees;
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float GetStabOriginX(StabMaskEntity mask, float directionX)
    {
        return mask.X + directionX * StabMaskEntity.StartOffset;
    }

    private static float GetStabOriginY(StabMaskEntity mask, float directionY)
    {
        return mask.Y + directionY * StabMaskEntity.StartOffset;
    }

    private static float PointDirectionDegrees(float x1, float y1, float x2, float y2)
    {
        var degrees = MathF.Atan2(y2 - y1, x2 - x1) * (180f / MathF.PI);
        if (degrees < 0f)
        {
            degrees += 360f;
        }

        return degrees;
    }

    private readonly record struct PlayerGibPartDefinition(
        string SpriteName,
        int FrameIndex,
        int Count,
        float VelocityRangeX,
        float VelocityRangeY,
        float RotationRange,
        int LifetimeTicks,
        float HorizontalFriction,
        float RotationFriction,
        bool InheritPlayerVelocity = false,
        float BloodChance = PlayerGibEntity.DefaultBloodChance);

    private void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1)
    {
        var normalizedDirectionDegrees = NormalizeAngleDegrees(directionDegrees);
        _pendingVisualEvents.Add(new WorldVisualEvent("Blood", x, y, normalizedDirectionDegrees, Math.Max(1, count)));
        SpawnImpactBloodDrops(x, y, normalizedDirectionDegrees, count);
    }

    private void SpawnImpactBloodDrops(float x, float y, float directionDegrees, int count)
    {
        var dropCount = int.Clamp(4 + Math.Max(0, count - 1), 4, 7);
        var directionRadians = DegreesToRadians(directionDegrees);
        for (var index = 0; index < dropCount; index += 1)
        {
            var speed = _random.NextSingle() * 12f;
            var spreadRadians = DegreesToRadians((_random.NextSingle() * 43f) - 22f);
            var velocityRadians = directionRadians + spreadRadians;
            var bloodDrop = new BloodDropEntity(
                AllocateEntityId(),
                x,
                y,
                MathF.Cos(velocityRadians) * speed,
                MathF.Sin(velocityRadians) * speed);
            _bloodDrops.Add(bloodDrop);
            _entities.Add(bloodDrop.Id, bloodDrop);
        }
    }

    private void RegisterVisualEffect(string effectName, float x, float y, float directionDegrees = 0f, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(effectName))
        {
            return;
        }

        _pendingVisualEvents.Add(new WorldVisualEvent(effectName, x, y, NormalizeAngleDegrees(directionDegrees), Math.Max(1, count)));
    }

    private void RegisterImpactEffect(float x, float y, float directionDegrees)
    {
        RegisterVisualEffect("Impact", x, y, directionDegrees);
    }

    private void RegisterWallspinDustEffect(PlayerEntity player)
    {
        var dustX = player.IsSourceFacingLeft
            ? player.X + player.CollisionRightOffset + 1f
            : player.X + player.CollisionLeftOffset + 2f;
        var dustY = player.Y + player.CollisionBottomOffset - 4f;
        RegisterVisualEffect("WallspinDust", dustX, dustY);
    }

    private void RegisterIntelTrailEffect(float x, float y, float horizontalSpeed)
    {
        RegisterVisualEffect("LooseSheet", x, y, horizontalSpeed);
    }

    private bool ShouldEmitSourceTickChance(float sourceTickChance)
    {
        if (sourceTickChance <= 0f)
        {
            return false;
        }

        var sourceTicksPerSimulationTick = LegacyMovementModel.SourceTicksPerSecond / (float)Config.TicksPerSecond;
        if (sourceTicksPerSimulationTick <= 0f)
        {
            return false;
        }

        var wholeSourceTicks = (int)MathF.Floor(sourceTicksPerSimulationTick);
        for (var tick = 0; tick < wholeSourceTicks; tick += 1)
        {
            if (_random.NextSingle() < sourceTickChance)
            {
                return true;
            }
        }

        var fractionalSourceTick = sourceTicksPerSimulationTick - wholeSourceTicks;
        if (fractionalSourceTick <= 0f)
        {
            return false;
        }

        var fractionalChance = 1f - MathF.Pow(1f - sourceTickChance, fractionalSourceTick);
        return _random.NextSingle() < fractionalChance;
    }

    private void TryRegisterIntelTrailEffect(PlayerEntity player)
    {
        if (!player.IsAlive || !player.IsCarryingIntel)
        {
            return;
        }

        var sourceTickChance = MathF.Abs(player.HorizontalSpeed) > 0.195f * LegacyMovementModel.SourceTicksPerSecond
            ? 0.1f
            : 0.025f;
        if (!ShouldEmitSourceTickChance(sourceTickChance))
        {
            return;
        }

        RegisterIntelTrailEffect(
            player.X,
            player.Y - 11f + (_random.NextSingle() * 9f),
            player.HorizontalSpeed);
    }

    private void RegisterSoundEvent(PlayerEntity attacker, string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _pendingSoundEvents.Add(new WorldSoundEvent(soundName, attacker.X, attacker.Y, SourceFrame: (ulong)Frame));
    }

    private void RegisterWorldSoundEvent(string soundName, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _pendingSoundEvents.Add(new WorldSoundEvent(soundName, x, y, SourceFrame: (ulong)Frame));
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
