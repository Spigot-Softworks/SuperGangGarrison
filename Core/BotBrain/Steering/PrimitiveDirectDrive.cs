namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Small combat steering overlay for nearby visible enemies.
/// </summary>
public static class PrimitiveDirectDrive
{
    private const float MaxDriveDistance = 375f;
    private const float MaxRecoveryEnemyDriveDistance = 900f;
    private const float HorizontalDeadZone = 18f;
    private const float RiseJumpThreshold = 24f;
    private const float DropThroughTargetBelowThreshold = 72f;
    private const float BlockedVerticalSeekThreshold = 48f;
    private const float HeadroomProbeHeight = 64f;
    private const float WallProbeDistance = 18f;
    private const float WallProbeThickness = 3f;
    private const float WallProbeBottomInset = 4f;

    public static bool TryResolve(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        SteeringOutput pathSteering,
        out SteeringOutput directSteering,
        out string trace)
    {
        if (combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            || !self.IsAlive
            || !target.IsAlive)
        {
            directSteering = pathSteering;
            trace = string.Empty;
            return false;
        }

        return TryResolveTarget(
            world,
            self,
            new DirectDriveTarget(DirectDriveTargetKind.Enemy, target.X, target.Y, $"enemy player:{target.Id}"),
            pathSteering,
            MaxDriveDistance,
            useCombatRange: true,
            out directSteering,
            out trace);
    }

    public static bool TryResolveRecovery(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput pathSteering,
        out SteeringOutput directSteering,
        out string trace)
    {
        var maxDistance = target.Kind == DirectDriveTargetKind.Enemy
            || target.Kind == DirectDriveTargetKind.Carrier
            || target.Kind == DirectDriveTargetKind.Escort
            ? MaxRecoveryEnemyDriveDistance
            : float.PositiveInfinity;
        return TryResolveTarget(
            world,
            self,
            target,
            pathSteering,
            maxDistance,
            useCombatRange: false,
            out directSteering,
            out trace);
    }

    public static bool TryResolveSupport(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput pathSteering,
        float maxDistance,
        out SteeringOutput directSteering,
        out string trace)
    {
        return TryResolveTarget(
            world,
            self,
            target,
            pathSteering,
            maxDistance,
            useCombatRange: true,
            out directSteering,
            out trace);
    }

    private static bool TryResolveTarget(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        SteeringOutput pathSteering,
        float maxDistance,
        bool useCombatRange,
        out SteeringOutput directSteering,
        out string trace)
    {
        directSteering = pathSteering;
        trace = string.Empty;

        if (!self.IsAlive)
        {
            return false;
        }

        var dx = target.X - self.X;
        var dy = target.Y - self.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance > maxDistance)
        {
            return false;
        }

        var moveDirection = useCombatRange
            ? ResolveCombatMoveDirection(self, distance, dx)
            : GetMoveDirection(dx);
        var dropDown = ShouldDropTowardTarget(world, self, target, moveDirection);
        var jump = !dropDown && ShouldJumpTowardTarget(world, self, target.Y, moveDirection);
        if (jump
            && target.Y < self.Y - BlockedVerticalSeekThreshold
            && MathF.Abs(dx) <= HorizontalDeadZone
            && HasBlockedHeadroom(world, self))
        {
            return false;
        }

        if (moveDirection == 0 && !jump)
        {
            return false;
        }

