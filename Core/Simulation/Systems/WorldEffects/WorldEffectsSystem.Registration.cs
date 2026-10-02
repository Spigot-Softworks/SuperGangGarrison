namespace OpenGarrison.Core;

internal sealed partial class WorldEffectsSystem
{
    internal void AdvanceCombatTraces()
    {
        _host.PresentationEvents.AdvanceCombatTraces();
    }

    internal void RegisterCombatTrace(float originX, float originY, float directionX, float directionY, float distance, bool hitCharacter, PlayerTeam team = PlayerTeam.Red, bool isSniperTracer = false, bool isCritical = false)
    {
        _host.PresentationEvents.AddCombatTrace(new CombatTrace(
            originX,
            originY,
            originX + directionX * distance,
            originY + directionY * distance,
            SimulationConstants.CombatTraceLifetimeTicks,
            hitCharacter,
            team,
            isSniperTracer,
            isCritical));
    }

    internal void ComputeSniperAimIndicators()
    {
        _host.PresentationEvents.ClearSniperAimIndicators();

        if (!_host.SniperAimIndicatorEnabled)
        {
            return;
        }

        foreach (var player in _host.EnumerateSimulatedPlayers())
        {
            // Only show aim indicator for scoped rifle snipers (not bow).
            if (!player.IsSniperScoped || !player.IsAlive || player.IsSniperBowEquipped)
            {
                continue;
            }

            // Use rounded origin coordinates to match exactly how FireRifle works
            // This prevents flickering when player position changes slightly between ticks
            var originX = MathF.Round(player.X);
            var originY = MathF.Round(player.Y);

            // Calculate direction from weapon origin to aim point (matching FireRifle logic)
            var aimDeltaX = player.AimWorldX - originX;
            var aimDeltaY = player.AimWorldY - originY;
            if (aimDeltaX == 0f && aimDeltaY == 0f)
            {
                aimDeltaX = player.FacingDirectionX;
            }

            var distance = MathF.Sqrt((aimDeltaX * aimDeltaX) + (aimDeltaY * aimDeltaY));
            if (distance <= 0.0001f)
            {
                continue;
            }

            var directionX = aimDeltaX / distance;
            var directionY = aimDeltaY / distance;
            var maxDistance = 2000f; // Maximum raycast distance (same as rifle shot)

            var hitResult = _host.GeometryResolver.ResolveRifleHit(player, originX, originY, directionX, directionY, maxDistance);

            // If we hit any cloaked spy (friendly or enemy) that's not visible, ignore them and use max distance
            // This prevents revealing spy positions through the aim indicator
            var effectiveDistance = hitResult.Distance;
            if (hitResult.HitPlayer is not null)
            {
                var target = hitResult.HitPlayer;
                if (target.ClassId == PlayerClass.Spy && target.IsSpyCloaked && !target.IsSpyVisibleToEnemies)
                {
                    effectiveDistance = maxDistance;
                }
            }

            // Calculate the hit position using the same rounded origin
            var hitX = originX + directionX * effectiveDistance;
            var hitY = originY + directionY * effectiveDistance;

            // Calculate transparency based on charge level
            // 0 ticks = 0.25 (25%), max ticks (120) = 0.80 (80%)
            var chargeRatio = MathF.Min(
                1f,
                player.SniperChargeTicks / (float)player.SniperRifleFullChargeTicks);
            var transparency = 0.25f + (chargeRatio * 0.55f);

            _host.PresentationEvents.AddSniperAimIndicator(new SniperAimIndicator(
                player.Id,
                hitX,
                hitY,
                player.Team,
                transparency));
        }
    }

    internal void RegisterBloodEffect(float x, float y, float directionDegrees, int count = 1)
    {
        if (!_host.LocalGoreEffectsEnabled)
        {
            return;
        }

        var normalizedDirectionDegrees = SimulationMath.NormalizeAngleDegrees(directionDegrees);
        _host.PresentationEvents.AddVisualEvent(new WorldVisualEvent(
            "Blood",
            x,
            y,
            normalizedDirectionDegrees,
            Math.Max(1, count),
            SourceFrame: _host.Frame < 0 ? 0UL : (ulong)_host.Frame));
        SpawnImpactBloodDrops(x, y, normalizedDirectionDegrees, count);
    }

    private void SpawnImpactBloodDrops(float x, float y, float directionDegrees, int count)
    {
        var dropCount = int.Clamp(4 + Math.Max(0, count - 1), 4, 7);
        var directionRadians = SimulationMath.DegreesToRadians(directionDegrees);
        var lifetimeTicks = _host.ScaleBloodDropLifetimeTicks();
        for (var index = 0; index < dropCount; index += 1)
        {
            var speed = _host.Randoms.Gameplay.NextSingle() * 12f;
            var spreadRadians = SimulationMath.DegreesToRadians((_host.Randoms.Gameplay.NextSingle() * 43f) - 22f);
            var velocityRadians = directionRadians + spreadRadians;
            var bloodDrop = new BloodDropEntity(
                _host.AllocateEntityId(),
                x,
                y,
                DeterministicMath.Cos(velocityRadians) * speed,
                DeterministicMath.Sin(velocityRadians) * speed,
                lifetimeTicks: lifetimeTicks);
            _host.WorldObjects.BloodDrops.Add(bloodDrop);
            _host.EntityStore.Add(bloodDrop);
        }
    }

