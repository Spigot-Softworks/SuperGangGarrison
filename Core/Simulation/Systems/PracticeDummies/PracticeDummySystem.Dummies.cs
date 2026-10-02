namespace OpenGarrison.Core;

internal sealed partial class PracticeDummySystem
{
    private const double PracticeCombatDummyDpsMinimumElapsedSeconds = 1d;
    private const double PracticeCombatDummyBurstTimeoutSeconds = 4d;
    private const float PracticeCombatDummyFullIntensityDamage = 1200f;

    internal bool PracticeCombatDummyActive => _host.DummyState.CombatMode == PracticeCombatDummyMode.Combat && _host.EnemyPlayerEnabled;

    internal bool PracticeDpsDummyActive => _host.DummyState.CombatMode == PracticeCombatDummyMode.Dps && _host.EnemyPlayerEnabled;

    internal int PracticeCombatDummyTotalDamage => PracticeDpsDummyActive
        && PracticeCombatDummyDpsVisible
        ? _host.DummyState.CombatTotalDamage
        : 0;

    internal bool PracticeCombatDummyDpsVisible => PracticeDpsDummyActive
        && _host.DummyState.CombatTotalDamage > 0
        && _host.DummyState.CombatFirstDamageFrame >= 0
        && _host.DummyState.CombatLastDamageFrame >= 0
        && !IsPracticeCombatDummyBurstExpired();

    internal double PracticeCombatDummyDps
    {
        get
        {
            if (!PracticeCombatDummyDpsVisible)
            {
                return 0d;
            }

            var elapsedFrames = Math.Max(1, _host.Frame - _host.DummyState.CombatFirstDamageFrame + 1);
            var elapsedSeconds = Math.Max(PracticeCombatDummyDpsMinimumElapsedSeconds, elapsedFrames * _host.Config.FixedDeltaSeconds);
            return _host.DummyState.CombatTotalDamage / elapsedSeconds;
        }
    }

    internal float PracticeCombatDummyDamageIntensity => PracticeCombatDummyDpsVisible
        ? Math.Clamp(_host.DummyState.CombatTotalDamage / PracticeCombatDummyFullIntensityDamage, 0f, 1f)
        : 0f;

    internal bool IsPracticeCombatDummy(PlayerEntity player)
    {
        return PracticeCombatDummyActive && ReferenceEquals(player, _host.EnemyPlayer);
    }

    internal bool IsPracticeDpsDummy(PlayerEntity player)
    {
        return PracticeDpsDummyActive && ReferenceEquals(player, _host.EnemyPlayer);
    }

    internal bool IsPracticeDummy(PlayerEntity player)
    {
        return _host.DummyState.CombatMode != PracticeCombatDummyMode.None
            && _host.EnemyPlayerEnabled
            && ReferenceEquals(player, _host.EnemyPlayer);
    }

    internal void SpawnEnemyDummy()
    {
        DisablePracticeCombatDummyMode(resetStats: true);
        if (!_host.Config.EnableLocalDummies || !_host.Config.EnableEnemyTrainingDummy)
        {
            return;
        }

        _host.EnemyPlayerEnabled = true;
        _host.DummyState.EnemyRespawnTicks = 0;
        _host.EnemyPlayer.SetClassDefinition(_host.DummyState.EnemyClassDefinition);
        _host.SpawnPlayerResolved(_host.EnemyPlayer, _host.DummyState.EnemyTeam, _host.ReserveSpawn(_host.EnemyPlayer, _host.DummyState.EnemyTeam));
    }

    internal void DespawnEnemyDummy()
    {
        DisablePracticeCombatDummyMode(resetStats: true);
        if (!_host.Config.EnableLocalDummies || !_host.Config.EnableEnemyTrainingDummy)
        {
            _host.EnemyPlayerEnabled = false;
            return;
        }

        _host.EnemyPlayerEnabled = false;
        _host.DummyState.EnemyRespawnTicks = 0;
        _host.ClearEnemyInputOverride();
        _host.EnemyPlayer.ClearMedicHealingTarget();
        _host.EnemyPlayer.Kill();
    }

    internal void SpawnPracticeCombatDummy()
    {
        SpawnPracticeCombatDummy(PracticeCombatDummyMode.Combat, CharacterClassCatalog.Heavy);
    }

