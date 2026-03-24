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
        bool InheritPlayerVelocity = false);

    private void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1)
    {
        _pendingVisualEvents.Add(new WorldVisualEvent("Blood", x, y, NormalizeAngleDegrees(directionDegrees), Math.Max(1, count)));
    }

    private void RegisterVisualEffect(string effectName, float x, float y, float directionDegrees = 0f, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(effectName))
        {
            return;
        }

        _pendingVisualEvents.Add(new WorldVisualEvent(effectName, x, y, NormalizeAngleDegrees(directionDegrees), Math.Max(1, count)));
    }

    private void RegisterSoundEvent(PlayerEntity attacker, string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _pendingSoundEvents.Add(new WorldSoundEvent(soundName, attacker.X, attacker.Y));
    }

    private void RegisterWorldSoundEvent(string soundName, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _pendingSoundEvents.Add(new WorldSoundEvent(soundName, x, y));
    }

    private static PlayerTeam GetOpposingTeam(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? PlayerTeam.Red : PlayerTeam.Blue;
    }
}
