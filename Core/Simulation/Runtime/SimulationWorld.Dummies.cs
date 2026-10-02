namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const double PracticeCombatDummyDpsMinimumElapsedSeconds = 1d;
    private const double PracticeCombatDummyBurstTimeoutSeconds = 4d;
    private const float PracticeCombatDummyFullIntensityDamage = 1200f;

    public bool PracticeCombatDummyActive => DummyState.CombatMode == PracticeCombatDummyMode.Combat && EnemyPlayerEnabled;

    public bool PracticeDpsDummyActive => DummyState.CombatMode == PracticeCombatDummyMode.Dps && EnemyPlayerEnabled;

    public int PracticeCombatDummyTotalDamage => PracticeDpsDummyActive
        && PracticeCombatDummyDpsVisible
        ? DummyState.CombatTotalDamage
        : 0;

    public bool PracticeCombatDummyDpsVisible => PracticeDpsDummyActive
        && DummyState.CombatTotalDamage > 0
        && DummyState.CombatFirstDamageFrame >= 0
        && DummyState.CombatLastDamageFrame >= 0
        && !IsPracticeCombatDummyBurstExpired();

    public double PracticeCombatDummyDps
    {
        get
        {
            if (!PracticeCombatDummyDpsVisible)
            {
                return 0d;
            }

            var elapsedFrames = Math.Max(1, Frame - DummyState.CombatFirstDamageFrame + 1);
            var elapsedSeconds = Math.Max(PracticeCombatDummyDpsMinimumElapsedSeconds, elapsedFrames * Config.FixedDeltaSeconds);
            return DummyState.CombatTotalDamage / elapsedSeconds;
        }
    }

    public float PracticeCombatDummyDamageIntensity => PracticeCombatDummyDpsVisible
        ? Math.Clamp(DummyState.CombatTotalDamage / PracticeCombatDummyFullIntensityDamage, 0f, 1f)
        : 0f;

    public bool IsPracticeCombatDummy(PlayerEntity player)
    {
        return PracticeCombatDummyActive && ReferenceEquals(player, EnemyPlayer);
    }

    public bool IsPracticeDpsDummy(PlayerEntity player)
    {
        return PracticeDpsDummyActive && ReferenceEquals(player, EnemyPlayer);
    }

    private bool IsPracticeDummy(PlayerEntity player)
    {
        return DummyState.CombatMode != PracticeCombatDummyMode.None
            && EnemyPlayerEnabled
            && ReferenceEquals(player, EnemyPlayer);
    }

    public void SpawnEnemyDummy()
    {
        DisablePracticeCombatDummyMode(resetStats: true);
        if (!Config.EnableLocalDummies || !Config.EnableEnemyTrainingDummy)
        {
            return;
        }

        EnemyPlayerEnabled = true;
        DummyState.EnemyRespawnTicks = 0;
        EnemyPlayer.SetClassDefinition(DummyState.EnemyClassDefinition);
        SpawnPlayerResolved(EnemyPlayer, DummyState.EnemyTeam, ReserveSpawn(EnemyPlayer, DummyState.EnemyTeam));
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
        DummyState.EnemyRespawnTicks = 0;
        ClearEnemyInputOverride();
        EnemyPlayer.ClearMedicHealingTarget();
        EnemyPlayer.Kill();
    }

    public void SpawnPracticeCombatDummy()
    {
        SpawnPracticeCombatDummy(PracticeCombatDummyMode.Combat, CharacterClassCatalog.Heavy);
    }

    public void SpawnPracticeCombatDummy(PlayerClass playerClass)
    {
        SpawnPracticeCombatDummy(PracticeCombatDummyMode.Combat, CharacterClassCatalog.GetDefinition(playerClass));
    }

    public void SpawnPracticeDpsDummy()
    {
        SpawnPracticeCombatDummy(PracticeCombatDummyMode.Dps, CharacterClassCatalog.Heavy);
    }

    private void SpawnPracticeCombatDummy(
        PracticeCombatDummyMode mode,
        CharacterClassDefinition classDefinition)
    {
        if (!Config.EnableLocalDummies || !Config.EnableEnemyTrainingDummy)
        {
            return;
        }

        EnemyPlayerEnabled = true;
        DummyState.CombatMode = mode;
        DummyState.CombatClassDefinition = classDefinition;
        ResetPracticeCombatDummyStats();
        DummyState.EnemyRespawnTicks = 0;
        ClearEnemyInputOverride();
        EnemyPlayer.ClearMedicHealingTarget();
        SpawnPracticeCombatDummyResolved(playRespawnSound: false);
    }

    public void DespawnPracticeCombatDummy()
    {
        DespawnEnemyDummy();
    }

    public void DespawnPracticeDpsDummy()
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
        FriendlyDummy.SetClassDefinition(LocalState.FriendlyDummyClassDefinition);
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

        DummyState.EnemyTeam = team;
        if (EnemyPlayerEnabled)
        {
            if (DummyState.CombatMode != PracticeCombatDummyMode.None)
            {
                SpawnPracticeCombatDummyResolved(playRespawnSound: false);
            }
            else
            {
                EnemyPlayer.SetClassDefinition(DummyState.EnemyClassDefinition);
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

        var input = DummyState.CombatMode != PracticeCombatDummyMode.None
            ? BuildPracticeCombatDummyInput()
            : ResolveEnemyDummyInput();
        var previousInput = DummyState.PreviousEnemyInput;
        if (EnemyPlayer.IsAlive)
        {
            AdvanceAlivePlayerWithInput(EnemyPlayer, input, previousInput, DummyState.EnemyTeam, allowDebugKill: false);
        }
        else
        {
            AdvanceEnemyDummyRespawnTimer();
            DummyState.EnemyInput = default;
            input = default;
        }

        DummyState.PreviousEnemyInput = input;
    }

    private PlayerInputSnapshot ResolveEnemyDummyInput()
    {
        if (!DummyState.EnemyInputOverrideActive)
        {
            DummyState.EnemyInput = BuildEnemyInput();
        }

        return DummyState.EnemyInput;
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
        EnemyPlayer.SetClassDefinition(DummyState.CombatClassDefinition ?? CharacterClassCatalog.Heavy);
        var spawn = FindEnemyDummySpawnNearLocalPlayer();
        if (SpawnPlayerResolved(EnemyPlayer, DummyState.EnemyTeam, spawn.X, spawn.Y, playRespawnSound: playRespawnSound))
        {
            EnemyPlayer.SetAimWorldPosition(LocalPlayer.X, LocalPlayer.Y - (LocalPlayer.Height / 4f));
            return true;
        }

        var fallbackSpawn = ReserveSpawn(EnemyPlayer, DummyState.EnemyTeam);
        var spawned = SpawnPlayerResolved(EnemyPlayer, DummyState.EnemyTeam, fallbackSpawn, playRespawnSound: playRespawnSound);
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
            if (CanPlaceDebugDummyAt(candidateX, candidateY, EnemyPlayer.Width, EnemyPlayer.Height, DummyState.EnemyTeam))
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
        if (DummyState.EnemyStrafeTicksRemaining > 0)
        {
            DummyState.EnemyStrafeTicksRemaining -= 1;
            return DummyState.EnemyStrafeDirection;
        }

        DummyState.EnemyStrafeTicksRemaining = 30 + Randoms.Gameplay.Next(30);
        DummyState.EnemyStrafeDirection = Randoms.Gameplay.Next(2) == 0 ? -1 : 1;
        return DummyState.EnemyStrafeDirection;
    }

    private void DisablePracticeCombatDummyMode(bool resetStats)
    {
        DummyState.CombatMode = PracticeCombatDummyMode.None;
        DummyState.CombatClassDefinition = null;
        if (resetStats)
        {
            ResetPracticeCombatDummyStats();
        }
    }

    private void ResetPracticeCombatDummyStats()
    {
        DummyState.CombatTotalDamage = 0;
        DummyState.CombatFirstDamageFrame = -1;
        DummyState.CombatLastDamageFrame = -1;
        DummyState.CombatContinuousDamageAccumulator = 0f;
    }

    private bool IsPracticeCombatDummyBurstExpired()
    {
        if (DummyState.CombatLastDamageFrame < 0)
        {
            return false;
        }

        var timeoutFrames = Math.Max(1L, (long)Math.Ceiling(PracticeCombatDummyBurstTimeoutSeconds / Config.FixedDeltaSeconds));
        return Frame - DummyState.CombatLastDamageFrame > timeoutFrames;
    }

    private void ResetPracticeCombatDummyBurstIfExpired()
    {
        if (IsPracticeCombatDummyBurstExpired())
        {
            ResetPracticeCombatDummyStats();
        }
    }

    private bool TryAbsorbPracticeCombatDummyDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags)
    {
        if (!IsPracticeDpsDummy(target))
        {
            return false;
        }

        ResetPracticeCombatDummyBurstIfExpired();
        RegisterPracticeCombatDummyDamage(target, damage, attacker, damageFlags);
        return true;
    }

    private bool TryAbsorbPracticeCombatDummyContinuousDamage(
        PlayerEntity target,
        float damage,
        PlayerEntity? attacker,
        DamageEventFlags damageFlags)
    {
        if (!IsPracticeDpsDummy(target))
        {
            return false;
        }

        ResetPracticeCombatDummyBurstIfExpired();
        DummyState.CombatContinuousDamageAccumulator += damage;
        var wholeDamage = (int)DummyState.CombatContinuousDamageAccumulator;
        if (wholeDamage > 0)
        {
            DummyState.CombatContinuousDamageAccumulator -= wholeDamage;
            RegisterPracticeCombatDummyDamage(target, wholeDamage, attacker, damageFlags);
        }

        return true;
    }

    private bool TryAbsorbPracticeCombatDummyTickDamage(
        PlayerEntity target,
        int damage,
        PlayerEntity? attacker)
    {
        if (!IsPracticeDpsDummy(target))
        {
            return false;
        }

        ResetPracticeCombatDummyBurstIfExpired();
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

        if (DummyState.CombatFirstDamageFrame < 0)
        {
            DummyState.CombatFirstDamageFrame = Frame;
        }

        DummyState.CombatLastDamageFrame = Frame;
        DummyState.CombatTotalDamage = (int)Math.Min(int.MaxValue, DummyState.CombatTotalDamage + (long)damage);
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