        directSteering.MoveDirection = moveDirection;
        directSteering.Jump = jump || pathSteering.Jump;
        directSteering.DropDown = dropDown;
        var modeTrace = useCombatRange ? " combat:spacing" : string.Empty;
        trace = $"directDrive={target.Label}{modeTrace} dx:{dx:0.0} dy:{dy:0.0} dist:{distance:0.0} move:{moveDirection:0} jump:{(directSteering.Jump ? 1 : 0)} drop:{(directSteering.DropDown ? 1 : 0)}";
        return true;
    }

    internal static int ResolveCombatMoveDirection(PlayerEntity self, float distance, float dx)
    {
        var preferredRange = ResolvePreferredRange(self);
        if (distance < preferredRange.Min)
        {
            return ResolveCombatAwayDirection(self, dx);
        }

        if (distance > preferredRange.Max)
        {
            return GetMoveDirection(dx);
        }

        var horizontalDistance = MathF.Abs(dx);
        if (preferredRange.Min > 0f
            && horizontalDistance < preferredRange.Min + (preferredRange.HorizontalTolerance * 0.5f))
        {
            return ResolveCombatAwayDirection(self, dx);
        }

        if (horizontalDistance > preferredRange.Max - (preferredRange.HorizontalTolerance * 0.5f))
        {
            return GetMoveDirection(dx);
        }

        return ResolveCombatLateralDirection(self, dx);
    }

    private static int ResolveCombatLateralDirection(PlayerEntity self, float dx)
    {
        var awayDirection = ResolveCombatAwayDirection(self, dx);
        return self.Id % 2 == 0
            ? awayDirection
            : -awayDirection;
    }

    private static int ResolveCombatAwayDirection(PlayerEntity self, float dx)
    {
        if (MathF.Abs(dx) > 1f)
        {
            return dx > 0f ? -1 : 1;
        }

        return self.Id % 2 == 0 ? 1 : -1;
    }

    private static (float Min, float Max, float HorizontalTolerance) ResolvePreferredRange(PlayerEntity self)
    {
        return self.PrimaryWeapon.Kind switch
        {
            PrimaryWeaponKind.Blade => (24f, 56f, 10f),
            PrimaryWeaponKind.FlameThrower => (45f, 130f, 20f),
            PrimaryWeaponKind.Minigun => (90f, 260f, 48f),
            PrimaryWeaponKind.Rifle => (170f, 340f, 72f),
            PrimaryWeaponKind.RocketLauncher => (120f, 300f, 56f),
            PrimaryWeaponKind.MineLauncher => (100f, 240f, 56f),
            PrimaryWeaponKind.Revolver => (80f, 280f, 48f),
            _ => (70f, 230f, 48f),
        };
    }

    private static bool ShouldJumpTowardTarget(
        SimulationWorld world,
        PlayerEntity self,
        float targetY,
        int moveDirection)
    {
        if (!self.IsGrounded)
        {
            return self.RemainingAirJumps > 0
                && targetY < self.Y - RiseJumpThreshold
                && self.VerticalSpeed > 0f;
        }

        if (targetY < self.Y - RiseJumpThreshold)
        {
            return true;
        }

        return moveDirection != 0 && WouldMoveIntoObstacle(world, self, moveDirection);
    }

    private static bool ShouldDropTowardTarget(
        SimulationWorld world,
        PlayerEntity self,
        DirectDriveTarget target,
        int moveDirection)
    {
        if (!self.IsGrounded
            || moveDirection == 0
            || target.Kind is DirectDriveTargetKind.Enemy or DirectDriveTargetKind.Carrier or DirectDriveTargetKind.Escort
            || target.Y <= self.Y + DropThroughTargetBelowThreshold)
        {
            return false;
        }

        return WouldMoveIntoObstacle(world, self, moveDirection);
    }

    private static bool HasBlockedHeadroom(SimulationWorld world, PlayerEntity player)
    {
        var probeLeft = player.Left + 2f;
        var probeRight = player.Right - 2f;
        var probeTop = player.Top - HeadroomProbeHeight;
        var probeBottom = player.Top - 2f;

        foreach (var solid in world.Level.Solids)
        {
            if (RectanglesOverlap(probeLeft, probeTop, probeRight, probeBottom, solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                return true;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if ((wall.Type == RoomObjectType.PlayerWall || wall.Type == RoomObjectType.BulletWall)
                && RectanglesOverlap(probeLeft, probeTop, probeRight, probeBottom, wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return true;
            }
        }

        foreach (var barrier in world.Level.GetRoomObjects(RoomObjectType.Barrier))
        {
            if (!RectanglesOverlap(probeLeft, probeTop, probeRight, probeBottom, barrier.Left, barrier.Top, barrier.Right, barrier.Bottom))
            {
                continue;
            }

            if (BarrierCollision.BlocksPlayerWithoutDirection(barrier.Barrier, player.Team, player.IsCarryingIntel))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WouldMoveIntoObstacle(SimulationWorld world, PlayerEntity player, int horizontalDirection)
    {
        if (horizontalDirection == 0)
        {
            return false;
        }

        var probeLeft = horizontalDirection > 0
            ? player.Right + WallProbeDistance
            : player.Left - WallProbeDistance - WallProbeThickness;
        var probeRight = probeLeft + WallProbeThickness;
        var probeTop = player.Top;
        var probeBottom = player.Bottom - WallProbeBottomInset;

        var direction = MathF.Sign(horizontalDirection);
        var maxProbeDistance = WallProbeDistance + WallProbeThickness;
        for (var offset = 2f; offset <= maxProbeDistance; offset += 4f)
        {
            if (!player.CanOccupy(world.Level, player.Team, player.X + (direction * offset), player.Y))
            {
                return true;
            }
        }

        player.GetCollisionBounds(out var previousLeft, out var previousTop, out var previousRight, out var previousBottom);
        if (SimpleLevelBarrierCollision.BlocksPlayerAt(
                world.Level,
                player.Team,
                player.IsCarryingIntel,
                previousLeft,
                previousRight,
                previousTop,
                previousBottom,
                probeLeft,
                probeTop,
                probeRight,
                probeBottom))
        {
            return true;
        }

        return false;
    }

    private static int GetMoveDirection(float deltaX)
    {
        if (MathF.Abs(deltaX) <= HorizontalDeadZone)
        {
            return 0;
        }

        return deltaX > 0f ? 1 : -1;
    }

    private static bool RectanglesOverlap(float leftA, float topA, float rightA, float bottomA, float leftB, float topB, float rightB, float bottomB)
    {
        return leftA <= rightB
            && rightA >= leftB
            && topA <= bottomB
            && bottomA >= topB;
    }
}

public enum DirectDriveTargetKind
{
    Enemy,
    Carrier,
    Intel,
    Escort,
    Objective,
}

public readonly record struct DirectDriveTarget(
    DirectDriveTargetKind Kind,
    float X,
    float Y,
    string Label);
