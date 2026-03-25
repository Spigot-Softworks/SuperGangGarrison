namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public void SpawnEnemyDummy()
    {
        if (!Config.EnableLocalDummies || !Config.EnableEnemyTrainingDummy)
        {
            return;
        }

        EnemyPlayerEnabled = true;
        _enemyDummyRespawnTicks = 0;
        EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
        SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, ReserveSpawn(EnemyPlayer, _enemyDummyTeam));
    }

    public void DespawnEnemyDummy()
    {
        if (!Config.EnableLocalDummies || !Config.EnableEnemyTrainingDummy)
        {
            EnemyPlayerEnabled = false;
            return;
        }

        EnemyPlayerEnabled = false;
        _enemyDummyRespawnTicks = 0;
        ClearEnemyInputOverride();
        EnemyPlayer.ClearMedicHealingTarget();
        EnemyPlayer.Kill();
    }

    public void SpawnFriendlyDummy()
    {
        if (!Config.EnableLocalDummies || !Config.EnableFriendlySupportDummy)
        {
            return;
        }

        FriendlyDummyEnabled = true;
        FriendlyDummy.SetClassDefinition(_friendlyDummyClassDefinition);
        var spawn = FindFriendlyDummySpawnNearLocalPlayer();
        SpawnPlayerResolved(FriendlyDummy, LocalPlayerTeam, spawn.X, spawn.Y, clearMedicHealingTarget: false);
    }

    public void DespawnFriendlyDummy()
    {
        FriendlyDummyEnabled = false;
        FriendlyDummy.ClearMedicHealingTarget();
        FriendlyDummy.Kill();
    }

    public void SetFriendlyDummyHealth(int health)
    {
        if (!Config.EnableLocalDummies || !Config.EnableFriendlySupportDummy)
        {
            return;
        }

        if (!FriendlyDummyEnabled)
        {
            SpawnFriendlyDummy();
        }

        FriendlyDummy.ForceSetHealth(health);
    }

    public void SetEnemyPlayerName(string displayName)
    {
        EnemyPlayer.SetDisplayName(displayName);
    }

    public void SetFriendlyDummyName(string displayName)
    {
        FriendlyDummy.SetDisplayName(displayName);
    }

    public void SetEnemyPlayerTeam(PlayerTeam team)
    {
        if (!Config.EnableLocalDummies)
        {
            return;
        }

        _enemyDummyTeam = team;
        if (EnemyPlayerEnabled)
        {
            EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
            SpawnPlayerResolved(EnemyPlayer, team, ReserveSpawn(EnemyPlayer, team));
        }
    }

    private void AdvanceEnemyDummy()
    {
        if (!EnemyPlayerEnabled)
        {
            return;
        }

        var input = ResolveEnemyDummyInput();
        var previousInput = _previousEnemyInput;
        if (EnemyPlayer.IsAlive)
        {
            AdvanceAlivePlayerWithInput(EnemyPlayer, input, previousInput, _enemyDummyTeam, allowDebugKill: false);
        }
        else
        {
            AdvanceEnemyDummyRespawnTimer();
            _enemyInput = default;
            input = default;
        }

        _previousEnemyInput = input;
    }

    private PlayerInputSnapshot ResolveEnemyDummyInput()
    {
        if (!_enemyInputOverrideActive)
        {
            _enemyInput = BuildEnemyInput();
        }

        return _enemyInput;
    }

    private (float X, float Y) FindFriendlyDummySpawnNearLocalPlayer()
    {
        var candidateOffsets = new[]
        {
            96f,
            -96f,
            144f,
            -144f,
            192f,
            -192f,
        };

        foreach (var offset in candidateOffsets)
        {
            var candidateX = Bounds.ClampX(LocalPlayer.X + offset, FriendlyDummy.Width);
            var candidateY = Bounds.ClampY(LocalPlayer.Y, FriendlyDummy.Height);
            if (CanPlaceDebugDummyAt(candidateX, candidateY, FriendlyDummy.Width, FriendlyDummy.Height, LocalPlayerTeam))
            {
                return (candidateX, candidateY);
            }
        }

        return (
            Bounds.ClampX(LocalPlayer.X + 96f, FriendlyDummy.Width),
            Bounds.ClampY(LocalPlayer.Y, FriendlyDummy.Height));
    }

    private bool CanPlaceDebugDummyAt(float x, float y, float width, float height, PlayerTeam team)
    {
        var left = x - width / 2f;
        var right = x + width / 2f;
        var top = y - height / 2f;
        var bottom = y + height / 2f;

        foreach (var solid in Level.Solids)
        {
            if (left < solid.Right
                && right > solid.Left
                && top < solid.Bottom
                && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var gate in Level.GetBlockingTeamGates(team, false))
        {
            var gateLeft = gate.Left;
            var gateRight = gate.Right;
            var gateTop = gate.Top;
            var gateBottom = gate.Bottom;
            if (left < gateRight
                && right > gateLeft
                && top < gateBottom
                && bottom > gateTop)
            {
                return false;
            }
        }

        foreach (var wall in Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            var wallLeft = wall.Left;
            var wallRight = wall.Right;
            var wallTop = wall.Top;
            var wallBottom = wall.Bottom;
            if (left < wallRight
                && right > wallLeft
                && top < wallBottom
                && bottom > wallTop)
            {
                return false;
            }
        }

        foreach (var roomObject in Level.RoomObjects)
        {
            if (roomObject.Type != RoomObjectType.HealingCabinet)
            {
                continue;
            }

            var cabinetLeft = roomObject.Left;
            var cabinetRight = roomObject.Right;
            var cabinetTop = roomObject.Top;
            var cabinetBottom = roomObject.Bottom;
            if (left < cabinetRight
                && right > cabinetLeft
                && top < cabinetBottom
                && bottom > cabinetTop)
            {
                return false;
            }
        }

        return true;
    }

    private PlayerInputSnapshot BuildEnemyInput()
    {
        var horizontalDelta = LocalPlayer.X - EnemyPlayer.X;
        var verticalDelta = LocalPlayer.Y - EnemyPlayer.Y;
        if (!float.IsFinite(horizontalDelta) || !float.IsFinite(verticalDelta))
        {
            return new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: EnemyPlayer.X,
                AimWorldY: EnemyPlayer.Y,
                DebugKill: false);
        }

        var absoluteHorizontal = MathF.Abs(horizontalDelta);
        var desiredDirection = MathF.Sign(horizontalDelta);
        var strafeDirection = GetEnemyStrafeDirection();

        var moveDirection = 0f;
        if (absoluteHorizontal > 220f)
        {
            moveDirection = desiredDirection;
        }
        else if (absoluteHorizontal < 96f)
        {
            moveDirection = -desiredDirection;
        }
        else
        {
            moveDirection = strafeDirection;
        }

        var jump = EnemyPlayer.IsGrounded
            && ((verticalDelta < -24f && absoluteHorizontal < 280f)
                || WouldRunIntoWall(EnemyPlayer, moveDirection));
        var fire = LocalPlayer.IsAlive
            && absoluteHorizontal < 360f
            && MathF.Abs(verticalDelta) < 140f
            && HasLineOfSight(EnemyPlayer, LocalPlayer);

        return new PlayerInputSnapshot(
            Left: moveDirection < 0f,
            Right: moveDirection > 0f,
            Up: jump,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: fire,
            FireSecondary: false,
            AimWorldX: LocalPlayer.X,
            AimWorldY: LocalPlayer.Y - (LocalPlayer.Height / 4f),
            DebugKill: false);
    }

    private int GetEnemyStrafeDirection()
    {
        if (_enemyStrafeTicksRemaining > 0)
        {
            _enemyStrafeTicksRemaining -= 1;
            return _enemyStrafeDirection;
        }

        _enemyStrafeTicksRemaining = 30 + _random.Next(30);
        _enemyStrafeDirection = _random.Next(2) == 0 ? -1 : 1;
        return _enemyStrafeDirection;
    }
}
