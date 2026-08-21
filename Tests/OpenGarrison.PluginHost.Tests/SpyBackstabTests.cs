using OpenGarrison.Core;
using OpenGarrison.Core.LastToDie;
using System.Reflection;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class SpyBackstabTests
{
    [Fact]
    public void StartingBackstabPreservesCurrentHorizontalSpeed()
    {
        var player = new PlayerEntity(id: 1, CharacterClassCatalog.Spy);
        player.ForceSetHealth(player.MaxHealth);

        Assert.True(player.TryToggleSpyCloak());

        player.AddImpulse(120f, 0f);
        Assert.True(player.HorizontalSpeed > 0f);
        var horizontalSpeedBeforeBackstab = player.HorizontalSpeed;

        Assert.True(player.TryStartSpyBackstab(0f));

        Assert.Equal(horizontalSpeedBeforeBackstab, player.HorizontalSpeed);
    }

    [Fact]
    public void BackstabCanRestartWhenReadyBeforePreviousVisualExpires()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Spy));
        world.ForceRespawnLocalPlayer();

        var player = world.LocalPlayer;
        Assert.True(player.TryToggleSpyCloak());
        Assert.True(player.TryStartSpyBackstab(0f));

        for (var tick = 0; tick < PlayerEntity.SpyBackstabWindupTicksDefault + PlayerEntity.SpyBackstabRecoveryTicksDefault; tick += 1)
        {
            player.AdvanceTickState(default, world.Config.FixedDeltaSeconds);
        }

        Assert.True(player.IsSpyBackstabReady);
        Assert.InRange(player.SpyBackstabVisualTicksRemaining, 1, PlayerEntity.SpyBackstabVisualTicksDefault - 1);
        Assert.True(player.TryStartSpyBackstab(180f));
        Assert.Equal(PlayerEntity.SpyBackstabVisualTicksDefault, player.SpyBackstabVisualTicksRemaining);
        Assert.Equal(180f, player.SpyBackstabDirectionDegrees);
    }

    [Fact]
    public void BackstabAnimationSoftlySlowsHorizontalMovementWithoutInput()
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Spy));
        world.ForceRespawnLocalPlayer();

        var player = world.LocalPlayer;
        Assert.True(player.TryToggleSpyCloak());

        player.AddImpulse(120f, 0f);
        var horizontalSpeedBeforeBackstab = player.HorizontalSpeed;
        Assert.True(horizontalSpeedBeforeBackstab > 0f);
        Assert.True(player.TryStartSpyBackstab(0f));

        var neutralInput = new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: player.X + 256f,
            AimWorldY: player.Y,
            DebugKill: false);

        var startedGrounded = player.PrepareMovement(
            neutralInput,
            world.Level,
            world.LocalPlayerTeam,
            world.Config.FixedDeltaSeconds,
            out _);
        player.CompleteMovement(
            world.Level,
            world.LocalPlayerTeam,
            world.Config.FixedDeltaSeconds,
            startedGrounded,
            jumped: false,
            allowDropdownFallThrough: false);

        Assert.InRange(player.HorizontalSpeed, 0.01f, horizontalSpeedBeforeBackstab - 0.01f);
    }

    [Fact]
    public void CloakedPrimaryStartsBackstabWithoutAContactTarget()
    {
        var world = CreateSpyCombatWorld();
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        Assert.True(spy.TryToggleSpyCloak());

        world.SetLocalInput(CreatePrimaryInput(spy.X + 256f, spy.Y));
        world.AdvanceOneTick();

        var animation = Assert.Single(world.StabAnimations);
        Assert.Equal(spy.Id, animation.OwnerId);
        Assert.Equal(PlayerEntity.SpyBackstabWindupTicksDefault, spy.SpyBackstabWindupTicksRemaining);
        Assert.Contains(
            world.PendingVisualEvents,
            effect => effect.EffectName == "BackstabRed" && effect.Count == spy.Id);
    }

    [Fact]
    public void ProfessionalCloakedPrimaryStillBackstabsUnlessSecondaryIsHeld()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Professional],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        Assert.True(spy.TryToggleSpyCloak());

        world.SetLocalInput(CreatePrimaryInput(spy.X + 256f, spy.Y));
        world.AdvanceOneTick();

        Assert.Single(world.StabAnimations);
        Assert.Empty(world.RevolverShots);
        Assert.True(spy.IsSpyBackstabAnimating);
    }

    [Fact]
    public void ProfessionalSecondaryTapDefersDecloakUntilRelease()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Professional],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        Assert.True(spy.TryToggleSpyCloak());

        world.SetLocalInput(CreateCombatInput(spy.X + 256f, spy.Y, fireSecondary: true));
        world.AdvanceOneTick();

        Assert.True(spy.IsSpyCloaked);
        Assert.Equal((byte)1, spy.LastToDieProfessionalFireChordState);

        world.SetLocalInput(default);
        world.AdvanceOneTick();

        Assert.False(spy.IsSpyCloaked);
        Assert.Equal((byte)0, spy.LastToDieProfessionalFireChordState);
    }

    [Fact]
    public void ProfessionalSecondaryHoldFiresCloakedRevolverAndReleaseDoesNotDecloak()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Professional],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        var enemy = AddEnemy(world, slot: 2, x: 24f, y: 0f);
        Assert.True(spy.TryToggleSpyCloak());
        var fullMeter = spy.LastToDieSpyCloakMeterMaximumUnits;

        world.SetLocalInput(CreateCombatInput(
            enemy.X,
            enemy.Y,
            firePrimary: true,
            fireSecondary: true));
        world.AdvanceOneTick();

        Assert.Single(world.RevolverShots);
        Assert.Empty(world.StabAnimations);
        Assert.True(spy.IsSpyCloaked);
        Assert.Equal((byte)2, spy.LastToDieProfessionalFireChordState);
        Assert.Equal(fullMeter - (fullMeter / 5), spy.LastToDieSpyCloakMeterUnits);

        world.SetLocalInput(default);
        world.AdvanceOneTick();

        Assert.True(spy.IsSpyCloaked);
        Assert.Equal((byte)0, spy.LastToDieProfessionalFireChordState);
    }

    [Fact]
    public void InstastabPrimarySpawnsVisualAndHitsAfterAcceleratedWindup()
    {
        var world = CreateSpyCombatWorld();
        Assert.True(world.TryConfigureLastToDiePlayerBuild(
            SimulationWorld.LocalPlayerSlot,
            [LastToDiePerkIds.Spy.Instastab],
            resetDynamicState: true));
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        var enemy = AddEnemy(world, slot: 2, x: 24f, y: 0f);
        Assert.True(spy.TryToggleSpyCloak());

        world.SetLocalInput(CreatePrimaryInput(enemy.X, enemy.Y));
        world.AdvanceOneTick();

        var animation = Assert.Single(world.StabAnimations);
        Assert.Equal(LastToDieDerivedModifiers.SpyInstastabSpeedMultiplier, animation.SpeedMultiplier);
        Assert.Equal(11, animation.LifetimeTicks);
        Assert.Equal(6, spy.SpyBackstabWindupTicksRemaining);

        world.SetLocalInput(default);
        for (var tick = 0; tick < 7; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.False(enemy.IsAlive);
        Assert.Empty(world.StabMasks);
        Assert.Contains(
            world.PendingDamageEvents,
            damageEvent => damageEvent.AttackerPlayerId == spy.Id
                && damageEvent.TargetEntityId == enemy.Id
                && damageEvent.Amount == enemy.MaxHealth);
    }

    [Fact]
    public void StabMaskUsesSourceSpriteBoundsForHighVisualOverlap()
    {
        var world = CreateSpyCombatWorld();
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        var enemy = AddEnemy(world, slot: 2, x: 24f, y: -40f);
        var mask = new StabMaskEntity(id: 100, spy.Id, spy.Team, spy.X, spy.Y, directionDegrees: 0f);

        var hit = GetNearestStabHit(world, mask);

        Assert.Same(enemy, hit.HitPlayer);
    }

    [Fact]
    public void StabMaskHitsTargetBeforeWallButNotBehindWall()
    {
        var world = CreateSpyCombatWorld(
            [
                new LevelSolid(30f, -32f, 32f, 32f),
            ]);
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        var enemy = AddEnemy(world, slot: 2, x: 24f, y: 0f);
        var mask = new StabMaskEntity(id: 101, spy.Id, spy.Team, spy.X, spy.Y, directionDegrees: 0f);

        var hit = GetNearestStabHit(world, mask);

        Assert.Same(enemy, hit.HitPlayer);

        enemy.TeleportTo(40f, 0f);
        hit = GetNearestStabHit(world, mask);

        Assert.Null(hit.HitPlayer);
        Assert.Null(hit.HitSentry);
    }

    [Fact]
    public void RevolverShotSpawnsAtForwardMuzzleUnlessBlocked()
    {
        var world = CreateSpyCombatWorld();
        var spy = world.LocalPlayer;
        spy.TeleportTo(0f, 0f);
        spy.ForceSetAmmo(spy.PrimaryWeapon.MaxAmmo);

        Assert.True(spy.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, spy, 256f, -5f);

        var shot = Assert.Single(world.RevolverShots);
        Assert.InRange(shot.X, 17.9f, 18.1f);
        Assert.InRange(shot.Y, -5.5f, -4.5f);

        var blockedWorld = CreateSpyCombatWorld(
            [
                new LevelSolid(8f, -16f, 10f, 16f),
            ]);
        var blockedSpy = blockedWorld.LocalPlayer;
        blockedSpy.TeleportTo(0f, 0f);
        blockedSpy.ForceSetAmmo(blockedSpy.PrimaryWeapon.MaxAmmo);

        Assert.True(blockedSpy.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(blockedWorld, blockedSpy, 256f, -5f);

        var blockedShot = Assert.Single(blockedWorld.RevolverShots);
        Assert.InRange(blockedShot.X, -0.1f, 0.1f);
        Assert.InRange(blockedShot.Y, -5.5f, -4.5f);
    }

    [Fact]
    public void CivilianUmbrellaShotSpawnsAtMirroredUmbrellaTipUnlessBlocked()
    {
        var world = CreateCivilianCombatWorld();
        var civilian = world.LocalPlayer;
        civilian.TeleportTo(0f, 0f);
        civilian.ForceSetAmmo(civilian.PrimaryWeapon.MaxAmmo);

        Assert.True(civilian.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(world, civilian, 256f, -7f);

        var shot = Assert.Single(world.RevolverShots);
        Assert.InRange(shot.X, 26.9f, 27.1f);
        Assert.InRange(shot.Y, -7.1f, -6.9f);

        var leftWorld = CreateCivilianCombatWorld();
        var leftCivilian = leftWorld.LocalPlayer;
        leftCivilian.TeleportTo(0f, 0f);
        leftCivilian.ForceSetAmmo(leftCivilian.PrimaryWeapon.MaxAmmo);

        Assert.True(leftCivilian.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(leftWorld, leftCivilian, -256f, -7f);

        var leftShot = Assert.Single(leftWorld.RevolverShots);
        Assert.InRange(leftShot.X, -27.1f, -26.9f);
        Assert.InRange(leftShot.Y, -7.1f, -6.9f);

        var blockedWorld = CreateCivilianCombatWorld(
            [
                new LevelSolid(8f, -16f, 10f, 16f),
            ]);
        var blockedCivilian = blockedWorld.LocalPlayer;
        blockedCivilian.TeleportTo(0f, 0f);
        blockedCivilian.ForceSetAmmo(blockedCivilian.PrimaryWeapon.MaxAmmo);

        Assert.True(blockedCivilian.TryFirePrimaryWeapon());
        InvokeFirePrimaryWeapon(blockedWorld, blockedCivilian, 256f, -7f);

        var blockedShot = Assert.Single(blockedWorld.RevolverShots);
        Assert.InRange(blockedShot.X, -6.1f, -5.9f);
        Assert.InRange(blockedShot.Y, -7.1f, -6.9f);
    }

    private static SimulationWorld CreateSpyCombatWorld(IReadOnlyList<LevelSolid>? solids = null)
    {
        var world = new SimulationWorld();
        Assert.True(world.TrySetLocalClass(PlayerClass.Spy));
        SetCombatLevel(
            world,
            new SimpleLevel(
                name: "spy_combat_test",
                mode: GameModeKind.CaptureTheFlag,
                bounds: new WorldBounds(2048f, 2048f),
                mapScale: 1f,
                backgroundAssetName: null,
                mapAreaIndex: 1,
                mapAreaCount: 1,
                localSpawn: new SpawnPoint(0f, 0f),
                redSpawns: [new SpawnPoint(0f, 0f)],
                blueSpawns: [new SpawnPoint(256f, 0f)],
                intelBases:
                [
                    new IntelBaseMarker(PlayerTeam.Red, 0f, 0f),
                    new IntelBaseMarker(PlayerTeam.Blue, 256f, 0f),
                ],
                roomObjects: [],
                floorY: 2048f,
                solids: solids ?? [],
                importedFromSource: false));
        world.ForceRespawnLocalPlayer();
        return world;
    }

    private static SimulationWorld CreateCivilianCombatWorld(IReadOnlyList<LevelSolid>? solids = null)
    {
        var world = new SimulationWorld();
        world.RandomSpreadEnabled = false;
        Assert.True(world.TrySetLocalClass(PlayerClass.Quote));
        SetCombatLevel(
            world,
            new SimpleLevel(
                name: "civilian_combat_test",
                mode: GameModeKind.CaptureTheFlag,
                bounds: new WorldBounds(2048f, 2048f),
                mapScale: 1f,
                backgroundAssetName: null,
                mapAreaIndex: 1,
                mapAreaCount: 1,
                localSpawn: new SpawnPoint(0f, 0f),
                redSpawns: [new SpawnPoint(0f, 0f)],
                blueSpawns: [new SpawnPoint(256f, 0f)],
                intelBases:
                [
                    new IntelBaseMarker(PlayerTeam.Red, 0f, 0f),
                    new IntelBaseMarker(PlayerTeam.Blue, 256f, 0f),
                ],
                roomObjects: [],
                floorY: 2048f,
                solids: solids ?? [],
                importedFromSource: false));
        world.ForceRespawnLocalPlayer();
        return world;
    }

    private static PlayerEntity AddEnemy(SimulationWorld world, byte slot, float x, float y)
    {
        Assert.True(world.TryPrepareNetworkPlayerJoin(slot));
        Assert.True(world.TrySetNetworkPlayerTeam(slot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(slot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(slot, out var enemy));
        enemy.TeleportTo(x, y);
        return enemy;
    }

    private static PlayerInputSnapshot CreatePrimaryInput(float aimWorldX, float aimWorldY) =>
        CreateCombatInput(aimWorldX, aimWorldY, firePrimary: true);

    private static PlayerInputSnapshot CreateCombatInput(
        float aimWorldX,
        float aimWorldY,
        bool firePrimary = false,
        bool fireSecondary = false) =>
        new(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: firePrimary,
            FireSecondary: fireSecondary,
            AimWorldX: aimWorldX,
            AimWorldY: aimWorldY,
            DebugKill: false);

    private static void SetCombatLevel(SimulationWorld world, SimpleLevel level)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestSetLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [level]);
    }

    private static (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, int HitDamageableZoneRoomObjectIndex) GetNearestStabHit(
        SimulationWorld world,
        StabMaskEntity mask)
    {
        var method = typeof(SimulationWorld).GetMethod("CombatTestGetNearestStabHit", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method!.Invoke(world, [mask, mask.FacingLeft ? -1f : 1f, 0f]);
        Assert.NotNull(result);
        return ((float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, int HitDamageableZoneRoomObjectIndex))result!;
    }

    private static void InvokeFirePrimaryWeapon(SimulationWorld world, PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var method = typeof(SimulationWorld).GetMethod("FirePrimaryWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        _ = method!.Invoke(world, [player, aimWorldX, aimWorldY]);
    }
}
