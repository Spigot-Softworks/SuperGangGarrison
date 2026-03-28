using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class SimulationWorldTests
{
    [Fact]
    public void ForceKillLocalPlayer_RespawnsAfterDefaultDelay()
    {
        var world = CreateWorld();

        world.ForceKillLocalPlayer();

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(150, world.LocalPlayerRespawnTicks);

        AdvanceTicks(world, 149);

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(1, world.LocalPlayerRespawnTicks);

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(0, world.LocalPlayerRespawnTicks);
    }

    [Fact]
    public void TrySetLocalClass_KillsPlayerAndRespawnsAsRequestedClass()
    {
        var world = CreateWorld();

        var changed = world.TrySetLocalClass(PlayerClass.Engineer);

        Assert.True(changed);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(150, world.LocalPlayerRespawnTicks);
        Assert.Null(world.LocalDeathCam);
        var killFeedEntry = Assert.Single(world.KillFeed);
        Assert.Equal(string.Empty, killFeedEntry.KillerName);
        Assert.Equal(string.Empty, killFeedEntry.VictimName);
        Assert.Equal("DeadKL", killFeedEntry.WeaponSpriteName);
        Assert.Equal("Player 1 bid farewell, cruel world!", killFeedEntry.MessageText);

        AdvanceTicks(world, 150);

        Assert.Equal(PlayerClass.Engineer, world.LocalPlayer.ClassId);
        Assert.Equal("Engineer", world.LocalPlayer.ClassName);
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(world.LocalPlayer.MaxShells, world.LocalPlayer.CurrentShells);
    }

    [Fact]
    public void TrySetLocalClass_InSpawnRoomDoesNotLeaveCorpse()
    {
        var world = CreateWorld();

        world.LocalPlayer.SetSpawnRoomState(true);
        Assert.True(world.TrySetLocalClass(PlayerClass.Engineer));

        Assert.Empty(world.DeadBodies);
        Assert.Empty(world.PlayerGibs);
        Assert.Equal(1, world.LocalPlayerRespawnTicks);

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(PlayerClass.Engineer, world.LocalPlayer.ClassId);
    }

    [Fact]
    public void EngineerCanOnlyBuildOneSentryAndSpendsMetal()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);

        var builtFirst = world.TryBuildLocalSentry();
        var builtSecond = world.TryBuildLocalSentry();

        Assert.True(builtFirst);
        Assert.False(builtSecond);
        Assert.Single(world.Sentries);
        Assert.Equal(0f, world.LocalPlayer.Metal);
    }

    [Fact]
    public void DroppedIntel_ReturnsToBaseAfterReturnTimer()
    {
        var world = CreateWorld();

        var pickedUp = world.ForceGiveEnemyIntelToLocalPlayer();
        AdvanceTicks(world, 30);
        world.ForceDropLocalIntel();

        Assert.True(pickedUp);
        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
        Assert.Equal(300, world.BlueIntel.ReturnTicksRemaining);

        AdvanceTicks(world, 301);

        if (!world.BlueIntel.IsAtBase)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.BlueIntel.ReturnTicksRemaining <= 0);
        Assert.True(world.BlueIntel.X >= 0f);
        Assert.True(world.BlueIntel.Y >= 0f);
    }

    [Fact]
    public void KillingLocalCarrier_DropsIntelWithoutCreatingSelfDeathCam()
    {
        var world = CreateWorld();

        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        world.ForceKillLocalPlayer();

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.Null(world.LocalDeathCam);
        Assert.Single(world.KillFeed);
        Assert.Equal(150, world.LocalPlayerRespawnTicks);
    }

    [Fact]
    public void SelfKill_KillFeedExpiresAfterItsTickLifetime()
    {
        var world = CreateWorld();

        world.ForceKillLocalPlayer();

        Assert.Null(world.LocalDeathCam);
        var killFeedEntry = Assert.Single(world.KillFeed);
        Assert.Equal(string.Empty, killFeedEntry.KillerName);
        Assert.Equal(world.LocalPlayer.DisplayName, killFeedEntry.VictimName);
        Assert.Equal("DeadKL", killFeedEntry.WeaponSpriteName);

        AdvanceTicks(world, 150);

        Assert.Null(world.LocalDeathCam);
        Assert.Empty(world.KillFeed);
    }

    [Fact]
    public void DuplicateNormalKillFeedEntriesInSameFrame_AreSuppressed()
    {
        var world = CreateWorld();
        const byte enemySlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));

        world.CombatTestRecordKillFeedEntry(world.LocalPlayer, enemy, "RocketKL");
        world.CombatTestRecordKillFeedEntry(world.LocalPlayer, enemy, "RocketKL");

        var entry = Assert.Single(world.KillFeed);
        Assert.Equal(enemy.DisplayName, entry.KillerName);
        Assert.Equal(world.LocalPlayer.DisplayName, entry.VictimName);
        Assert.Equal("RocketKL", entry.WeaponSpriteName);
    }

    [Fact]
    public void EnemyKill_CreatesDeathCamFocusedOnKiller()
    {
        var world = CreateWorld();
        const byte enemySlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));

        enemy.TeleportTo(world.LocalPlayer.X + 40f, world.LocalPlayer.Y);
        world.LocalPlayer.ForceSetHealth(1);

        world.CombatTestExplodeRocket(enemy, world.LocalPlayer.X, world.LocalPlayer.Y);

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.NotNull(world.LocalDeathCam);
        var deathCam = world.LocalDeathCam!;
        Assert.Equal(enemy.X, deathCam.FocusX);
        Assert.Equal(enemy.Y, deathCam.FocusY);
        Assert.Equal("You were killed by", deathCam.KillMessage);
        Assert.Equal(enemy.DisplayName, deathCam.KillerName);
    }

    [Fact]
    public void CarryingEnemyIntelToOwnBase_ScoresCaptureAndResetsIntel()
    {
        var world = CreateWorld();
        var ownBase = world.Level.GetIntelBase(world.LocalPlayerTeam);

        Assert.True(ownBase.HasValue);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        world.TeleportLocalPlayer(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();

        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(1, world.LocalPlayer.Caps);
        Assert.True(world.BlueIntel.IsAtBase);
        Assert.False(world.BlueIntel.IsDropped);
    }

    [Fact]
    public void CarryingEnemyIntelToOwnBase_RecordsObjectiveLogMessage()
    {
        var world = CreateWorld();
        var ownBase = world.Level.GetIntelBase(world.LocalPlayerTeam);

        Assert.True(ownBase.HasValue);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        world.TeleportLocalPlayer(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();

        var entry = Assert.Single(world.KillFeed);
        Assert.Equal(world.LocalPlayer.DisplayName, entry.KillerName);
        Assert.Equal("RedCaptureS", entry.WeaponSpriteName);
        Assert.Equal("captured the intelligence!", entry.MessageText);
        Assert.Equal(string.Empty, entry.VictimName);
    }

    [Fact]
    public void SetCapLimit_UpdatesRuleWithoutResettingRoundState()
    {
        var world = CreateWorld();
        var ownBase = world.Level.GetIntelBase(world.LocalPlayerTeam);

        Assert.True(ownBase.HasValue);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());
        world.TeleportLocalPlayer(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();
        Assert.Equal(1, world.RedCaps);

        world.SetCapLimit(8);

        Assert.Equal(8, world.MatchRules.CapLimit);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Assert.Equal(MatchPhase.Running, world.MatchState.Phase);
    }

    [Fact]
    public void LoadingArenaMap_EnablesArenaModeAndStartsLockedPointTimer()
    {
        var world = CreateWorld();

        Assert.True(world.TryLoadLevel("arena_montane"));
        Assert.Equal(GameModeKind.Arena, world.MatchRules.Mode);
        Assert.True(world.ArenaPointLocked);
        Assert.Equal(1800, world.ArenaUnlockTicksRemaining);

        world.AdvanceOneTick();

        Assert.True(world.ArenaPointLocked);
        Assert.Equal(1799, world.ArenaUnlockTicksRemaining);
    }

    [Fact]
    public void LoadingGeneratorMap_InitializesGenerators()
    {
        var world = CreateWorld();

        Assert.True(world.TryLoadLevel("destroy"));
        Assert.Equal(GameModeKind.Generator, world.MatchRules.Mode);
        Assert.Equal(2, world.Generators.Count);
        Assert.Equal(4000, world.GetGenerator(PlayerTeam.Red)!.Health);
        Assert.Equal(4000, world.GetGenerator(PlayerTeam.Blue)!.Health);
    }

    [Fact]
    public void DestroyingGenerator_AwardsCapAndEndsRound()
    {
        var world = CreateWorld();
        var redGenerator = new RoomObjectMarker(RoomObjectType.Generator, 80f, 180f, 40f, 60f, "GeneratorS", PlayerTeam.Red);
        var blueGenerator = new RoomObjectMarker(RoomObjectType.Generator, 180f, 180f, 40f, 60f, "GeneratorS", PlayerTeam.Blue);
        world.CombatTestSetLevel(CreateLevel(mode: GameModeKind.Generator, roomObjects: [redGenerator, blueGenerator]));

        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);

        var destroyed = world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f);

        Assert.True(destroyed);
        Assert.True(world.GetGenerator(PlayerTeam.Blue)!.IsDestroyed);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Assert.True(world.IsMapChangePending);
    }

    [Fact]
    public void DestroyingGenerator_RecordsObjectiveLogMessage()
    {
        var world = CreateWorld();
        var redGenerator = new RoomObjectMarker(RoomObjectType.Generator, 80f, 180f, 40f, 60f, "GeneratorS", PlayerTeam.Red);
        var blueGenerator = new RoomObjectMarker(RoomObjectType.Generator, 180f, 180f, 40f, 60f, "GeneratorS", PlayerTeam.Blue);
        world.CombatTestSetLevel(CreateLevel(mode: GameModeKind.Generator, roomObjects: [redGenerator, blueGenerator]));

        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);
        Assert.True(world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f));

        var entry = Assert.Single(world.KillFeed);
        Assert.Equal("Red team", entry.KillerName);
        Assert.Equal(" has destroyed the enemy generator!", entry.MessageText);
        Assert.Equal(string.Empty, entry.WeaponSpriteName);
    }

    [Fact]
    public void DestroyingGenerator_ExplodesNearbyPlayersProjectilesAndStructures()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        var redGenerator = new RoomObjectMarker(RoomObjectType.Generator, 80f, 180f, 40f, 60f, "GeneratorS", PlayerTeam.Red);
        var blueGenerator = new RoomObjectMarker(RoomObjectType.Generator, 180f, 180f, 40f, 60f, "GeneratorS", PlayerTeam.Blue);
        world.CombatTestSetLevel(CreateLevel(mode: GameModeKind.Generator, roomObjects: [redGenerator, blueGenerator], solids: [floor]));
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);
        world.TeleportLocalPlayer(blueGenerator.CenterX + 12f, 220f);
        world.CombatTestAddSentry(new SentryEntity(900, world.LocalPlayer.Id, PlayerTeam.Red, blueGenerator.CenterX + 20f, 220f, 1f));
        world.CombatTestSpawnRocket(world.LocalPlayer, blueGenerator.CenterX + 5f, blueGenerator.CenterY);
        world.CombatTestSpawnMine(world.LocalPlayer, blueGenerator.CenterX + 15f, blueGenerator.CenterY);
        world.CombatTestSpawnBubble(world.LocalPlayer, blueGenerator.CenterX + 25f, blueGenerator.CenterY);
        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);

        var destroyed = world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f);

        Assert.True(destroyed);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Empty(world.Rockets);
        Assert.Empty(world.Mines);
        Assert.Empty(world.Bubbles);
        Assert.Empty(world.Sentries);
        Assert.NotEmpty(world.PlayerGibs);
        Assert.True(ConsumePendingExplosion(world));
    }

    [Fact]
    public void DestroyingLocalSentry_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);

        Assert.True(world.TryBuildLocalSentry());

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        Assert.True(world.TryDestroyLocalSentry());

        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Explosion");
    }

    [Fact]
    public void FragBoxKill_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        var fragBox = new RoomObjectMarker(RoomObjectType.FragBox, 80f, 180f, 60f, 60f, string.Empty);
        world.CombatTestSetLevel(CreateLevel(roomObjects: [fragBox]));
        world.TeleportLocalPlayer(fragBox.CenterX, fragBox.CenterY);

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        world.AdvanceOneTick();

        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Explosion");
    }

    [Fact]
    public void HeldDemomanSecondary_DetonatesMineOnSubsequentHeldTick()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);

        var heldSecondaryInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false);
        world.SetLocalInput(heldSecondaryInput);
        world.AdvanceOneTick();

        world.CombatTestSpawnMine(world.LocalPlayer, world.LocalPlayer.X + 10f, world.LocalPlayer.Y, stickied: true);
        Assert.Single(world.Mines);

        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Empty(world.Mines);
    }

    [Fact]
    public void DestroyingLocalSentry_BlastsAirborneOwner()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);

        Assert.True(world.TryBuildLocalSentry());
        var sentry = Assert.Single(world.Sentries);
        world.TeleportLocalPlayer(sentry.X, sentry.Y - 30f);

        Assert.True(world.TryDestroyLocalSentry());

        Assert.True(world.LocalPlayer.VerticalSpeed < 0f);
    }

    [Fact]
    public void RocketExplosion_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(20f, world.LocalPlayer.Y);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: -100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        var exploded = AdvanceUntilExplosion(world);

        Assert.True(exploded);
    }

    [Fact]
    public void RocketExplosion_GibsInheritVictimVelocity()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        target.TeleportTo(320f, 200f);
        target.AddImpulse(240f, 0f);
        target.ForceSetHealth(1);

        world.CombatTestExplodeRocket(world.LocalPlayer, target.X, target.Y);

        Assert.NotEmpty(world.PlayerGibs);
        Assert.True(world.PlayerGibs.Average(static gib => gib.VelocityX) > 3f);
    }

    [Fact]
    public void RocketLauncher_FiresFromPreMovePositionBeforeMovement()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(200f, 220f);
        var playerXBeforeTick = world.LocalPlayer.X;
        var playerYBeforeTick = world.LocalPlayer.Y;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: playerXBeforeTick + 200f,
            AimWorldY: playerYBeforeTick,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var rocket = Assert.Single(world.Rockets);
        Assert.Equal(playerXBeforeTick + 20f, rocket.X, 3);
        Assert.Equal(playerYBeforeTick, rocket.Y, 3);
        Assert.True(world.LocalPlayer.X > playerXBeforeTick);
    }

    [Fact]
    public void QuoteBubble_FiresFromSourceOffsetWithFixedSpeedAndOwnerInheritance()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        SetLocalClassAndRespawn(world, PlayerClass.Quote);
        world.TeleportLocalPlayer(200f, 220f);
        world.LocalPlayer.AddImpulse(60f, -30f);
        var playerXBeforeTick = world.LocalPlayer.X;
        var playerYBeforeTick = world.LocalPlayer.Y;
        var horizontalSpeedBeforeTick = world.LocalPlayer.HorizontalSpeed;
        var verticalSpeedBeforeTick = world.LocalPlayer.VerticalSpeed;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: playerXBeforeTick + 200f,
            AimWorldY: playerYBeforeTick,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var bubble = Assert.Single(world.Bubbles);
        Assert.Equal(playerXBeforeTick + 8f, bubble.X, 3);
        Assert.Equal(playerYBeforeTick, bubble.Y, 3);
        Assert.Equal(10f + (horizontalSpeedBeforeTick / LegacyMovementModel.SourceTicksPerSecond), bubble.VelocityX, 3);
        Assert.Equal(verticalSpeedBeforeTick / LegacyMovementModel.SourceTicksPerSecond, bubble.VelocityY, 3);
    }

    [Fact]
    public void Bubble_ExpiresWhenTooFarFromOwner()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Quote);

        world.DrainPendingVisualEvents();
        world.CombatTestSpawnBubble(
            world.LocalPlayer,
            world.LocalPlayer.X + BubbleProjectileEntity.MaxDistanceFromOwner + 1f,
            world.LocalPlayer.Y);

        world.AdvanceOneTick();

        Assert.Empty(world.Bubbles);
        Assert.Contains(world.DrainPendingVisualEvents(), visualEvent => visualEvent.EffectName == "Pop");
    }

    [Fact]
    public void Bubble_HittingEnemyPlayerDestroysBubbleAndAccumulatesSourceDamage()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Quote);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        world.LocalPlayer.TeleportTo(200f, 200f);
        target.TeleportTo(220f, 200f);
        var healthBeforeHits = target.Health;

        world.CombatTestSpawnBubble(world.LocalPlayer, target.X, target.Y);
        world.AdvanceOneTick();
        Assert.Empty(world.Bubbles);

        world.CombatTestSpawnBubble(world.LocalPlayer, target.X, target.Y);
        world.AdvanceOneTick();

        Assert.Empty(world.Bubbles);
        Assert.Equal(healthBeforeHits - 1, target.Health);
    }

    [Fact]
    public void Bubble_HittingEnemyRocketDestroysOnlyBubble()
    {
        var world = CreateWorld();
        const byte enemySlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Quote);

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemy));

        enemy.TeleportTo(260f, 200f);
        var rocket = world.CombatTestSpawnRocket(enemy, 220f, 200f);
        world.CombatTestSpawnBubble(world.LocalPlayer, 220f, 200f);

        world.AdvanceOneTick();

        Assert.Empty(world.Bubbles);
        var remainingRocket = Assert.Single(world.Rockets);
        Assert.Equal(rocket.Id, remainingRocket.Id);
    }

    [Fact]
    public void HeldQuoteSecondary_FiresBladeWhenReleaseGateClears()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Quote);
        world.LocalPlayer.IncrementQuoteBladeCount();

        var heldSecondaryInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 120f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false);
        world.SetLocalInput(heldSecondaryInput);
        world.AdvanceOneTick();

        Assert.Empty(world.Blades);

        world.LocalPlayer.DecrementQuoteBladeCount();
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Single(world.Blades);
    }

    [Fact]
    public void PyroFlame_FiresFromSourceOffsetWithSourceSpreadRangeAndOwnerInheritance()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.TeleportLocalPlayer(200f, 220f);
        world.LocalPlayer.AddImpulse(60f, -30f);
        var playerXBeforeTick = world.LocalPlayer.X;
        var playerYBeforeTick = world.LocalPlayer.Y;
        var horizontalSpeedBeforeTick = world.LocalPlayer.HorizontalSpeed;
        var verticalSpeedBeforeTick = world.LocalPlayer.VerticalSpeed;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: playerXBeforeTick + 200f,
            AimWorldY: playerYBeforeTick,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var flame = Assert.Single(world.Flames);
        Assert.Equal(playerXBeforeTick + 25f, flame.X, 3);
        Assert.Equal(playerYBeforeTick, flame.Y, 3);
        var inheritedVelocityX = horizontalSpeedBeforeTick / LegacyMovementModel.SourceTicksPerSecond;
        var inheritedVelocityY = verticalSpeedBeforeTick / LegacyMovementModel.SourceTicksPerSecond;
        var sourceVelocityX = flame.VelocityX - inheritedVelocityX;
        var sourceVelocityY = flame.VelocityY - inheritedVelocityY;
        var sourceSpeed = MathF.Sqrt((sourceVelocityX * sourceVelocityX) + (sourceVelocityY * sourceVelocityY));
        var sourceAngleDegrees = MathF.Atan2(sourceVelocityY, sourceVelocityX) * (180f / MathF.PI);
        var maxSpreadDegrees = MathF.Abs(1f - (horizontalSpeedBeforeTick / world.LocalPlayer.MaxRunSpeed)) * MathF.Pow(3f, 1.8f);

        Assert.InRange(sourceSpeed, 6.5f, 10f);
        Assert.InRange(sourceAngleDegrees, -maxSpreadDegrees, maxSpreadDegrees);
    }

    [Fact]
    public void PyroFlame_SpawnTraceUsesRoundedWeaponOrigin()
    {
        var world = CreateWorld();
        var wall = new LevelSolid(200.7f, 190f, 0.15f, 20f);
        world.CombatTestSetLevel(CreateLevel(solids: [wall]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.LocalPlayer.TeleportTo(200.6f, 200f);

        Assert.False(world.CombatTestIsFlameSpawnBlocked(201f, 200f, 226f, 200f, PlayerTeam.Red));
    }

    [Fact]
    public void Flame_HitAppliesSourceAfterburnWithoutAttachedFlames()
    {
        var world = CreateWorld();
        const byte enemySlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var target));

        world.LocalPlayer.TeleportTo(200f, 200f);
        target.TeleportTo(226f, 200f);
        var flame = world.CombatTestSpawnFlame(world.LocalPlayer, 210f, 200f, velocityX: 8f, velocityY: 0f);

        AdvanceTicks(world, 3);

        Assert.DoesNotContain(world.Flames, activeFlame => activeFlame.Id == flame.Id);
        Assert.True(target.BurnIntensity > 0f);
        Assert.True(target.BurnDurationSourceTicks > 0f);
    }

    [Fact]
    public void Afterburn_UsesSourceDelayBeforeIntensityDecay()
    {
        var world = CreateWorld();

        world.LocalPlayer.IgniteAfterburn(ownerPlayerId: 99, durationIncreaseSourceTicks: 120f, intensityIncrease: 6f, afterburnFalloff: false, burnFalloffAmount: 0f);
        var initialIntensity = world.LocalPlayer.BurnIntensity;
        var delayTicks = (int)MathF.Ceiling(PlayerEntity.BurnDecayDelaySourceTicks * world.Config.TicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);

        AdvanceTicks(world, delayTicks);

        Assert.Equal(initialIntensity, world.LocalPlayer.BurnIntensity, 3);

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.BurnIntensity < initialIntensity);
    }

    [Fact]
    public void Afterburn_UsesSourceVisualCountScaling()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);

        world.LocalPlayer.IgniteAfterburn(ownerPlayerId: 99, durationIncreaseSourceTicks: 210f, intensityIncrease: 6f, afterburnFalloff: false, burnFalloffAmount: 0f);
        Assert.Equal(3, world.LocalPlayer.BurnVisualCount);

        world.LocalPlayer.ReduceBurnDuration(209f);
        Assert.Equal(1, world.LocalPlayer.BurnVisualCount);

        world.LocalPlayer.IgniteAfterburn(ownerPlayerId: 99, durationIncreaseSourceTicks: 209f, intensityIncrease: 6f, afterburnFalloff: false, burnFalloffAmount: 0f);
        Assert.Equal(3, world.LocalPlayer.BurnVisualCount);

        world.LocalPlayer.ReduceBurnDuration(70f);
        Assert.Equal(2, world.LocalPlayer.BurnVisualCount);

        world.LocalPlayer.ReduceBurnDuration(70f);
        Assert.Equal(1, world.LocalPlayer.BurnVisualCount);

        world.LocalPlayer.ReduceBurnDuration(70f);
        Assert.Equal(0, world.LocalPlayer.BurnVisualCount);
    }

    [Fact]
    public void MedicHealing_ReducesAfterburnInAdditionToNaturalDecay()
    {
        var world = CreateWorld();
        const byte teammateSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Medic);

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var target));

        world.LocalPlayer.TeleportTo(200f, 200f);
        target.TeleportTo(240f, 200f);
        target.IgniteAfterburn(world.LocalPlayer.Id, durationIncreaseSourceTicks: 100f, intensityIncrease: 5f, afterburnFalloff: false, burnFalloffAmount: 0f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: target.X,
            AimWorldY: target.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(98f, target.BurnDurationSourceTicks, 3);
    }

    [Fact]
    public void PyroAirblast_ExtinguishesFriendlyAfterburn()
    {
        var world = CreateWorld();
        const byte teammateSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var target));

        world.LocalPlayer.TeleportTo(200f, 200f);
        target.TeleportTo(245f, 200f);
        target.IgniteAfterburn(world.LocalPlayer.Id, durationIncreaseSourceTicks: 100f, intensityIncrease: 5f, afterburnFalloff: false, burnFalloffAmount: 0f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: target.X,
            AimWorldY: target.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.False(target.IsBurning);
        Assert.Equal(0f, target.BurnIntensity);
        Assert.Equal(0f, target.BurnDurationSourceTicks);
    }

    [Fact]
    public void PyroAirblast_ExtinguishesFriendlyAfterburnAndReemitsFlames()
    {
        var world = CreateWorld();
        const byte teammateSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Heavy));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var target));

        world.LocalPlayer.TeleportTo(200f, 200f);
        target.TeleportTo(245f, 200f);
        target.IgniteAfterburn(world.LocalPlayer.Id, durationIncreaseSourceTicks: 210f, intensityIncrease: 10f, afterburnFalloff: false, burnFalloffAmount: 0f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: target.X,
            AimWorldY: target.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.False(target.IsBurning);
        Assert.NotEmpty(world.Flames);
        Assert.All(world.Flames, flame =>
        {
            Assert.Equal(world.LocalPlayer.Id, flame.OwnerId);
            Assert.Equal(world.LocalPlayer.Team, flame.Team);
        });
    }

    [Fact]
    public void Afterburn_KillCreditsBurnerWithFlamethrowerIcon()
    {
        var world = CreateWorld();
        const byte enemySlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var target));

        target.ForceSetHealth(1);
        target.IgniteAfterburn(world.LocalPlayer.Id, durationIncreaseSourceTicks: 30f, intensityIncrease: 60f, afterburnFalloff: false, burnFalloffAmount: 0f);

        AdvanceTicks(world, 3);

        Assert.False(target.IsAlive);
        var entry = Assert.Single(world.KillFeed);
        Assert.Equal(world.LocalPlayer.DisplayName, entry.KillerName);
        Assert.Equal("FlamethrowerS", entry.WeaponSpriteName);
    }

    [Fact]
    public void HealingCabinet_UsesFourSecondPerPlayerSoundCooldown()
    {
        var world = CreateWorld();
        var cabinet = new RoomObjectMarker(RoomObjectType.HealingCabinet, 180f, 200f, 32f, 48f, "sprite74", null, "HealingCabinet");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [cabinet]));
        world.TeleportLocalPlayer(cabinet.CenterX, cabinet.CenterY);

        world.SetLocalHealth(world.LocalPlayer.MaxHealth - 20);
        world.AdvanceOneTick();

        var firstTickSounds = world.DrainPendingSoundEvents();
        Assert.Contains(firstTickSounds, soundEvent => soundEvent.SoundName == "CbntHealSnd");

        world.AdvanceOneTick();

        var secondTickSounds = world.DrainPendingSoundEvents();
        Assert.DoesNotContain(secondTickSounds, soundEvent => soundEvent.SoundName == "CbntHealSnd");

        world.SetLocalHealth(world.LocalPlayer.MaxHealth - 10);
        world.AdvanceOneTick();

        var thirdTickSounds = world.DrainPendingSoundEvents();
        Assert.DoesNotContain(thirdTickSounds, soundEvent => soundEvent.SoundName == "CbntHealSnd");

        AdvanceTicks(world, world.Config.TicksPerSecond * 4);
        world.TeleportLocalPlayer(cabinet.CenterX, cabinet.CenterY);
        world.SetLocalHealth(world.LocalPlayer.MaxHealth - 10);
        world.AdvanceOneTick();

        var cooldownExpiredSounds = world.DrainPendingSoundEvents();
        Assert.Contains(cooldownExpiredSounds, soundEvent => soundEvent.SoundName == "CbntHealSnd");
    }

    [Fact]
    public void HealingCabinet_PlaysHealSoundForAmmoOnlyStreamRefillsWithoutImmediateSpam()
    {
        var world = CreateWorld();
        var cabinet = new RoomObjectMarker(RoomObjectType.HealingCabinet, 180f, 200f, 32f, 48f, "sprite74", null, "HealingCabinet");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [cabinet]));
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.TeleportLocalPlayer(cabinet.CenterX, cabinet.CenterY);
        world.DrainPendingSoundEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: cabinet.CenterX + 100f,
            AimWorldY: cabinet.CenterY,
            DebugKill: false));
        world.AdvanceOneTick();

        var firstTickSounds = world.DrainPendingSoundEvents();
        Assert.Contains(firstTickSounds, soundEvent => soundEvent.SoundName == "CbntHealSnd");

        AdvanceTicks(world, world.Config.TicksPerSecond * 2);
        var cooldownSounds = world.DrainPendingSoundEvents();
        Assert.DoesNotContain(cooldownSounds, soundEvent => soundEvent.SoundName == "CbntHealSnd");
    }

    [Fact]
    public void HealingCabinet_ResetsHeavySandvichCooldown()
    {
        var world = CreateWorld();
        var cabinet = new RoomObjectMarker(RoomObjectType.HealingCabinet, 180f, 200f, 32f, 48f, "sprite74", null, "HealingCabinet");
        world.CombatTestSetLevel(CreateLevel(roomObjects: [cabinet]));
        SetLocalClassAndRespawn(world, PlayerClass.Heavy);
        world.TeleportLocalPlayer(60f, 200f);
        world.SetLocalHealth(world.LocalPlayer.MaxHealth - 20);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: 120f,
            AimWorldY: 200f,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.True(world.LocalPlayer.HeavyEatCooldownTicksRemaining > 0);
        world.SetLocalHealth(world.LocalPlayer.MaxHealth);
        world.DrainPendingSoundEvents();
        world.TeleportLocalPlayer(cabinet.CenterX, cabinet.CenterY);

        world.AdvanceOneTick();

        var soundEvents = world.DrainPendingSoundEvents();
        Assert.Equal(0, world.LocalPlayer.HeavyEatCooldownTicksRemaining);
        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "CbntHealSnd");
    }

    [Fact]
    public void UberedPlayer_ExtinguishesAfterburnBeforeItDealsDamage()
    {
        var world = CreateWorld();
        var healthBeforeTick = world.LocalPlayer.Health;

        world.LocalPlayer.IgniteAfterburn(ownerPlayerId: 99, durationIncreaseSourceTicks: 60f, intensityIncrease: 12f, afterburnFalloff: false, burnFalloffAmount: 0f);
        world.LocalPlayer.RefreshUber();

        world.AdvanceOneTick();

        Assert.Equal(healthBeforeTick, world.LocalPlayer.Health);
        Assert.False(world.LocalPlayer.IsBurning);
    }

    [Fact]
    public void BurningNonPyro_EventuallyTriggersMedicAlertChatBubble()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);
        world.LocalPlayer.IgniteAfterburn(ownerPlayerId: 99, durationIncreaseSourceTicks: 210f, intensityIncrease: 6f, afterburnFalloff: false, burnFalloffAmount: 0f);

        var bubbleTriggered = false;
        var simulationTicksToCheck = (int)MathF.Ceiling(90f * world.Config.TicksPerSecond / LegacyMovementModel.SourceTicksPerSecond);
        for (var tick = 0; tick < simulationTicksToCheck; tick += 1)
        {
            world.AdvanceOneTick();
            if (world.LocalPlayer.IsChatBubbleVisible && world.LocalPlayer.ChatBubbleFrameIndex == 49)
            {
                bubbleTriggered = true;
                break;
            }
        }

        Assert.True(bubbleTriggered);
    }

    [Fact]
    public void RocketSelfBlast_EntersRecoveryStateUntilUpwardMotionEnds()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 500f, 1000f, 40f);
        var wall = new LevelSolid(46f, 120f, 8f, 220f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(20f, world.LocalPlayer.Y);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        var exploded = ConsumePendingExplosion(world);
        world.SetLocalInput(default);
        Assert.True(exploded || AdvanceUntilExplosion(world));

        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);

        for (var tick = 0; tick < 60 && world.LocalPlayer.MovementState != LegacyMovementState.None; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(LegacyMovementState.None, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void RocketSelfBlast_UsesSourceCadenceAtDifferentServerTickRates()
    {
        var thirtyTickWorld = CreateWorld();
        var sixtyTickWorld = CreateWorld(60);

        var thirtyTickVerticalSpeed = TriggerPointBlankRocketJump(thirtyTickWorld);
        var sixtyTickVerticalSpeed = TriggerPointBlankRocketJump(sixtyTickWorld);

        Assert.Equal(thirtyTickVerticalSpeed, sixtyTickVerticalSpeed, 3);
    }

    [Fact]
    public void RocketSelfBlast_UsesFullSourceSplashDamage()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 500f, 1000f, 40f);
        var wall = new LevelSolid(46f, 120f, 8f, 220f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(20f, world.LocalPlayer.Y);
        var healthBeforeExplosion = world.LocalPlayer.Health;
        var localXAtShot = world.LocalPlayer.X;
        var localYAtShot = world.LocalPlayer.Y;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        var explosion = ConsumePendingExplosionWithVisual(world);
        world.SetLocalInput(default);
        if (!explosion.Exploded)
        {
            explosion = AdvanceUntilExplosionWithVisual(world);
        }

        Assert.True(explosion.Exploded);
        var distance = DistanceBetween(
            explosion.X,
            explosion.Y,
            localXAtShot,
            localYAtShot);
        var fullSplashDamage = RocketProjectileEntity.ExplosionDamage * (1f - (distance / RocketProjectileEntity.BlastRadius));
        var damageTaken = healthBeforeExplosion - world.LocalPlayer.Health;

        Assert.InRange(damageTaken, Math.Max(0, (int)fullSplashDamage - 1), (int)MathF.Ceiling(fullSplashDamage));
    }

    [Fact]
    public void RocketExplosion_DoesNotJuggleGroundedFriendlyTeammate()
    {
        var world = CreateWorld();
        const byte teammateSlot = 3;
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        var wall = new LevelSolid(346f, 120f, 8f, 220f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(320f, 220f);

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var teammate));

        teammate.TeleportTo(350f, 220f);
        var teammateHealthBefore = teammate.Health;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: 420f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.True(AdvanceUntilExplosion(world));
        Assert.Equal(teammateHealthBefore, teammate.Health);
        Assert.Equal(0f, teammate.HorizontalSpeed);
        Assert.Equal(0f, teammate.VerticalSpeed);
        Assert.Equal(LegacyMovementState.None, teammate.MovementState);
    }

    [Fact]
    public void RocketExplosion_UsesSourceMineTriggerRadius()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(200f, 200f);

        var nearMine = world.CombatTestSpawnMine(world.LocalPlayer, 220f, 200f, stickied: true);
        var farMine = world.CombatTestSpawnMine(world.LocalPlayer, 200f, 230f, stickied: true);

        world.CombatTestExplodeRocket(world.LocalPlayer, 200f, 200f);

        var remainingMine = Assert.Single(world.Mines);
        Assert.Equal(farMine.Id, remainingMine.Id);
        Assert.DoesNotContain(world.Mines, mine => mine.Id == nearMine.Id);
    }

    [Fact]
    public void RocketExplosion_FromBelowUsesSourceLiftBonusAndSpeedScale()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        target.TeleportTo(320f, 200f);
        world.CombatTestExplodeRocket(world.LocalPlayer, 320f, 225f);

        Assert.Equal(0f, target.HorizontalSpeed, 3);
        Assert.Equal(-288f, target.VerticalSpeed, 3);
        Assert.Equal(LegacyMovementState.RocketJuggle, target.MovementState);
    }

    [Fact]
    public void RocketExplosion_LongFlightUsesReducedSourceKnockback()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        target.TeleportTo(320f, 200f);
        var rocket = world.CombatTestSpawnRocket(world.LocalPlayer, target.X, target.Y);
        AdvanceRocketTicks(rocket, 21, (float)world.Config.FixedDeltaSeconds);
        rocket.MoveTo(target.X, target.Y);

        world.CombatTestExplodeRocket(rocket);

        Assert.Equal(0f, target.HorizontalSpeed, 3);
        Assert.Equal(-150f, target.VerticalSpeed, 3);
        Assert.Equal(LegacyMovementState.RocketJuggle, target.MovementState);
    }

    [Fact]
    public void RocketExplosion_VeryLateFlightUsesZeroSourceKnockback()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        target.TeleportTo(320f, 200f);
        var rocket = world.CombatTestSpawnRocket(world.LocalPlayer, target.X, target.Y);
        AdvanceRocketTicks(rocket, 31, (float)world.Config.FixedDeltaSeconds);
        rocket.MoveTo(target.X, target.Y);

        world.CombatTestExplodeRocket(rocket);

        Assert.Equal(0f, target.HorizontalSpeed, 3);
        Assert.Equal(0f, target.VerticalSpeed, 3);
        Assert.Equal(LegacyMovementState.RocketJuggle, target.MovementState);
    }

    [Fact]
    public void Rocket_LongRangeEntersFadeAndDespawnsWithoutExplosion()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(100f, 200f);

        var rocket = world.CombatTestSpawnRocket(
            world.LocalPlayer,
            world.LocalPlayer.X + RocketProjectileEntity.MaxDistanceToTravel + 20f,
            world.LocalPlayer.Y,
            speed: 0f,
            directionRadians: 0f);

        world.AdvanceOneTick();
        Assert.True(rocket.IsFading);
        Assert.False(ConsumePendingExplosion(world));

        for (var tick = 0; tick < 12 && world.Rockets.Count > 0; tick += 1)
        {
            world.AdvanceOneTick();
            Assert.False(ConsumePendingExplosion(world));
        }

        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void Rocket_FadingEnvironmentHitDoesNotExplode()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1400f, 40f);
        var wall = new LevelSolid(935f, 0f, 12f, 400f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(100f, 220f);

        world.CombatTestSpawnRocket(
            world.LocalPlayer,
            world.LocalPlayer.X + RocketProjectileEntity.MaxDistanceToTravel + 20f,
            world.LocalPlayer.Y,
            speed: 15f,
            directionRadians: 0f);

        world.AdvanceOneTick();

        Assert.Empty(world.Rockets);
        Assert.False(ConsumePendingExplosion(world));
    }

    [Fact]
    public void Rocket_PassingFriendlyTeammateReducesRemainingSourceRangeOnce()
    {
        var world = CreateWorld();
        const byte teammateSlot = 3;
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(160f, 220f);

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var teammate));

        teammate.TeleportTo(200f, 220f);
        var rocket = world.CombatTestSpawnRocket(world.LocalPlayer, 188f, 220f, speed: 4f, directionRadians: 0f);

        for (var tick = 0; tick < 4; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(700f, rocket.DistanceToTravel, 3);
        Assert.Contains(teammate.Id, rocket.PassedFriendlyPlayerIds);
    }

    [Fact]
    public void RocketSelfBlast_UsesSourceSpeedBoost()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(260f, 200f);

        world.CombatTestExplodeRocket(world.LocalPlayer, world.LocalPlayer.X, world.LocalPlayer.Y);

        Assert.Equal(0f, world.LocalPlayer.HorizontalSpeed, 3);
        Assert.Equal(-254.4f, world.LocalPlayer.VerticalSpeed, 3);
        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void RocketSelfBlast_UberedUsesReducedSourceSpeedBoost()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(260f, 200f);
        world.LocalPlayer.RefreshUber(60);

        world.CombatTestExplodeRocket(world.LocalPlayer, world.LocalPlayer.X, world.LocalPlayer.Y);

        Assert.Equal(0f, world.LocalPlayer.HorizontalSpeed, 3);
        Assert.Equal(-253.2f, world.LocalPlayer.VerticalSpeed, 3);
        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void MineDetonation_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 80f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.NotEmpty(world.Mines);

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Explosion");
    }

    [Fact]
    public void DemomanAtMaxMines_FiringPrimaryDoesNotConsumeAmmoOrCooldown()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);
        world.TeleportLocalPlayer(200f, 200f);

        for (var mineIndex = 0; mineIndex < world.LocalPlayer.PrimaryWeapon.MaxAmmo; mineIndex += 1)
        {
            world.CombatTestSpawnMine(world.LocalPlayer, 220f + (mineIndex * 8f), 200f);
        }

        var ammoBeforeShot = world.LocalPlayer.CurrentShells;
        var cooldownBeforeShot = world.LocalPlayer.PrimaryCooldownTicks;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 80f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(world.LocalPlayer.PrimaryWeapon.MaxAmmo, world.Mines.Count);
        Assert.Equal(ammoBeforeShot, world.LocalPlayer.CurrentShells);
        Assert.Equal(cooldownBeforeShot, world.LocalPlayer.PrimaryCooldownTicks);
    }

    [Fact]
    public void MineDetonation_UsesSourceSelfDamageResistance()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);
        world.TeleportLocalPlayer(300f, 220f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 80f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var mine = Assert.Single(world.Mines);
        mine.MoveTo(world.LocalPlayer.X, world.LocalPlayer.Y);
        mine.Stick();
        var healthBeforeExplosion = world.LocalPlayer.Health;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var damageTaken = healthBeforeExplosion - world.LocalPlayer.Health;
        Assert.InRange(damageTaken, 24, 25);
        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void MineDetonation_UsesPreMovePositionBeforePlayerMovement()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);
        world.TeleportLocalPlayer(200f, 220f);
        world.CombatTestSpawnMine(world.LocalPlayer, 171f, 220f, stickied: true);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.True(world.LocalPlayer.Health < world.LocalPlayer.MaxHealth);
        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void MineDetonation_EnemyVictimUsesSourceJuggleState()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);
        world.TeleportLocalPlayer(200f, 220f);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));
        target.TeleportTo(320f, 220f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 80f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var mine = Assert.Single(world.Mines);
        mine.MoveTo(300f, 220f);
        mine.Stick();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.True(target.Health < target.MaxHealth);
        Assert.Equal(LegacyMovementState.FriendlyJuggle, target.MovementState);
    }

    [Fact]
    public void MineDetonation_UsesFullAffectRadiusForDeadBodyAndGibFalloff()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        var deadBody = world.CombatTestSpawnDeadBody(world.LocalPlayer, 248f, 200f);
        var gib = world.CombatTestSpawnPlayerGib("GibS", 0, 248f, 200f);
        var mine = world.CombatTestSpawnMine(world.LocalPlayer, 200f, 200f);

        world.CombatTestExplodeMine(mine);

        Assert.True(deadBody.HorizontalSpeed > 1f);
        Assert.True(gib.VelocityX > 2f);
    }

    [Fact]
    public void MineDetonation_BeyondSourceThresholdDoesNotAffectEnemy()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);
        world.TeleportLocalPlayer(200f, 220f);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));
        target.TeleportTo(331f, 220f);
        var healthBeforeExplosion = target.Health;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 80f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var mine = Assert.Single(world.Mines);
        mine.MoveTo(300f, 220f);
        mine.Stick();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(healthBeforeExplosion, target.Health);
        Assert.Equal(0f, target.HorizontalSpeed);
        Assert.Equal(0f, target.VerticalSpeed);
        Assert.Equal(LegacyMovementState.None, target.MovementState);
    }

    [Fact]
    public void MineExplosion_PopsNearbyRocketAtCloseRange()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);

        var mine = world.CombatTestSpawnMine(world.LocalPlayer, 200f, 200f, stickied: true);
        world.CombatTestSpawnRocket(world.LocalPlayer, 212f, 200f);

        world.CombatTestExplodeMine(mine);

        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void RocketLauncher_BlockedSpawnExplodesNextTickAtSpawnPoint()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        var wall = new LevelSolid(210f, 190f, 6f, 60f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(200f, 220f);
        var playerXBeforeTick = world.LocalPlayer.X;
        var playerYBeforeTick = world.LocalPlayer.Y;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var rocket = Assert.Single(world.Rockets);
        Assert.Equal(playerXBeforeTick + 20f, rocket.X, 3);
        Assert.Equal(playerYBeforeTick, rocket.Y, 3);

        var explosion = AdvanceUntilExplosionWithVisual(world, maxTicks: 1);
        Assert.True(explosion.Exploded);
        Assert.Equal(playerXBeforeTick + 20f, explosion.X, 3);
        Assert.Equal(playerYBeforeTick, explosion.Y, 3);
    }

    [Fact]
    public void MineExplosion_ShovesRocketWithinAffectRadius()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);

        var mine = world.CombatTestSpawnMine(world.LocalPlayer, 200f, 200f, stickied: true);
        world.CombatTestSpawnRocket(world.LocalPlayer, 230f, 200f);

        world.CombatTestExplodeMine(mine);

        var rocket = Assert.Single(world.Rockets);
        var rocketVelocityX = MathF.Cos(rocket.DirectionRadians) * rocket.Speed;
        var rocketVelocityY = MathF.Sin(rocket.DirectionRadians) * rocket.Speed;
        Assert.True(rocketVelocityX > 5f);
        Assert.Equal(0f, rocketVelocityY, 3);
    }

    [Fact]
    public void PyroAirblast_UsesSourceDirectionalPushAndFixedLift()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.TeleportLocalPlayer(300f, world.LocalPlayer.Y);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        target.TeleportTo(world.LocalPlayer.X + 70f, world.LocalPlayer.Y);
        var localXBeforeAirblast = world.LocalPlayer.X;
        var localYBeforeAirblast = world.LocalPlayer.Y;
        var targetXBeforeAirblast = target.X;
        var targetYBeforeAirblast = target.Y;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var dt = (float)world.Config.FixedDeltaSeconds;
        var scale = 1f - (DistanceBetween(localXBeforeAirblast, localYBeforeAirblast, targetXBeforeAirblast, targetYBeforeAirblast) / 150f);
        var expectedHorizontalSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
            15f * LegacyMovementModel.SourceTicksPerSecond * scale,
            target.RunPower,
            movementScale: 1f,
            hasHorizontalInput: false,
            horizontalDirection: 0f,
            state: LegacyMovementState.Airblast,
            isCarryingIntel: false,
            deltaSeconds: dt);
        var expectedVerticalBeforeFallbackGravity = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(
            -2f * LegacyMovementModel.SourceTicksPerSecond,
            LegacyMovementModel.GravityPerTick,
            dt);
        var expectedVerticalSpeed = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(
            expectedVerticalBeforeFallbackGravity,
            LegacyMovementModel.GravityPerTick,
            dt);

        Assert.Equal(expectedHorizontalSpeed, target.HorizontalSpeed, 2);
        Assert.Equal(expectedVerticalSpeed, target.VerticalSpeed, 2);
        Assert.Equal(LegacyMovementState.Airblast, target.MovementState);
    }

    [Fact]
    public void PyroPrimary_HeldFireStartsFlamethrowerSoundOncePerBurst()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.LocalPlayer.ForceSetAmmo(world.LocalPlayer.MaxShells);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));

        var flameSoundCount = 0;
        for (var tick = 0; tick < 3; tick += 1)
        {
            world.AdvanceOneTick();
            flameSoundCount += world
                .DrainPendingSoundEvents()
                .Count(soundEvent => soundEvent.SoundName == "FlamethrowerSnd");
        }

        world.SetLocalInput(default);

        Assert.Equal(1, flameSoundCount);
    }

    [Fact]
    public void PyroPrimary_HeldFireAfterEmptyRequiresReleaseBeforeNextBurst()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.LocalPlayer.ForceSetAmmo(2);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));

        var flameSoundCount = 0;
        world.AdvanceOneTick();
        flameSoundCount += world
            .DrainPendingSoundEvents()
            .Count(soundEvent => soundEvent.SoundName == "FlamethrowerSnd");
        Assert.Single(world.Flames);

        for (var tick = 0; tick < PlayerEntity.PyroPrimaryRefillBufferTicks; tick += 1)
        {
            world.AdvanceOneTick();
            flameSoundCount += world
                .DrainPendingSoundEvents()
                .Count(soundEvent => soundEvent.SoundName == "FlamethrowerSnd");
            Assert.Single(world.Flames);
        }

        world.AdvanceOneTick();
        flameSoundCount += world
            .DrainPendingSoundEvents()
            .Count(soundEvent => soundEvent.SoundName == "FlamethrowerSnd");
        Assert.Single(world.Flames);
        Assert.True(world.LocalPlayer.PyroPrimaryRequiresReleaseAfterEmpty);
        Assert.True(world.LocalPlayer.CurrentShells > 0);
        Assert.Equal(1, flameSoundCount);

        world.SetLocalInput(default);
        world.AdvanceOneTick();
        Assert.False(world.LocalPlayer.PyroPrimaryRequiresReleaseAfterEmpty);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(2, world.Flames.Count);
    }

    [Fact]
    public void PyroAirblast_UsesSourceCostAndCooldownWindows()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.LocalPlayer.ForceSetAmmo(world.LocalPlayer.MaxShells);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(world.LocalPlayer.MaxShells - 40, world.LocalPlayer.CurrentShells);
        Assert.Equal(40, world.LocalPlayer.PyroAirblastCooldownTicks);
        Assert.Equal(15, world.LocalPlayer.PrimaryCooldownTicks);
    }

    [Fact]
    public void PyroAirblast_PrimaryHeldWithoutReadyFlareDoesNotLeakPrimaryFlame()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.LocalPlayer.ForceSetAmmo(world.LocalPlayer.MaxShells);

        Assert.True(world.LocalPlayer.PyroFlareCooldownTicks > 0);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        var soundEvents = world.DrainPendingSoundEvents();
        world.SetLocalInput(default);

        Assert.Empty(world.Flames);
        Assert.Empty(world.Flares);
        Assert.DoesNotContain(soundEvents, soundEvent => soundEvent.SoundName == "FlamethrowerSnd");
        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "CompressionBlastSnd");
    }

    [Fact]
    public void PyroAirblast_PrimaryHeldSpawnsFlareWhenSourceCooldownIsReady()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);

        Assert.Equal(PlayerEntity.PyroFlareReloadTicks, world.LocalPlayer.PyroFlareCooldownTicks);
        for (var tick = 0; tick < PlayerEntity.PyroFlareReloadTicks; tick += 1)
        {
            world.AdvanceOneTick();
        }

        world.LocalPlayer.ForceSetAmmo(world.LocalPlayer.MaxShells);
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var flare = Assert.Single(world.Flares);
        Assert.Equal(world.LocalPlayer.Team, flare.Team);
        Assert.Equal(world.LocalPlayer.Id, flare.OwnerId);
        Assert.True(world.LocalPlayer.CurrentShells <= world.LocalPlayer.MaxShells - PlayerEntity.PyroFlareAmmoRequirement);
        Assert.Equal(PlayerEntity.PyroFlareReloadTicks, world.LocalPlayer.PyroFlareCooldownTicks);
    }

    [Fact]
    public void PyroAirblast_ReflectedRocketKeepsItsCurrentSpeed()
    {
        var world = CreateWorld();
        const byte soldierSlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.TeleportLocalPlayer(300f, world.LocalPlayer.Y);

        Assert.True(world.TryPrepareNetworkPlayerJoin(soldierSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(soldierSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(soldierSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(soldierSlot, out var soldier));

        soldier.TeleportTo(world.LocalPlayer.X + 110f, world.LocalPlayer.Y);
        Assert.True(world.TrySetNetworkPlayerInput(
            soldierSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: world.LocalPlayer.X,
                AimWorldY: world.LocalPlayer.Y,
                DebugKill: false)));
        world.AdvanceOneTick();
        Assert.True(world.TrySetNetworkPlayerInput(soldierSlot, default));
        Assert.Single(world.Rockets);

        world.AdvanceOneTick();
        var rocketBeforeReflect = Assert.Single(world.Rockets);
        var speedBeforeReflect = rocketBeforeReflect.Speed;
        var expectedSpeedAfterReflectedAdvance = (speedBeforeReflect + 1f) * 0.92f;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: rocketBeforeReflect.X,
            AimWorldY: rocketBeforeReflect.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var reflectedRocket = Assert.Single(world.Rockets);
        Assert.Equal(world.LocalPlayer.Id, reflectedRocket.OwnerId);
        Assert.Equal(world.LocalPlayer.Team, reflectedRocket.Team);
        Assert.Equal(expectedSpeedAfterReflectedAdvance, reflectedRocket.Speed, 3);
    }

    [Fact]
    public void PyroAirblast_ReflectsEnemyFlare()
    {
        var world = CreateWorld();
        const byte enemySlot = 3;
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.TeleportLocalPlayer(300f, world.LocalPlayer.Y);

        Assert.True(world.TryPrepareNetworkPlayerJoin(enemySlot));
        Assert.True(world.TrySetNetworkPlayerTeam(enemySlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(enemySlot, PlayerClass.Pyro));
        Assert.True(world.TryGetNetworkPlayer(enemySlot, out var enemyPyro));

        enemyPyro.TeleportTo(world.LocalPlayer.X + 80f, world.LocalPlayer.Y);
        var flare = world.CombatTestSpawnFlare(enemyPyro, world.LocalPlayer.X + 60f, world.LocalPlayer.Y, velocityX: -15f, velocityY: 0f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var reflectedFlare = Assert.Single(world.Flares);
        Assert.Same(flare, reflectedFlare);
        Assert.Equal(world.LocalPlayer.Id, reflectedFlare.OwnerId);
        Assert.Equal(world.LocalPlayer.Team, reflectedFlare.Team);
        Assert.True(reflectedFlare.VelocityX > 0f);
        Assert.Equal(FlareProjectileEntity.LifetimeTicks, reflectedFlare.TicksRemaining);
    }

    [Fact]
    public void Bubble_PopsEnemyFlare()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        world.DespawnEnemyDummy();

        world.CombatTestSpawnBubble(world.LocalPlayer, 200f, 200f);
        world.EnemyPlayer.TeleportTo(160f, 200f);
        world.CombatTestSpawnFlare(world.EnemyPlayer, 185f, 200f, velocityX: 15f, velocityY: 0f);

        world.AdvanceOneTick();

        Assert.Empty(world.Bubbles);
        Assert.Empty(world.Flares);
    }

    [Fact]
    public void PyroAirblast_UsesSourceMaskInsteadOfWideCone()
    {
        var world = CreateWorld();
        const byte targetSlot = 3;
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.DespawnEnemyDummy();
        SetLocalClassAndRespawn(world, PlayerClass.Pyro);
        world.TeleportLocalPlayer(300f, 220f);

        Assert.True(world.TryPrepareNetworkPlayerJoin(targetSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(targetSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(targetSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(targetSlot, out var target));

        target.TeleportTo(world.LocalPlayer.X + 96f, world.LocalPlayer.Y + 40f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X + 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(0f, target.HorizontalSpeed);
        Assert.Equal(0f, target.VerticalSpeed);
        Assert.Equal(LegacyMovementState.None, target.MovementState);
    }

    [Fact]
    public void Movement_ClampsHorizontalSpeedToSourceStepMaximum()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.TeleportLocalPlayer(200f, 220f);
        world.LocalPlayer.AddImpulse(900f, 0f);

        world.AdvanceOneTick();

        Assert.Equal(LegacyMovementModel.MaxStepSpeedPerTick * LegacyMovementModel.SourceTicksPerSecond, world.LocalPlayer.HorizontalSpeed, 3);
    }

    [Fact]
    public void Movement_SpinjumpCancelsSourceGravityAfterTurnedWallRub()
    {
        var dt = (float)(1d / SimulationConfig.DefaultTicksPerSecond);
        var spinjumpVerticalSpeed = RunWallRubSpinjumpSequence(LegacyMovementState.None, turnBeforeWallRub: true);
        var controlVerticalSpeed = RunWallRubSpinjumpSequence(LegacyMovementState.None, turnBeforeWallRub: false);
        var expectedSingleGravityTick = AdvanceAirborneVerticalSpeedForTick(0f, LegacyMovementModel.GravityPerTick, dt);
        var expectedDoubleGravityTick = AdvanceAirborneVerticalSpeedForTick(expectedSingleGravityTick, LegacyMovementModel.GravityPerTick, dt);

        Assert.Equal(expectedSingleGravityTick, spinjumpVerticalSpeed, 3);
        Assert.Equal(expectedDoubleGravityTick, controlVerticalSpeed, 3);
    }

    [Fact]
    public void Movement_BlastStateSpinjumpUsesSourceBlastGravityVariant()
    {
        var dt = (float)(1d / SimulationConfig.DefaultTicksPerSecond);
        var spinjumpVerticalSpeed = RunWallRubSpinjumpSequence(LegacyMovementState.ExplosionRecovery, turnBeforeWallRub: true);
        var controlVerticalSpeed = RunWallRubSpinjumpSequence(LegacyMovementState.ExplosionRecovery, turnBeforeWallRub: false);
        var expectedSingleGravityTick = AdvanceAirborneVerticalSpeedForTick(0f, LegacyMovementModel.BlastGravityPerTick, dt);
        var expectedDoubleGravityTick = AdvanceAirborneVerticalSpeedForTick(expectedSingleGravityTick, LegacyMovementModel.BlastGravityPerTick, dt);

        Assert.Equal(expectedSingleGravityTick, spinjumpVerticalSpeed, 3);
        Assert.Equal(expectedDoubleGravityTick, controlVerticalSpeed, 3);
    }

    [Fact]
    public void Movement_SnapsDownSixPixelDropInSourceEndStep()
    {
        var world = CreateWorld();
        var leftFloor = new LevelSolid(0f, 240f, 220f, 24f);
        var rightFloor = new LevelSolid(220f, 246f, 380f, 24f);
        world.CombatTestSetLevel(CreateLevel(solids: [leftFloor, rightFloor]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(224f, leftFloor.Top - (world.LocalPlayer.Height / 2f));
        world.LocalPlayer.AddImpulse(450f, 0f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(rightFloor.Top - (world.LocalPlayer.Height / 2f), world.LocalPlayer.Y, 3);
        Assert.True(world.LocalPlayer.IsGrounded);
    }

    [Fact]
    public void Movement_FastRunSnapsDownTwelvePixelDropInSourceEndStep()
    {
        var world = CreateWorld();
        var leftFloor = new LevelSolid(0f, 240f, 220f, 24f);
        var rightFloor = new LevelSolid(220f, 252f, 380f, 24f);
        world.CombatTestSetLevel(CreateLevel(solids: [leftFloor, rightFloor]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(224f, leftFloor.Top - (world.LocalPlayer.Height / 2f));
        world.LocalPlayer.AddImpulse(450f, 0f);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.Equal(rightFloor.Top - (world.LocalPlayer.Height / 2f), world.LocalPlayer.Y, 3);
        Assert.True(world.LocalPlayer.IsGrounded);
    }

    [Fact]
    public void SpyBackstab_EmitsBackstabVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Spy);

        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 32f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "BackstabRed");
    }

    [Fact]
    public void SpyBackstab_HitboxSpawnEmitsKnifeSound()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Spy);

        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        world.DrainPendingSoundEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 32f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        AdvanceTicks(world, PlayerEntity.SpyBackstabWindupTicksDefault);
        var soundEvents = world.DrainPendingSoundEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "KnifeSnd");
    }

    [Fact]
    public void EngineerShotHittingWall_EmitsImpactVisual()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        var wall = new LevelSolid(250f, 0f, 20f, 240f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);
        world.TeleportLocalPlayer(200f, 220f);
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: 400f,
            AimWorldY: 220f,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        AdvanceTicks(world, 20);
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Impact");
    }

    [Fact]
    public void MedicNeedleHittingWall_EmitsImpactVisual()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 1000f, 40f);
        var wall = new LevelSolid(250f, 0f, 20f, 240f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Medic);
        world.TeleportLocalPlayer(200f, 220f);
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: 400f,
            AimWorldY: 220f,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        AdvanceTicks(world, 20);
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Impact");
    }

    [Fact]
    public void Movement_WallRubSpinjumpEmitsWallspinDustVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);

        const float playerX = 200f;
        const float playerY = 120f;
        var wall = new LevelSolid(playerX + (world.LocalPlayer.Width / 2f), playerY - 120f, 12f, 240f);
        world.CombatTestSetLevel(CreateLevel(solids: [wall]));
        world.TeleportLocalPlayer(playerX, playerY);
        world.DrainPendingVisualEvents();

        AdvanceWallRubTick(world, aimLeft: true);
        AdvanceWallRubTick(world, aimLeft: true);

        var visualEvents = world.DrainPendingVisualEvents();
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "WallspinDust");
    }

    [Fact]
    public void MovingIntelCarrier_EmitsLooseSheetTrailVisual()
    {
        var world = CreateWorld();

        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        AdvanceTicks(world, 60);
        world.SetLocalInput(default);

        var visualEvents = world.DrainPendingVisualEvents();
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "LooseSheet");
    }

    [Fact]
    public void Jumping_EmitsJumpSound()
    {
        var world = CreateWorld();
        world.DrainPendingSoundEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: true,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        var soundEvents = world.DrainPendingSoundEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "JumpSnd");
    }

    [Fact]
    public void IdlePlayerOnFlatGround_RemainsGroundedAcrossTicks()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 600f, 24f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.TeleportLocalPlayer(150f, floor.Top - (world.LocalPlayer.Height / 2f));

        for (var tick = 0; tick < 6; tick += 1)
        {
            world.SetLocalInput(default);
            world.AdvanceOneTick();
            Assert.True(world.LocalPlayer.IsGrounded, $"player lost grounded state on tick {tick}");
            Assert.Equal(0f, world.LocalPlayer.HorizontalSpeed);
        }
    }

    [Fact]
    public void FastLanding_SnapsPlayerFlushToGroundInsteadOfLeavingHoverGap()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 600f, 24f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.TeleportLocalPlayer(150f, floor.Top - world.LocalPlayer.CollisionBottomOffset - 4f);
        world.LocalPlayer.AddImpulse(0f, 2000f);

        world.SetLocalInput(default);
        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsGrounded);
        Assert.Equal(floor.Top, world.LocalPlayer.Bottom, 3);
    }

    [Fact]
    public void JumpingIntoLowCeiling_DoesNotSnapPlayerBackToCorridorEntrance()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 600f, 24f);
        var ceiling = new LevelSolid(160f, 170f, 260f, 18f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, ceiling]));
        world.TeleportLocalPlayer(150f, floor.Top - (world.LocalPlayer.Height / 2f));

        var previousX = world.LocalPlayer.X;
        for (var tick = 0; tick < 16; tick += 1)
        {
            world.SetLocalInput(new PlayerInputSnapshot(
                Left: false,
                Right: true,
                Up: tick == 0,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: world.LocalPlayer.X + 100f,
                AimWorldY: world.LocalPlayer.Y,
                DebugKill: false));
            world.AdvanceOneTick();
            Assert.True(world.LocalPlayer.X >= previousX - 0.1f);
            previousX = world.LocalPlayer.X;
        }

        Assert.True(world.LocalPlayer.X > ceiling.Left + 10f);
    }

    [Fact]
    public void ActiveSetupGate_RemainsBlockingDuringJumpAndWiggleMovement()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.ControlPointSetupGate, 300f, 200f, 60f, 6f, "setup-gate", SourceName: "setup-gate");
        var level = CreateLevel(mode: GameModeKind.ControlPoint, roomObjects: [gate], solids: [new LevelSolid(0f, 260f, 600f, 24f)]);
        world.CombatTestSetLevel(level);
        world.TeleportLocalPlayer(300f, 242f);

        Assert.True(world.ControlPointSetupActive);

        var minimumY = world.LocalPlayer.Y;
        var positions = new List<string>();
        for (var tick = 0; tick < 18; tick += 1)
        {
            world.SetLocalInput(new PlayerInputSnapshot(
                Left: tick % 2 == 0,
                Right: tick % 2 != 0,
                Up: tick == 0 || tick == 6 || tick == 12,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: world.LocalPlayer.X,
                AimWorldY: world.LocalPlayer.Y - 100f,
                DebugKill: false));
            world.AdvanceOneTick();
            minimumY = Math.Min(minimumY, world.LocalPlayer.Y);
            positions.Add($"{tick}:({world.LocalPlayer.X:F2},{world.LocalPlayer.Y:F2})");
        }

        Assert.True(
            minimumY >= gate.Bottom + (world.LocalPlayer.Height / 2f) - 0.1f,
            string.Join(" ", positions));
    }

    [Fact]
    public void AdditionalPlayableSlot_CanJoinSpawnAndReleaseCleanly()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(extraSlot));

        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        Assert.False(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.True(player.IsAlive);
        Assert.Equal(PlayerClass.Soldier, player.ClassId);
        Assert.Equal(PlayerTeam.Red, player.Team);
        Assert.Contains(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == extraSlot && entry.Player.Id == player.Id);

        Assert.True(world.TryReleaseNetworkPlayerSlot(extraSlot));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.False(player.IsAlive);
        Assert.DoesNotContain(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == extraSlot);
    }

    [Fact]
    public void AwaitingJoinSlots_AreExcludedFromActiveNetworkPlayers()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        world.PrepareLocalPlayerJoin();
        world.TryPrepareNetworkPlayerJoin(extraSlot);

        Assert.DoesNotContain(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == SimulationWorld.LocalPlayerSlot);
        Assert.DoesNotContain(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == extraSlot);
    }

    [Fact]
    public void ReleasingSlot_ResetsPlayerScoreboardStatsBeforeReuse()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.ApplyNetworkState(
            player.Team,
            CharacterClassCatalog.GetDefinition(player.ClassId),
            isAlive: true,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.Health,
            player.CurrentShells,
            kills: 7,
            deaths: 3,
            caps: 2,
            healPoints: 40,
            activeDominationCount: 0,
            isDominatingLocalViewer: false,
            isDominatedByLocalViewer: false,
            metal: player.Metal,
            isGrounded: player.IsGrounded,
            remainingAirJumps: player.RemainingAirJumps,
            isCarryingIntel: player.IsCarryingIntel,
            intelRechargeTicks: player.IntelRechargeTicks,
            isSpyCloaked: player.IsSpyCloaked,
            spyCloakAlpha: player.SpyCloakAlpha,
            isUbered: player.IsUbered,
            isHeavyEating: player.IsHeavyEating,
            heavyEatTicksRemaining: player.HeavyEatTicksRemaining,
            isSniperScoped: player.IsSniperScoped,
            sniperChargeTicks: player.SniperChargeTicks,
            facingDirectionX: player.FacingDirectionX,
            aimDirectionDegrees: player.AimDirectionDegrees,
            isTaunting: player.IsTaunting,
            tauntFrameIndex: player.TauntFrameIndex,
            isChatBubbleVisible: player.IsChatBubbleVisible,
            chatBubbleFrameIndex: player.ChatBubbleFrameIndex,
            chatBubbleAlpha: player.ChatBubbleAlpha);

        Assert.True(world.TryReleaseNetworkPlayerSlot(extraSlot));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Soldier));

        Assert.Equal(0, player.Kills);
        Assert.Equal(0, player.Deaths);
        Assert.Equal(0, player.Caps);
        Assert.Equal(0, player.HealPoints);
    }

    [Fact]
    public void AdditionalPlayableSlot_CanCaptureEnemyIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;
        var ownBase = world.Level.GetIntelBase(PlayerTeam.Red);

        Assert.True(ownBase.HasValue);
        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        player.TeleportTo(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();

        Assert.False(player.IsCarryingIntel);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(1, player.Caps);
        Assert.True(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void AdditionalPlayableSlot_DeathDropsCarriedIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        Assert.True(world.ForceKillNetworkPlayer(extraSlot));

        Assert.False(player.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void AdditionalPlayableSlot_CanManuallyDropCarriedIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        Assert.True(world.TrySetNetworkPlayerInput(
            extraSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X,
                AimWorldY: player.Y,
                DebugKill: false,
                DropIntel: true)));

        world.AdvanceOneTick();

        Assert.False(player.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void AdditionalPlayableSlot_MoveDownDoesNotDropCarriedIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        Assert.True(world.TrySetNetworkPlayerInput(
            extraSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: true,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X,
                AimWorldY: player.Y,
                DebugKill: false)));

        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);
        Assert.False(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void ReleasingAdditionalPlayableSlot_DropsCarriedIntelAndRemovesOwnedSentry()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Engineer));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();
        Assert.True(player.IsCarryingIntel);

        Assert.True(world.TrySetNetworkPlayerInput(
            extraSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: true,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X,
                AimWorldY: player.Y,
                DebugKill: false)));
        world.AdvanceOneTick();

        Assert.Single(world.Sentries);
        Assert.True(world.TryReleaseNetworkPlayerSlot(extraSlot));

        Assert.False(player.IsAlive);
        Assert.False(player.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
        Assert.Empty(world.Sentries);
    }

    [Fact]
    public void ReleasingAdditionalPlayableSlots_RemovesOwnedProjectiles()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        const byte soldierSlot = 3;
        const byte medicSlot = 4;

        Assert.True(world.TryPrepareNetworkPlayerJoin(soldierSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(soldierSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(soldierSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(soldierSlot, out var soldier));

        Assert.True(world.TryPrepareNetworkPlayerJoin(medicSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(medicSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(medicSlot, PlayerClass.Medic));
        Assert.True(world.TryGetNetworkPlayer(medicSlot, out var medic));

        soldier.TeleportTo(320f, 220f);
        medic.TeleportTo(320f, 260f);

        Assert.True(world.TrySetNetworkPlayerInput(
            soldierSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: soldier.X + 220f,
                AimWorldY: soldier.Y - 20f,
                DebugKill: false)));
        Assert.True(world.TrySetNetworkPlayerInput(
            medicSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: true,
                AimWorldX: medic.X + 220f,
                AimWorldY: medic.Y - 20f,
                DebugKill: false)));

        for (var tick = 0; tick < 3; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.NotEmpty(world.Rockets);
        Assert.NotEmpty(world.Needles);

        Assert.True(world.TryReleaseNetworkPlayerSlot(soldierSlot));
        Assert.True(world.TryReleaseNetworkPlayerSlot(medicSlot));

        Assert.Empty(world.Rockets);
        Assert.Empty(world.Needles);
    }

    [Fact]
    public void ArenaTeamCounts_IncludeAdditionalPlayableSlots()
    {
        var world = CreateWorld();
        const byte redExtraSlot = 3;
        const byte blueExtraSlot = 4;
        const byte blueExtraSlotTwo = 2;

        Assert.True(world.TryLoadLevel("arena_montane"));
        world.DespawnEnemyDummy();
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(SimulationWorld.LocalPlayerSlot, PlayerClass.Scout));
        Assert.True(world.TryPrepareNetworkPlayerJoin(redExtraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(redExtraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(redExtraSlot, PlayerClass.Scout));
        Assert.True(world.TryPrepareNetworkPlayerJoin(blueExtraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(blueExtraSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(blueExtraSlot, PlayerClass.Scout));
        Assert.True(world.TryPrepareNetworkPlayerJoin(blueExtraSlotTwo));
        Assert.True(world.TrySetNetworkPlayerTeam(blueExtraSlotTwo, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(blueExtraSlotTwo, PlayerClass.Scout));

        Assert.Equal(2, world.ArenaRedPlayerCount);
        Assert.Equal(2, world.ArenaBluePlayerCount);
        Assert.Equal(2, world.ArenaRedAliveCount);
        Assert.Equal(2, world.ArenaBlueAliveCount);
    }

    [Fact]
    public void SentryTargetsAdditionalPlayableEnemy()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        SetLocalClassAndRespawn(world, PlayerClass.Engineer);
        Assert.True(world.TryBuildLocalSentry());
        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        var sentry = Assert.Single(world.Sentries);
        player.TeleportTo(sentry.X + 48f, sentry.Y);

        var acquiredTarget = false;
        for (var tick = 0; tick < 180; tick += 1)
        {
            world.AdvanceOneTick();
            if (sentry.CurrentTargetPlayerId == player.Id)
            {
                acquiredTarget = true;
            }

            if (player.Health < player.MaxHealth)
            {
                break;
            }
        }

        Assert.True(acquiredTarget);
        Assert.True(player.Health < player.MaxHealth);
    }

    [Fact]
    public void AdditionalPlayableMedic_CanHealAdditionalPlayableTeammate()
    {
        var world = CreateWorld();
        const byte medicSlot = 3;
        const byte teammateSlot = 5;

        Assert.True(world.TryPrepareNetworkPlayerJoin(medicSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(medicSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(medicSlot, PlayerClass.Medic));
        Assert.True(world.TryGetNetworkPlayer(medicSlot, out var medic));

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var teammate));

        teammate.TeleportTo(medic.X + 24f, medic.Y);
        teammate.ForceSetHealth(Math.Max(1, teammate.MaxHealth / 3));

        Assert.True(world.TrySetNetworkPlayerInput(
            medicSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: teammate.X,
                AimWorldY: teammate.Y,
                DebugKill: false)));

        var beganHealing = false;
        for (var tick = 0; tick < 10; tick += 1)
        {
            world.AdvanceOneTick();
            if (medic.IsMedicHealing && medic.MedicHealTargetId == teammate.Id)
            {
                beganHealing = true;
                break;
            }
        }

        Assert.True(beganHealing);
        Assert.Equal(teammate.Id, medic.MedicHealTargetId);
        Assert.True(teammate.Health > teammate.MaxHealth / 3);
        Assert.True(medic.MedicUberCharge > 0f);
    }

    [Fact]
    public void AdditionalPlayableSlot_SelfKillDoesNotRecordDeathCamState()
    {
        var world = CreateWorld();
        const byte victimSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(victimSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(victimSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(victimSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(victimSlot, out var victim));

        victim.TeleportTo(320f, 220f);

        Assert.True(world.ForceKillNetworkPlayer(victimSlot));

        var deathCam = world.GetNetworkPlayerDeathCam(victimSlot);
        Assert.Null(deathCam);
    }

    [Fact]
    public void EndedRound_HumiliatesLosersAndBlocksCombatInput()
    {
        var world = CreateWorld();
        const byte losingSlot = 3;

        Assert.True(world.TryLoadLevel("destroy"));
        Assert.True(world.TryPrepareNetworkPlayerJoin(losingSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(losingSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(losingSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(losingSlot, out var losingPlayer));

        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);
        Assert.True(world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f));
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Assert.True(world.IsPlayerHumiliated(losingPlayer));
        Assert.False(world.IsPlayerHumiliated(world.LocalPlayer));

        Assert.True(world.TrySetNetworkPlayerInput(
            losingSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: losingPlayer.X + 80f,
                AimWorldY: losingPlayer.Y,
                DebugKill: false)));

        world.AdvanceOneTick();

        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void EndedRound_HumiliatedSpyForceDecloaks()
    {
        var world = CreateWorld();
        const byte losingSlot = 3;

        Assert.True(world.TryLoadLevel("destroy"));
        Assert.True(world.TryPrepareNetworkPlayerJoin(losingSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(losingSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(losingSlot, PlayerClass.Spy));
        Assert.True(world.TryGetNetworkPlayer(losingSlot, out var losingSpy));
        Assert.True(losingSpy.TryToggleSpyCloak());
        Assert.True(losingSpy.IsSpyCloaked);

        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);
        Assert.True(world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f));

        world.AdvanceOneTick();

        Assert.False(losingSpy.IsSpyCloaked);
        Assert.Equal(1f, losingSpy.SpyCloakAlpha);
    }

    [Fact]
    public void FullMapChange_ResetsPlayableSlotsToAwaitingJoin()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var extraPlayer));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.True(extraPlayer.IsAlive);

        Assert.True(world.TryLoadLevel("Waterway", mapAreaIndex: 1, preservePlayerStats: false));

        Assert.True(world.IsNetworkPlayerAwaitingJoin(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.False(extraPlayer.IsAlive);
    }

    private static SimulationWorld CreateWorld(int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond)
    {
        return new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = ticksPerSecond,
        });
    }

    private static SimpleLevel CreateLevel(
        GameModeKind mode = GameModeKind.CaptureTheFlag,
        IReadOnlyList<RoomObjectMarker>? roomObjects = null,
        IReadOnlyList<LevelSolid>? solids = null)
    {
        var spawn = new SpawnPoint(100f, 220f);
        return new SimpleLevel(
            name: "SimulationTest",
            mode: mode,
            bounds: new WorldBounds(1000f, 600f),
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [new SpawnPoint(900f, 220f)],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: roomObjects ?? Array.Empty<RoomObjectMarker>(),
            floorY: 500f,
            solids: solids ?? Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }

    [Fact]
    public void EnemyTrainingDummy_CanBeDisabledIndependently()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = SimulationConfig.DefaultTicksPerSecond,
            EnableLocalDummies = true,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = true,
        });

        Assert.False(world.EnemyPlayerEnabled);
        world.SpawnEnemyDummy();
        Assert.False(world.EnemyPlayerEnabled);

        world.SpawnFriendlyDummy();
        Assert.True(world.FriendlyDummyEnabled);
    }

    [Fact]
    public void FriendlySupportDummy_CanBeDisabledIndependently()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = SimulationConfig.DefaultTicksPerSecond,
            EnableLocalDummies = true,
            EnableEnemyTrainingDummy = true,
            EnableFriendlySupportDummy = false,
        });

        world.SpawnFriendlyDummy();
        Assert.False(world.FriendlyDummyEnabled);

        world.SpawnEnemyDummy();
        Assert.True(world.EnemyPlayerEnabled);
    }

    private static void AdvanceTicks(SimulationWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }

    private static void SetLocalClassAndRespawn(SimulationWorld world, PlayerClass playerClass)
    {
        Assert.True(world.TrySetLocalClass(playerClass));
        AdvanceTicks(world, 150);
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
    }

    private static float RunWallRubSpinjumpSequence(LegacyMovementState movementState, bool turnBeforeWallRub)
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);

        const float playerX = 200f;
        const float playerY = 120f;
        var wall = new LevelSolid(playerX + (world.LocalPlayer.Width / 2f), playerY - 120f, 12f, 240f);
        world.CombatTestSetLevel(CreateLevel(solids: [wall]));
        world.TeleportLocalPlayer(playerX, playerY);
        world.LocalPlayer.SetMovementState(movementState);

        AdvanceWallRubTick(world, aimLeft: turnBeforeWallRub);
        AdvanceWallRubTick(world, aimLeft: turnBeforeWallRub);

        return world.LocalPlayer.VerticalSpeed;
    }

    private static void AdvanceWallRubTick(SimulationWorld world, bool aimLeft)
    {
        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: true,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + (aimLeft ? -100f : 100f),
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);
    }

    private static float AdvanceAirborneVerticalSpeedForTick(float currentVerticalSpeed, float gravityPerTick, float deltaSeconds)
    {
        var nextVerticalSpeed = LegacyMovementModel.AdvanceVerticalSpeedHalfStep(currentVerticalSpeed, gravityPerTick, deltaSeconds);
        return LegacyMovementModel.AdvanceVerticalSpeedHalfStep(nextVerticalSpeed, gravityPerTick, deltaSeconds);
    }

    private static float TriggerPointBlankRocketJump(SimulationWorld world)
    {
        var floor = new LevelSolid(0f, 500f, 1000f, 40f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(300f, world.LocalPlayer.Y);

        world.CombatTestExplodeRocket(world.LocalPlayer, world.LocalPlayer.X, world.LocalPlayer.Y);
        Assert.True(ConsumePendingExplosion(world));
        return world.LocalPlayer.VerticalSpeed;
    }

    private static void AdvanceRocketTicks(RocketProjectileEntity rocket, int tickCount, float deltaSeconds)
    {
        for (var tick = 0; tick < tickCount; tick += 1)
        {
            rocket.AdvanceOneTick(deltaSeconds);
        }
    }

    private static bool ConsumePendingExplosion(SimulationWorld world)
    {
        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();
        return soundEvents.Any(soundEvent => soundEvent.SoundName == "ExplosionSnd")
            && visualEvents.Any(visualEvent => visualEvent.EffectName == "Explosion");
    }

    private static (bool Exploded, float X, float Y) ConsumePendingExplosionWithVisual(SimulationWorld world)
    {
        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();
        if (!soundEvents.Any(soundEvent => soundEvent.SoundName == "ExplosionSnd"))
        {
            return (false, 0f, 0f);
        }

        var visual = visualEvents.FirstOrDefault(visualEvent => visualEvent.EffectName == "Explosion");
        return visual is null
            ? (false, 0f, 0f)
            : (true, visual.X, visual.Y);
    }

    private static bool AdvanceUntilExplosion(SimulationWorld world, int maxTicks = 60)
    {
        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            world.AdvanceOneTick();
            var soundEvents = world.DrainPendingSoundEvents();
            var visualEvents = world.DrainPendingVisualEvents();
            if (soundEvents.Any(soundEvent => soundEvent.SoundName == "ExplosionSnd")
                && visualEvents.Any(visualEvent => visualEvent.EffectName == "Explosion"))
            {
                return true;
            }
        }

        return false;
    }

    private static (bool Exploded, float X, float Y) AdvanceUntilExplosionWithVisual(SimulationWorld world, int maxTicks = 60)
    {
        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            world.AdvanceOneTick();
            var soundEvents = world.DrainPendingSoundEvents();
            var visualEvents = world.DrainPendingVisualEvents();
            if (!soundEvents.Any(soundEvent => soundEvent.SoundName == "ExplosionSnd"))
            {
                continue;
            }

            var visual = visualEvents.FirstOrDefault(visualEvent => visualEvent.EffectName == "Explosion");
            if (visual is not null)
            {
                return (true, visual.X, visual.Y);
            }
        }

        return (false, 0f, 0f);
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