    internal void SpawnPracticeCombatDummy(PlayerClass playerClass)
    {
        SpawnPracticeCombatDummy(PracticeCombatDummyMode.Combat, CharacterClassCatalog.GetDefinition(playerClass));
    }

    internal void SpawnPracticeDpsDummy()
    {
        SpawnPracticeCombatDummy(PracticeCombatDummyMode.Dps, CharacterClassCatalog.Heavy);
    }

    internal void SpawnPracticeCombatDummy(
        PracticeCombatDummyMode mode,
        CharacterClassDefinition classDefinition)
    {
        if (!_host.Config.EnableLocalDummies || !_host.Config.EnableEnemyTrainingDummy)
        {
            return;
        }

        _host.EnemyPlayerEnabled = true;
        _host.DummyState.CombatMode = mode;
        _host.DummyState.CombatClassDefinition = classDefinition;
        ResetPracticeCombatDummyStats();
        _host.DummyState.EnemyRespawnTicks = 0;
        _host.ClearEnemyInputOverride();
        _host.EnemyPlayer.ClearMedicHealingTarget();
        SpawnPracticeCombatDummyResolved(playRespawnSound: false);
    }

    internal void DespawnPracticeCombatDummy()
    {
        DespawnEnemyDummy();
    }

    internal void DespawnPracticeDpsDummy()
    {
        DespawnEnemyDummy();
    }

    internal void SpawnFriendlyDummy()
    {
        if (!_host.Config.EnableLocalDummies || !_host.Config.EnableFriendlySupportDummy)
        {
            return;
        }

        _host.FriendlyDummyEnabled = true;
        _host.FriendlyDummy.SetClassDefinition(_host.LocalState.FriendlyDummyClassDefinition);
        var spawn = FindFriendlyDummySpawnNearLocalPlayer();
        _host.SpawnPlayerResolved(_host.FriendlyDummy, _host.LocalPlayerTeam, spawn.X, spawn.Y, clearMedicHealingTarget: false);
    }

    internal void DespawnFriendlyDummy()
    {
        _host.FriendlyDummyEnabled = false;
        _host.FriendlyDummy.ClearMedicHealingTarget();
        _host.FriendlyDummy.Kill();
    }

    internal void SetFriendlyDummyHealth(int health)
    {
        if (!_host.Config.EnableLocalDummies || !_host.Config.EnableFriendlySupportDummy)
        {
            return;
        }

        if (!_host.FriendlyDummyEnabled)
        {
            SpawnFriendlyDummy();
        }

        _host.FriendlyDummy.ForceSetHealth(health);
    }

    internal void SetEnemyPlayerName(string displayName)
    {
        _host.EnemyPlayer.SetDisplayName(displayName);
    }

    internal void SetFriendlyDummyName(string displayName)
    {
        _host.FriendlyDummy.SetDisplayName(displayName);
    }

    internal void SetEnemyPlayerTeam(PlayerTeam team)
    {
        if (!_host.Config.EnableLocalDummies)
        {
            return;
        }

        _host.DummyState.EnemyTeam = team;
        if (_host.EnemyPlayerEnabled)
        {
            if (_host.DummyState.CombatMode != PracticeCombatDummyMode.None)
            {
                SpawnPracticeCombatDummyResolved(playRespawnSound: false);
            }
            else
            {
                _host.EnemyPlayer.SetClassDefinition(_host.DummyState.EnemyClassDefinition);
                _host.SpawnPlayerResolved(_host.EnemyPlayer, team, _host.ReserveSpawn(_host.EnemyPlayer, team));
            }
        }
    }

    internal void AdvanceEnemyDummy()
    {
        if (!_host.EnemyPlayerEnabled)
        {
            return;
        }

        var input = _host.DummyState.CombatMode != PracticeCombatDummyMode.None
            ? BuildPracticeCombatDummyInput()
            : ResolveEnemyDummyInput();
        var previousInput = _host.DummyState.PreviousEnemyInput;
        if (_host.EnemyPlayer.IsAlive)
        {
            _host.AdvanceAlivePlayerWithInput(_host.EnemyPlayer, input, previousInput, _host.DummyState.EnemyTeam, allowDebugKill: false);
        }
        else
        {
            _host.AdvanceEnemyDummyRespawnTimer();
            _host.DummyState.EnemyInput = default;
            input = default;
        }

        _host.DummyState.PreviousEnemyInput = input;
    }