    internal void RegisterVisualEffect(string effectName, float x, float y, float directionDegrees = 0f, int count = 1, bool normalizeDirection = true)
    {
        if (string.IsNullOrWhiteSpace(effectName))
        {
            return;
        }

        var directionValue = normalizeDirection
            ? SimulationMath.NormalizeAngleDegrees(directionDegrees)
            : directionDegrees;
        _host.PresentationEvents.AddVisualEvent(new WorldVisualEvent(
            effectName,
            x,
            y,
            directionValue,
            Math.Max(1, count),
            SourceFrame: _host.Frame < 0 ? 0UL : (ulong)_host.Frame));
    }

    internal void RegisterStrongDrinkShatterEffect(
        float x,
        float y,
        PlayerTeam team,
        float burstDirectionDegrees = 270f)
    {
        // Direction is the preferred burst axis (surface outward normal). Default 270° = straight up.
        RegisterVisualEffect("BottleShards", x, y, burstDirectionDegrees, count: (int)team);
    }

    internal void RegisterImpactEffect(float x, float y, float directionDegrees)
    {
        RegisterVisualEffect("Impact", x, y, directionDegrees);
    }

    internal void RegisterStuckArrowEffect(
        float hitX,
        float hitY,
        float directionX,
        float directionY,
        ArrowProjectileEntity arrow)
    {
        // Freeze at the sweep contact pose with a slight tip embed into the surface.
        const float embedPixels = 5f;
        var tipOffset = arrow.HitProbeForwardOffset;
        var tipX = hitX + (directionX * embedPixels);
        var tipY = hitY + (directionY * embedPixels);
        arrow.GetBasePositionFromProbeHit(tipX, tipY, directionX, directionY, out var freezeX, out var freezeY);

        // Stairs/slopes are stacks of long RLE walkmask rows. The origin (near the fletching) often
        // lands buried under the slope; pull the whole arrow back until the tail clears solids.
        const float maxPullPixels = 64f;
        for (var pulled = 0f; pulled < maxPullPixels; pulled += 1f)
        {
            if (!IsStuckArrowPointInSolid(freezeX, freezeY))
            {
                break;
            }

            freezeX -= directionX;
            freezeY -= directionY;
        }

        // After pullback, nudge forward so the tip is at least slightly into the surface again.
        tipX = freezeX + (directionX * tipOffset);
        tipY = freezeY + (directionY * tipOffset);
        if (!IsStuckArrowPointInSolid(tipX, tipY))
        {
            for (var pushed = 0; pushed < 12; pushed += 1)
            {
                freezeX += directionX;
                freezeY += directionY;
                tipX += directionX;
                tipY += directionY;
                if (IsStuckArrowPointInSolid(tipX, tipY))
                {
                    break;
                }
            }
        }

        arrow.Land(freezeX, freezeY, directionX, directionY);
        var directionDegrees = DeterministicMath.Atan2(directionY, directionX) * (180f / MathF.PI);
        // Count encodes the ArrowS team frame: Red=1, Blue=2 (matches PlayerTeam values).
        RegisterVisualEffect("StuckArrow", freezeX, freezeY, directionDegrees, count: (int)arrow.Team);
    }

    private bool IsStuckArrowPointInSolid(float x, float y)
    {
        return _host.Level.IntersectsSolid(x, y, x + 0.1f, y + 0.1f);
    }

    internal void RegisterWallspinDustEffect(PlayerEntity player)
    {
        var dustX = player.IsSourceFacingLeft
            ? player.X + player.CollisionRightOffset + 1f
            : player.X + player.CollisionLeftOffset + 2f;
        var dustY = player.Y + player.CollisionBottomOffset - 4f;
        RegisterVisualEffect("WallspinDust", dustX, dustY);
    }

    private void RegisterIntelTrailEffect(float x, float y, float horizontalSpeed)
    {
        RegisterVisualEffect("LooseSheet", x, y, horizontalSpeed, normalizeDirection: false);
    }

    private bool ShouldEmitSourceTickChance(float sourceTickChance)
    {
        if (sourceTickChance <= 0f)
        {
            return false;
        }

        var sourceTicksPerSimulationTick = LegacyMovementModel.SourceTicksPerSecond / (float)_host.Config.TicksPerSecond;
        if (sourceTicksPerSimulationTick <= 0f)
        {
            return false;
        }

        var wholeSourceTicks = (int)MathF.Floor(sourceTicksPerSimulationTick);
        for (var tick = 0; tick < wholeSourceTicks; tick += 1)
        {
            if (_host.Randoms.Gameplay.NextSingle() < sourceTickChance)
            {
                return true;
            }
        }

        var fractionalSourceTick = sourceTicksPerSimulationTick - wholeSourceTicks;
        if (fractionalSourceTick <= 0f)
        {
            return false;
        }

        var fractionalChance = 1f - DeterministicMath.Pow(1f - sourceTickChance, fractionalSourceTick);
        return _host.Randoms.Gameplay.NextSingle() < fractionalChance;
    }

    internal void TryRegisterIntelTrailEffect(PlayerEntity player)
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
            player.Y - 11f + (_host.Randoms.Gameplay.NextSingle() * 9f),
            player.HorizontalSpeed);
    }

    internal void RegisterSoundEvent(PlayerEntity attacker, string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _host.PresentationEvents.AddSoundEvent(new WorldSoundEvent(soundName, attacker.X, attacker.Y, SourceFrame: (ulong)_host.Frame, SourcePlayerId: attacker.Id));
    }

    internal void RegisterWorldSoundEvent(string soundName, float x, float y, int sourcePlayerId = -1)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _host.PresentationEvents.AddSoundEvent(new WorldSoundEvent(soundName, x, y, SourceFrame: (ulong)_host.Frame, SourcePlayerId: sourcePlayerId));
    }
}
