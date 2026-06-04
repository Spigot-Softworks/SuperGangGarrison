namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float PracticeCombatDummyDpsMinimumElapsedSeconds = 1f;
    private const float PracticeCombatDummyFullIntensityDamage = 1200f;

    private bool _practiceCombatDummyActive;
    private int _practiceCombatDummyTotalDamage;
    private long _practiceCombatDummyFirstDamageFrame = -1;
    private float _practiceCombatDummyContinuousDamageAccumulator;

    public bool PracticeCombatDummyActive => _practiceCombatDummyActive && EnemyPlayerEnabled;

    public int PracticeCombatDummyTotalDamage => PracticeCombatDummyActive
        ? _practiceCombatDummyTotalDamage
        : 0;

    public double PracticeCombatDummyDps
    {
        get
        {
            if (!PracticeCombatDummyActive
                || _practiceCombatDummyTotalDamage <= 0
                || _practiceCombatDummyFirstDamageFrame < 0)
            {
                return 0d;
            }

            var elapsedFrames = Math.Max(1, Frame - _practiceCombatDummyFirstDamageFrame + 1);
            var elapsedSeconds = Math.Max(PracticeCombatDummyDpsMinimumElapsedSeconds, elapsedFrames * Config.FixedDeltaSeconds);
            return _practiceCombatDummyTotalDamage / elapsedSeconds;
        }
    }

    public float PracticeCombatDummyDamageIntensity => PracticeCombatDummyActive
        ? Math.Clamp(_practiceCombatDummyTotalDamage / PracticeCombatDummyFullIntensityDamage, 0f, 1f)
        : 0f;

    public bool IsPracticeCombatDummy(PlayerEntity player)
    {
        return PracticeCombatDummyActive && ReferenceEquals(player, EnemyPlayer);
    }

    public void SpawnEnemyDummy()
    {
        DisablePracticeCombatDummyMode(resetStats: true);
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
        DisablePracticeCombatDummyMode(resetStats: true);
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

    public void SpawnPracticeCombatDummy()
    {
        if (!Config.EnableLocalDummies || !Config.EnableEnemyTrainingDummy)
        {
            return;
        }

        EnemyPlayerEnabled = true;
        _practiceCombatDummyActive = true;
        ResetPracticeCombatDummyStats();
        _enemyDummyRespawnTicks = 0;
        ClearEnemyInputOverride();
        EnemyPlayer.ClearMedicHealingTarget();
        SpawnPracticeCombatDummyResolved(playRespawnSound: false);
    }

    public void DespawnPracticeCombatDummy()
    {
        DespawnEnemyDummy();
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
            if (_practiceCombatDummyActive)
            {
                SpawnPracticeCombatDummyResolved(playRespawnSound: false);
            }
            else
            {
                EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
                SpawnPlayerResolved(EnemyPlayer, team, ReserveSpawn(EnemyPlayer, team));
            }
        }
    }

    private void AdvanceEnemyDummy()
    {
        if (!EnemyPlayerEnabled)
        {
            return;
        }

        var input = _practiceCombatDummyActive
            ? BuildPracticeCombatDummyInput()
            : ResolveEnemyDummyInput();
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

    private PlayerInputSnapshot BuildPracticeCombatDummyInput()
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
            AimWorldX: LocalPlayer.X,
            AimWorldY: LocalPlayer.Y - (LocalPlayer.Height / 4f),
            DebugKill: false);
    }

    private bool SpawnPracticeCombatDummyResolved(bool playRespawnSound)
    {
        EnemyPlayer.SetClassDefinition(CharacterClassCatalog.Heavy);
        var spawn = FindEnemyDummySpawnNearLocalPlayer();
        if (SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, spawn.X, spawn.Y, playRespawnSound: playRespawnSound))
        {
            EnemyPlayer.SetAimWorldPosition(LocalPlayer.X, LocalPlayer.Y - (LocalPlayer.Height / 4f));
            return true;
        }

        var fallbackSpawn = ReserveSpawn(EnemyPlayer, _enemyDummyTeam);
        var spawned = SpawnPlayerResolved(EnemyPlayer, _enemyDummyTeam, fallbackSpawn, playRespawnSound: playRespawnSound);
        EnemyPlayer.SetAimWorldPosition(LocalPlayer.X, LocalPlayer.Y - (LocalPlayer.Height / 4f));
        return spawned;
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

    private (float X, float Y) FindEnemyDummySpawnNearLocalPlayer()
    {
        var candidateOffsets = new[]
        {
            112f,
            -112f,
            160f,
            -160f,
            80f,
            -80f,
            224f,
            -224f,
        };

        foreach (var offset in candidateOffsets)
        {
            var candidateX = Bounds.ClampX(LocalPlayer.X + offset, EnemyPlayer.Width);
            var candidateY = Bounds.ClampY(LocalPlayer.Y, EnemyPlayer.Height);
            if (CanPlaceDebugDummyAt(candidateX, candidateY, EnemyPlayer.Width, EnemyPlayer.Height, _enemyDummyTeam))
            {
                return (candidateX, candidateY);
            }
        }

        return (
            Bounds.ClampX(LocalPlayer.X + 112f, EnemyPlayer.Width),
            Bounds.ClampY(LocalPlayer.Y, EnemyPlayer.Height));
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

    private void DisablePracticeCombatDummyMode(bool resetStats)
    {
        _practiceCombatDummyActive = false;
        if (resetStats)
        {
            ResetPracticeCombatDummyStats();
        }
    }

    private void ResetPracticeCombatDummyStats()
    {
        _practiceCombatDummyTotalDamage = 0;
        _practiceCombatDummyFirstDamageFrame = -1;
        _practiceCombatDummyContinuousDamageAccumulator = 0f;
    }

    private bool TryAbsorbPracticeCombatDummyDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags)
    {
        if (!IsPracticeCombatDummy(target))
        {
            return false;
        }

        RegisterPracticeCombatDummyDamage(target, damage, attacker, damageFlags);
        return true;
    }

    private bool TryAbsorbPracticeCombatDummyContinuousDamage(
        PlayerEntity target,
        float damage,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags)
    {
        if (!IsPracticeCombatDummy(target))
        {
            return false;
        }

        _practiceCombatDummyContinuousDamageAccumulator += damage;
        var wholeDamage = (int)_practiceCombatDummyContinuousDamageAccumulator;
        if (wholeDamage > 0)
        {
            _practiceCombatDummyContinuousDamageAccumulator -= wholeDamage;
            RegisterPracticeCombatDummyDamage(target, wholeDamage, attacker, damageFlags);
        }

        return true;
    }

    private bool TryAbsorbPracticeCombatDummyTickDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker)
    {
        if (!IsPracticeCombatDummy(target))
        {
            return false;
        }

        RegisterPracticeCombatDummyDamage(target, damage, attacker, DamageEventFlags.None);
        return true;
    }

    private void RegisterPracticeCombatDummyDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags)
    {
        if (damage <= 0)
        {
            target.ForceSetHealth(target.MaxHealth);
            return;
        }

        if (_practiceCombatDummyFirstDamageFrame < 0)
        {
            _practiceCombatDummyFirstDamageFrame = Frame;
        }

        _practiceCombatDummyTotalDamage = (int)Math.Min(int.MaxValue, _practiceCombatDummyTotalDamage + (long)damage);
        target.ForceSetHealth(target.MaxHealth);
        RegisterDamageEvent(
            attacker,
            DamageTargetKind.Player,
            target.Id,
            target.X,
            target.Y,
            damage,
            wasFatal: false,
            target,
            damageFlags);
    }
}
