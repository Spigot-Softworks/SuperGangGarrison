namespace OpenGarrison.Core.BotBrain;

// Movement-only probes have no SimulationWorld. Apply the room forces that
// normally precede PlayerEntity.Advance and reject lethal contacts without
// producing sounds, deaths, or other changes to the live world.
internal static class BotNavigationEnvironment
{
    public static bool HasTraversalEffects(SimpleLevel level) =>
        level.MoveBoxIndices.Length > 0
        || level.GetRoomObjectIndices(RoomObjectType.FragBox).Length > 0
        || level.GetRoomObjectIndices(RoomObjectType.KillBox).Length > 0;

    public static void ApplyForces(SimpleLevel level, PlayerEntity player)
    {
        foreach (var index in level.MoveBoxIndices)
        {
            ref readonly var marker = ref level.GetRoomObject(index);
            if (!player.IntersectsMarker(marker.CenterX, marker.CenterY, marker.Width, marker.Height))
                continue;
            var impulse = marker.GetMoveBoxImpulse();
            if (impulse.X == 0f && impulse.Y == 0f)
                continue;
            player.SetMovementState(LegacyMovementState.None);
            player.AddImpulse(impulse.X, impulse.Y);
        }
    }

    public static void AdvanceProbe(SimpleLevel level, PlayerEntity player, PlayerInputSnapshot input, bool jumpPressed, PlayerTeam team)
    {
        ApplyForces(level, player);
        player.Advance(input, jumpPressed, level, team, 1d / SimulationConfig.DefaultTicksPerSecond);
    }

    public static bool IsSafe(SimpleLevel level, PlayerEntity player) =>
        player.IsAlive && player.Top <= level.Bounds.Height
        && !IntersectsLethalZone(level, player.Left, player.Top, player.Right, player.Bottom);

    public static bool IntersectsLethalZone(SimpleLevel level, float left, float top, float right, float bottom)
    {
        foreach (var index in level.HazardIndices)
        {
            ref readonly var marker = ref level.GetRoomObject(index);
            if (marker.Type is RoomObjectType.FragBox or RoomObjectType.KillBox
                && Overlaps(left, top, right, bottom, marker))
                return true;
        }
        return false;
    }

    public static bool HasOpposingForce(SimpleLevel level, float fromX, float fromY, float toX, float toY,
        float leftOffset, float topOffset, float rightOffset, float bottomOffset)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) / 12f));
        foreach (var index in level.MoveBoxIndices)
        {
            ref readonly var marker = ref level.GetRoomObject(index);
            var impulse = marker.GetMoveBoxImpulse();
            // A push is applied every tick, so compare it with one tick of
            // walking control rather than the player's maximum running speed.
            // Use the fastest stock class to keep the shared graph permissive.
            if (impulse.X * dx >= 0f
                || LegacyMovementModel.AdvanceHorizontalSpeed(-MathF.Abs(impulse.X),
                    CharacterClassCatalog.Scout.RunPower, 1f, true, 1f,
                    LegacyMovementState.None, false, 1f / SimulationConfig.DefaultTicksPerSecond) > 0f)
                continue;
            for (var step = 0; step <= steps; step++)
            {
                var x = fromX + dx * step / steps;
                var y = fromY + dy * step / steps;
                if (Overlaps(x + leftOffset, y + topOffset, x + rightOffset, y + bottomOffset, marker))
                    return true;
            }
        }
        return false;
    }

    public static bool HasLethalFall(SimpleLevel level, PlayerEntity player, float x)
    {
        var left = x + player.CollisionLeftOffset;
        var right = x + player.CollisionRightOffset;
        var hazardTop = float.PositiveInfinity;
        foreach (var index in level.HazardIndices)
        {
            ref readonly var marker = ref level.GetRoomObject(index);
            if (marker.Type is RoomObjectType.FragBox or RoomObjectType.KillBox
                && marker.Right > left && marker.Left < right && marker.Bottom >= player.Bottom)
                hazardTop = MathF.Min(hazardTop, marker.Top);
        }
        // Most candidate endpoints have no hazard below them. Avoid scanning
        // every solid for each local movement candidate in that common case.
        if (float.IsPositiveInfinity(hazardTop))
            return false;
        var supportTop = float.PositiveInfinity;
        foreach (var solid in level.Solids)
            if (solid.Right > left && solid.Left < right && solid.Top >= player.Bottom - 6f)
                supportTop = MathF.Min(supportTop, solid.Top);
        foreach (var platform in level.GetRoomObjects(RoomObjectType.DropdownPlatform))
            if (platform.Right > left && platform.Left < right && platform.Top >= player.Bottom - 6f)
                supportTop = MathF.Min(supportTop, platform.Top);
        return hazardTop <= supportTop;
    }

    public static bool HasSafeLanding(SimpleLevel level, PlayerEntity player, PlayerTeam team, PlayerInputSnapshot input)
    {
        if (player.IsGrounded || !HasLethalFall(level, player, player.X))
            return true;
        var probe = new PlayerEntity(-901_101, player.ClassDefinition, "BotLandingProbe");
        var state = player.CapturePredictionState();
        probe.RestorePredictionState(in state);
        input = input with { Up = false, Down = false };
        for (var tick = 0; tick < 90; tick++)
        {
            AdvanceProbe(level, probe, input, false, team);
            if (!IsSafe(level, probe))
                return false;
            if (probe.IsGrounded)
                return true;
        }
        return false;
    }

    private static bool Overlaps(float left, float top, float right, float bottom, in RoomObjectMarker marker) =>
        left < marker.Right && right > marker.Left && top < marker.Bottom && bottom > marker.Top;
}