    private PlayerInputSnapshot ResolveEnemyDummyInput()
    {
        if (!_host.DummyState.EnemyInputOverrideActive)
        {
            _host.DummyState.EnemyInput = BuildEnemyInput();
        }

        return _host.DummyState.EnemyInput;
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
            AimWorldX: _host.LocalPlayer.X,
            AimWorldY: _host.LocalPlayer.Y - (_host.LocalPlayer.Height / 4f),
            DebugKill: false);
    }

    internal bool SpawnPracticeCombatDummyResolved(bool playRespawnSound)
    {
        _host.EnemyPlayer.SetClassDefinition(_host.DummyState.CombatClassDefinition ?? CharacterClassCatalog.Heavy);
        var spawn = FindEnemyDummySpawnNearLocalPlayer();
        if (_host.SpawnPlayerResolved(_host.EnemyPlayer, _host.DummyState.EnemyTeam, spawn.X, spawn.Y, playRespawnSound: playRespawnSound))
        {
            _host.EnemyPlayer.SetAimWorldPosition(_host.LocalPlayer.X, _host.LocalPlayer.Y - (_host.LocalPlayer.Height / 4f));
            return true;
        }

        var fallbackSpawn = _host.ReserveSpawn(_host.EnemyPlayer, _host.DummyState.EnemyTeam);
        var spawned = _host.SpawnPlayerResolved(_host.EnemyPlayer, _host.DummyState.EnemyTeam, fallbackSpawn, playRespawnSound: playRespawnSound);
        _host.EnemyPlayer.SetAimWorldPosition(_host.LocalPlayer.X, _host.LocalPlayer.Y - (_host.LocalPlayer.Height / 4f));
        return spawned;
    }

    internal (float X, float Y) FindFriendlyDummySpawnNearLocalPlayer()
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
            var candidateX = _host.Level.Bounds.ClampX(_host.LocalPlayer.X + offset, _host.FriendlyDummy.Width);
            var candidateY = _host.Level.Bounds.ClampY(_host.LocalPlayer.Y, _host.FriendlyDummy.Height);
            if (CanPlaceDebugDummyAt(candidateX, candidateY, _host.FriendlyDummy.Width, _host.FriendlyDummy.Height, _host.LocalPlayerTeam))
            {
                return (candidateX, candidateY);
            }
        }

        return (
            _host.Level.Bounds.ClampX(_host.LocalPlayer.X + 96f, _host.FriendlyDummy.Width),
            _host.Level.Bounds.ClampY(_host.LocalPlayer.Y, _host.FriendlyDummy.Height));
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
            var candidateX = _host.Level.Bounds.ClampX(_host.LocalPlayer.X + offset, _host.EnemyPlayer.Width);
            var candidateY = _host.Level.Bounds.ClampY(_host.LocalPlayer.Y, _host.EnemyPlayer.Height);
            if (CanPlaceDebugDummyAt(candidateX, candidateY, _host.EnemyPlayer.Width, _host.EnemyPlayer.Height, _host.DummyState.EnemyTeam))
            {
                return (candidateX, candidateY);
            }
        }

        return (
            _host.Level.Bounds.ClampX(_host.LocalPlayer.X + 112f, _host.EnemyPlayer.Width),
            _host.Level.Bounds.ClampY(_host.LocalPlayer.Y, _host.EnemyPlayer.Height));
    }

    private bool CanPlaceDebugDummyAt(float x, float y, float width, float height, PlayerTeam team)
    {
        var left = x - width / 2f;
        var right = x + width / 2f;
        var top = y - height / 2f;
        var bottom = y + height / 2f;

        foreach (var solid in _host.Level.Solids)
        {
            if (left < solid.Right
                && right > solid.Left
                && top < solid.Bottom
                && bottom > solid.Top)
            {
                return false;
            }
        }

        foreach (var gate in _host.Level.GetBlockingTeamGates(team, false))
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

        foreach (var wall in _host.Level.GetRoomObjects(RoomObjectType.PlayerWall))
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

        foreach (var roomObject in _host.Level.RoomObjects)
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
        var horizontalDelta = _host.LocalPlayer.X - _host.EnemyPlayer.X;
        var verticalDelta = _host.LocalPlayer.Y - _host.EnemyPlayer.Y;
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
                AimWorldX: _host.EnemyPlayer.X,
                AimWorldY: _host.EnemyPlayer.Y,
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

        var jump = _host.EnemyPlayer.IsGrounded
            && ((verticalDelta < -24f && absoluteHorizontal < 280f)
                || _host.WouldRunIntoWall(_host.EnemyPlayer, moveDirection));
        var fire = _host.LocalPlayer.IsAlive
            && absoluteHorizontal < 360f
            && MathF.Abs(verticalDelta) < 140f
            && _host.HasLineOfSight(_host.EnemyPlayer, _host.LocalPlayer);

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
            AimWorldX: _host.LocalPlayer.X,
            AimWorldY: _host.LocalPlayer.Y - (_host.LocalPlayer.Height / 4f),
            DebugKill: false);
    }

    private int GetEnemyStrafeDirection()
    {
        if (_host.DummyState.EnemyStrafeTicksRemaining > 0)
        {
            _host.DummyState.EnemyStrafeTicksRemaining -= 1;
            return _host.DummyState.EnemyStrafeDirection;
        }

        _host.DummyState.EnemyStrafeTicksRemaining = 30 + _host.Randoms.Gameplay.Next(30);
        _host.DummyState.EnemyStrafeDirection = _host.Randoms.Gameplay.Next(2) == 0 ? -1 : 1;
        return _host.DummyState.EnemyStrafeDirection;
    }

    private void DisablePracticeCombatDummyMode(bool resetStats)
    {
        _host.DummyState.CombatMode = PracticeCombatDummyMode.None;
        _host.DummyState.CombatClassDefinition = null;
        if (resetStats)
        {
            ResetPracticeCombatDummyStats();
        }
    }

    private void ResetPracticeCombatDummyStats()
    {
        _host.DummyState.CombatTotalDamage = 0;
        _host.DummyState.CombatFirstDamageFrame = -1;
        _host.DummyState.CombatLastDamageFrame = -1;
        _host.DummyState.CombatContinuousDamageAccumulator = 0f;
    }

    private bool IsPracticeCombatDummyBurstExpired()
    {
        if (_host.DummyState.CombatLastDamageFrame < 0)
        {
            return false;
        }

        var timeoutFrames = Math.Max(1L, (long)Math.Ceiling(PracticeCombatDummyBurstTimeoutSeconds / _host.Config.FixedDeltaSeconds));
        return _host.Frame - _host.DummyState.CombatLastDamageFrame > timeoutFrames;
    }

    private void ResetPracticeCombatDummyBurstIfExpired()
    {
        if (IsPracticeCombatDummyBurstExpired())
        {
            ResetPracticeCombatDummyStats();
        }
    }

    internal bool TryAbsorbPracticeCombatDummyDamage(
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

    internal bool TryAbsorbPracticeCombatDummyContinuousDamage(
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
        _host.DummyState.CombatContinuousDamageAccumulator += damage;
        var wholeDamage = (int)_host.DummyState.CombatContinuousDamageAccumulator;
        if (wholeDamage > 0)
        {
            _host.DummyState.CombatContinuousDamageAccumulator -= wholeDamage;
            RegisterPracticeCombatDummyDamage(target, wholeDamage, attacker, damageFlags);
        }

        return true;
    }

    internal bool TryAbsorbPracticeCombatDummyTickDamage(
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

        if (_host.DummyState.CombatFirstDamageFrame < 0)
        {
            _host.DummyState.CombatFirstDamageFrame = _host.Frame;
        }

        _host.DummyState.CombatLastDamageFrame = _host.Frame;
        _host.DummyState.CombatTotalDamage = (int)Math.Min(int.MaxValue, _host.DummyState.CombatTotalDamage + (long)damage);
        target.ForceSetHealth(target.MaxHealth);
        _host.RegisterDamageEvent(
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
